using System.Collections.Generic;
using System.Linq;
using CustomSpawns.Data.Dao;
using CustomSpawns.Data.Dto;
using CustomSpawns.Spawn;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace CustomSpawns.Invasions.Adapters
{
    /**
     * Who is fighting an invasion. A spawned party joins the invasion its template invades for, keeps
     * that membership across a save, and leaves when it dies; reconciliation is the same question
     * asked of the whole map, since parties, clans and configurations all disappear behind our back.
     */
    public sealed class InvasionMembership
    {
        private readonly InvasionState _invasionState;
        private readonly SpawnDao _spawnDao;
        private readonly DynamicSpawnData _dynamicSpawnData;
        private readonly InvasionCaptureCorrelation _invasionCaptureCorrelation;

        public InvasionMembership(InvasionState state, SpawnDao spawns, DynamicSpawnData parties,
            InvasionCaptureCorrelation correlation)
        {
            _invasionState = state;
            _spawnDao = spawns;
            _dynamicSpawnData = parties;
            _invasionCaptureCorrelation = correlation;
        }

        public void Register(MobileParty party, CsPartyData data)
        {
            string? templateId = data.OwningSpawnTemplateId;
            Clan? clan = party.ActualClan;
            // The party's clan names its invasion, and it survives the rebuilds that lose everything we
            // write on the party. Membership therefore outlives a removed configuration, so cleanup
            // remains possible.
            Invasion? saved = _invasionState.FindInvasionHolding(clan?.StringId);

            if (saved != null)
            {
                // Another spawn of the same faction is not the invasion's: it only shares its clan.
                if (saved.SpawnTemplateId != templateId)
                {
                    data.SetInvasionRole(null);
                    return;
                }

                saved.Members.Add(party.StringId);
                data.SetInvasionRole(data.InvasionRole ?? RoleFor(data));
                return;
            }

            if (templateId == null || clan == null || clan.IsEliminated || clan.IsBanditFaction ||
                _spawnDao.FindByPartyTemplateId(templateId)?.Invasion == null)
            {
                data.SetInvasionRole(null);
                return;
            }

            _invasionState.RegisterMember(templateId, clan.StringId, party.StringId);
            data.SetInvasionRole(RoleFor(data));
        }

        /**
         * Two paths hand an invasion clan an ordinary lord party. A spawned party is handed to its clan
         * leader as owner, and unless a lord is generated to lead it that clan leader stays its owner
         * without ever entering a roster, so vanilla reads it as party-less and fields a party for it
         * whenever the clan sits under its tier party limit -- the common case, and the one that keeps
         * happening all campaign. A generated besieging lord is not that hero: it both owns and leads
         * the party it was made for, and is enrolled in its roster. The other path is a living invasion
         * party changing into a lord party when the invasion establishes its kingdom. Either way the
         * party arrives under a StringId no recognition path of ours
         * matches, so it carries on without its membership, its steering and its spawn-sized party
         * limit. Registering it under its invasion's spawn template gives it back the party data all
         * three are read from. A defeated lord is not a case here: it is removed rather
         * than left to come back.
         */
        public void Adopt(MobileParty party)
        {
            string? templateId = FindAdoptingTemplate(party, _dynamicSpawnData.GetDynamicSpawnData(party),
                _invasionState);

            if (templateId != null)
            {
                _dynamicSpawnData.RegisterAdoptedParty(party, templateId);
            }
        }

        /**
         * A party we already know keeps the identity it was registered with, and only a lord party can
         * be an invasion party the game rebuilt: the garrisons, caravans and militias of the same clan
         * are not ours to adopt.
         */
        public static string? FindAdoptingTemplate(MobileParty party, CsPartyData? registered,
            InvasionState invasionState)
        {
            if (registered != null || party.LordPartyComponent == null)
            {
                return null;
            }

            return invasionState.FindFightingInvasion(party.ActualClan?.StringId)?.SpawnTemplateId;
        }

        public void RegisterLive()
        {
            foreach (MobileParty party in MobileParty.All.Where(p => p.IsActive))
            {
                CsPartyData? data = _dynamicSpawnData.GetDynamicSpawnData(party);

                if (data != null)
                {
                    Register(party, data);
                }
            }
        }

        public void Remove(MobileParty party)
        {
            _invasionState.RemoveMember(party.StringId);
            Reconcile();
        }

        public void Reconcile()
        {
            _invasionState.Reconcile(MobileParty.All.Where(p => p.IsActive).Select(p => p.StringId),
                id => Settlement.Find(id)?.OwnerClan?.StringId,
                invasion => GetConfiguration(invasion) != null && Clan.FindFirst(c => c.StringId == invasion.ClanId)?.IsEliminated == false,
                invasion => InvasionKingdomRetirement.FindRetirableKingdom(invasion) != null);

            _invasionCaptureCorrelation.Prune();
        }

        public IReadOnlyList<MobileParty> FindLiveMembers(MobileParty party)
        {
            Invasion? invasion = _invasionState.FindInvasionOfParty(party.StringId);

            if (invasion == null)
            {
                return new[] { party };
            }

            return MobileParty.All.Where(member => member.IsActive && invasion.Members.Contains(member.StringId))
                .ToList();
        }

        public static string RoleFor(CsPartyData data) =>
            data.IsAdopted ? "Adopted" :
            data.IsPrimarySpawn ? "Original" : "Escort";

        private InvasionData? GetConfiguration(Invasion invasion) => _spawnDao.FindByPartyTemplateId(invasion.SpawnTemplateId)?.Invasion;
    }
}
