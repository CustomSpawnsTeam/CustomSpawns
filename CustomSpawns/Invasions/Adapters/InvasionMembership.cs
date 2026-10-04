using System;
using System.Collections.Generic;
using System.Linq;
using CustomSpawns.Data.Dao;
using CustomSpawns.Data.Dto;
using CustomSpawns.Spawn;
using NLog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace CustomSpawns.Invasions.Adapters
{
    /**
     * Who is fighting an invasion. A spawned party joins the group its template invades for, keeps
     * that membership across a save, and leaves when it dies; reconciliation is the same question
     * asked of the whole map, since parties, clans and configurations all disappear behind our back.
     */
    internal sealed class InvasionMembership
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly InvasionState _invasionState;
        private readonly SpawnDao _spawnDao;
        private readonly DynamicSpawnData _dynamicSpawnData;
        private readonly InvasionCaptureCorrelation _invasionCaptureCorrelation;

        internal InvasionMembership(InvasionState state, SpawnDao spawns, DynamicSpawnData parties,
            InvasionCaptureCorrelation correlation)
        {
            _invasionState = state;
            _spawnDao = spawns;
            _dynamicSpawnData = parties;
            _invasionCaptureCorrelation = correlation;
        }

        internal void Register(MobileParty party, CsPartyData data)
        {
            string? templateId = data.OwningSpawnTemplateId;
            Clan? clan = party.ActualClan;
            // The party's clan names its invasion, and it survives the rebuilds that lose everything we
            // write on the party. Membership therefore outlives a removed configuration, so cleanup
            // remains possible.
            InvasionGroupState? saved = _invasionState.FindGroupHolding(clan?.StringId);

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
         * party changing component, as one does when the invasion becomes a kingdom and loses its only
         * coastal town. Either way the party arrives under a StringId no recognition path of ours
         * matches, so it carries on without its membership, its food eligibility, its steering and its
         * spawn-sized party limit. Registering it under its group's spawn template gives it back the
         * party data all four are read from. A defeated lord is not a case here: it is removed rather
         * than left to come back.
         */
        internal void Adopt(MobileParty party) =>
            Adopt(party, CountLiveCountedMembers, BudgetOf);

        // The live count and the budget are parameters so a test can drive adoption off the campaign map.
        internal void Adopt(MobileParty party, Func<InvasionGroupState, int> countLiveMembers,
            Func<InvasionGroupState, int> partyBudget)
        {
            CsPartyData? registered = _dynamicSpawnData.GetDynamicSpawnData(party);
            string? templateId = FindAdoptingTemplate(party, registered, _invasionState, countLiveMembers, partyBudget);

            if (templateId == null)
            {
                ReportAdoptionRefusal(party, registered, countLiveMembers, partyBudget);
                return;
            }

            _dynamicSpawnData.RegisterAdoptedParty(party, templateId);
        }

        /**
         * A budget refusal leaves a rebuilt party outside its invasion for good, and nothing else on the
         * map says so, so it is reported rather than left to be inferred from a lord that stopped invading.
         */
        private void ReportAdoptionRefusal(MobileParty party, CsPartyData? registered,
            Func<InvasionGroupState, int> countLiveMembers, Func<InvasionGroupState, int> partyBudget)
        {
            if (registered != null || party.LordPartyComponent == null)
            {
                return;
            }

            InvasionGroupState? group = _invasionState.FindFightingGroup(party.ActualClan?.StringId);

            if (group != null)
            {
                Logger.Info("Invasion adoption refused: group=" + group.ClanId + " party=" + party.StringId +
                            " counted_parties=" + countLiveMembers(group) + " budget=" + partyBudget(group));
            }
        }

        /**
         * A party we already know keeps the identity it was registered with, and only a lord party can
         * be an invasion party the game rebuilt: the garrisons, caravans and militias of the same clan
         * are not ours to adopt.
         */
        internal static string? FindAdoptingTemplate(MobileParty party, CsPartyData? registered,
            InvasionState invasionState, Func<InvasionGroupState, int> countLiveMembers,
            Func<InvasionGroupState, int> partyBudget)
        {
            if (registered != null || party.LordPartyComponent == null)
            {
                return null;
            }

            return invasionState.FindAdoptingGroup(party.ActualClan?.StringId, countLiveMembers, partyBudget)
                ?.SpawnTemplateId;
        }

        /**
         * Escorts are outside the budget: nothing bounds how many of them a group holds, since the spawner
         * frees a primary's slot when it dies but leaves its escorts on the map, so counting them would let
         * the count drift past the budget and switch adoption off for good. What the budget bounds -- the
         * parties an invasion leads with -- is therefore what it counts.
         */
        private int CountLiveCountedMembers(InvasionGroupState group) =>
            MobileParty.All.Count(party => party.IsActive && group.Members.Contains(party.StringId) &&
                _dynamicSpawnData.GetDynamicSpawnData(party)?.IsEscort != true);

        private int BudgetOf(InvasionGroupState group) =>
            PartyBudget(_spawnDao.FindByPartyTemplateId(group.SpawnTemplateId));

        /**
         * How many non-escort parties the configuration fields for a group: the primary parties the spawner
         * keeps on the map plus the reinforcement landings the invasion may hold at once. A spawn that
         * invades for nothing budgets nothing.
         *
         * This bounds adoption, not the parties alive: the spawner keeps its own count, so a dead primary
         * frees a spawn slot and opens adoption headroom at the same time, and a group can hold more live
         * non-escort parties than this until the next death settles it.
         */
        internal static int PartyBudget(SpawnDto? spawn) =>
            spawn?.Invasion == null
                ? 0
                : spawn.MaximumConcurrentPrimaries +
                  spawn.Invasion.ReinforcementRules.Sum(rule => rule.MaxConcurrentParties);

        internal void RegisterLive()
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

        internal void Remove(MobileParty party)
        {
            _invasionState.RemoveMember(party.StringId);
            Reconcile();
        }

        internal void Reconcile()
        {
            _invasionState.Reconcile(MobileParty.All.Where(p => p.IsActive).Select(p => p.StringId),
                id => Settlement.Find(id)?.OwnerClan?.StringId,
                group => GetConfiguration(group) != null && Clan.FindFirst(c => c.StringId == group.ClanId)?.IsEliminated == false,
                group => InvasionKingdomRetirement.FindRetirableKingdom(group) != null);

            _invasionCaptureCorrelation.Prune();
        }

        internal IReadOnlyList<MobileParty> FindLiveMembers(MobileParty party)
        {
            InvasionGroupState? group = _invasionState.FindMemberGroup(party.StringId);

            if (group == null)
            {
                return new[] { party };
            }

            return MobileParty.All.Where(member => member.IsActive && group.Members.Contains(member.StringId))
                .ToList();
        }

        internal static string RoleFor(CsPartyData data) => data.IsIndependentReinforcement ? "Reinforcement" :
            data.IsAdopted ? "Adopted" :
            data.IsPrimarySpawn ? "Original" : "Escort";

        private InvasionData? GetConfiguration(InvasionGroupState group) => _spawnDao.FindByPartyTemplateId(group.SpawnTemplateId)?.Invasion;
    }
}
