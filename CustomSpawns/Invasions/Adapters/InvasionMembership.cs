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
        private readonly ISettledInvasionQuery _settledInvasionQuery;

        public InvasionMembership(InvasionState state, SpawnDao spawns, DynamicSpawnData parties,
            InvasionCaptureCorrelation correlation, ISettledInvasionQuery settledInvasionQuery)
        {
            _settledInvasionQuery = settledInvasionQuery;
            _invasionState = state;
            _spawnDao = spawns;
            _dynamicSpawnData = parties;
            _invasionCaptureCorrelation = correlation;
        }

        public void Register(MobileParty party, CsPartyData data)
        {
            Clan? clan = party.ActualClan;
            data.SetInvasionRole(_invasionState.RegisterParty(party.StringId, data.OwningSpawnTemplateId,
                (clan?.StringId, clan?.IsEliminated == true, clan?.IsBanditFaction == true),
                (data.InvasionRole, data.IsAdopted, data.IsPrimarySpawn),
                templateId => _spawnDao.FindByPartyTemplateId(templateId)?.Invasion != null));
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
            string? templateId = _invasionState.FindAdoptingTemplate(party.ActualClan?.StringId,
                _dynamicSpawnData.GetDynamicSpawnData(party) != null, party.LordPartyComponent != null);

            if (templateId != null)
            {
                _dynamicSpawnData.RegisterAdoptedParty(party, templateId);
            }
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
                invasion => GetConfiguration(invasion) != null && !_settledInvasionQuery.IsSettled(invasion.ClanId) &&
                            invasion.FindLeadingClan() != null,
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

        private InvasionData? GetConfiguration(Invasion invasion) => _spawnDao.FindByPartyTemplateId(invasion.SpawnTemplateId)?.Invasion;
    }
}
