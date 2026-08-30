using System;
using System.Collections.Generic;
using System.Linq;
using CustomSpawns.AI.Model;
using CustomSpawns.Spawn;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace CustomSpawns.AI
{
    internal sealed class BannerlordRaidClosestVillageAdapter
        : BannerlordSettlementOffensiveAdapter, IRaidOperations
    {
        public BannerlordRaidClosestVillageAdapter(DynamicSpawnData dynamicSpawnData) : this(
            observer => observer.SeeingRange, ObserveRoamingParties,
            dynamicSpawnData.FindAccompanyingParties)
        {
        }

        internal BannerlordRaidClosestVillageAdapter(Func<MobileParty, float> getSeeingRange,
            Func<MobileParty, IEnumerable<RoamingPartyStrength>> getRoamingParties,
            Func<MobileParty, IEnumerable<MobileParty>> getAccompanyingParties)
            : base(getSeeingRange, getRoamingParties, getAccompanyingParties)
        {
        }

        public Settlement? FindClosestRaidTarget(MobileParty party, float attackerStrength,
            float minimumRatio)
        {
            IEnumerable<RaidTargetCandidate<Settlement>> candidates = Settlement.All.Select(settlement =>
                new RaidTargetCandidate<Settlement>(settlement, IsCandidateValid(party, settlement),
                    party.Position.Distance(settlement.GatePosition), settlement.Militia));
            return SettlementOffensiveBehaviour.ChooseClosestTarget(candidates, attackerStrength, minimumRatio);
        }

        public float GetTargetStrength(Settlement target)
        {
            return target.Militia;
        }

        public bool IsMatchingRaidActive(MobileParty party, Settlement target)
        {
            return party.MapEvent is not null && party.MapEvent.IsRaid &&
                   party.MapEvent.MapEventSettlement == target;
        }

        public bool IsCurrentTargetValid(MobileParty party, Settlement target,
            bool matchingRaidActive)
        {
            return target.IsActive && target.IsVillage && !target.IsRaided &&
                   (target.Party.MapEvent is null || matchingRaidActive) &&
                   party.MapFaction.IsAtWarWith(target.MapFaction);
        }

        public bool IsTargetComplete(Settlement target)
        {
            return target.IsRaided;
        }

        public bool HasNativeContinuation(MobileParty party, Settlement target)
        {
            return party.DefaultBehavior == AiBehavior.RaidSettlement &&
                   party.TargetSettlement == target;
        }

        public AIBehaviorData CreateRaidBehavior(MobileParty party, Settlement target)
        {
            return new AIBehaviorData(target, AiBehavior.RaidSettlement,
                party.NavigationCapability, false, false, false);
        }

        private static bool IsCandidateValid(MobileParty party, Settlement settlement)
        {
            return settlement.IsActive && settlement.IsVillage && !settlement.IsRaided &&
                   !settlement.IsUnderRaid && settlement.Party.MapEvent is null &&
                   party.MapFaction.IsAtWarWith(settlement.MapFaction);
        }
    }
}
