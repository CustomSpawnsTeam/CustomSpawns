using System;
using System.Collections.Generic;
using System.Linq;
using CustomSpawns.AI.Model;
using CustomSpawns.Data.Adapter;
using CustomSpawns.Spawn;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace CustomSpawns.AI
{
    internal sealed class BannerlordBesiegeClosestSettlementContext
        : BannerlordSettlementOffensiveContext, IBesiegeClosestSettlementContext
    {
        private readonly Func<MobileParty, IEnumerable<SiegeTargetCandidate>> _getCandidates;

        public BannerlordBesiegeClosestSettlementContext(DynamicSpawnData dynamicSpawnData) : this(
            observer => observer.SeeingRange, ObserveRoamingParties, ObserveCandidates,
            dynamicSpawnData.FindAccompanyingParties)
        {
        }

        internal BannerlordBesiegeClosestSettlementContext(Func<MobileParty, float> getSeeingRange,
            Func<MobileParty, IEnumerable<RoamingPartyStrength>> getRoamingParties,
            Func<MobileParty, IEnumerable<SiegeTargetCandidate>> getCandidates,
            Func<MobileParty, IEnumerable<MobileParty>> getAccompanyingParties)
            : base(getSeeingRange, getRoamingParties, getAccompanyingParties)
        {
            _getCandidates = getCandidates;
        }

        public Settlement? FindClosestSiegeTarget(MobileParty party, float attackerStrength,
            float minimumRatio, IReadOnlyList<SpawnSettlementType> targetTypes)
        {
            return _getCandidates(party)
                .Where(candidate => candidate.IsActive && candidate.IsHostile &&
                                    candidate.HasCompatibleSiege && targetTypes.Contains(candidate.Type) &&
                                    SettlementOffensiveBehaviour.MeetsStrengthRatio(attackerStrength,
                                        candidate.DefenderStrength, minimumRatio))
                .OrderBy(candidate => candidate.Distance)
                .Select(candidate => candidate.Target)
                .FirstOrDefault();
        }

        public float GetTargetStrength(MobileParty party, Settlement target) =>
            GetDefenderStrength(party, target);

        public IFaction GetTargetMapFaction(Settlement target) => target.MapFaction;

        public bool IsMatchingSiegeActive(MobileParty party, Settlement target)
        {
            var camp = target.SiegeEvent?.BesiegerCamp;
            return camp is not null && camp.SiegeEvent.BesiegedSettlement == target &&
                   (party.BesiegerCamp == camp || camp.IsBesiegerSideParty(party) ||
                    camp.HasInvolvedPartyForEventType(party.Party));
        }

        public bool IsMatchingAssaultActive(MobileParty party, Settlement target)
        {
            return party.MapEvent is not null && party.MapEvent.IsSiegeAssault &&
                   party.MapEvent.MapEventSettlement == target &&
                   party.MapEventSide == party.MapEvent.AttackerSide;
        }

        public bool IsCurrentTargetValid(MobileParty party, Settlement target,
            bool matchingOperationActive, IFaction? intendedTargetFaction)
        {
            return target.IsActive && (target.IsTown || target.IsCastle) &&
                   party.MapFaction.IsAtWarWith(target.MapFaction) &&
                   IsIntendedTargetFaction(target.MapFaction, intendedTargetFaction) &&
                   (target.SiegeEvent is null || matchingOperationActive);
        }

        internal static bool IsIntendedTargetFaction(IFaction currentFaction,
            IFaction? intendedTargetFaction)
        {
            return currentFaction == intendedTargetFaction;
        }

        public bool IsTargetComplete(MobileParty party, Settlement target)
        {
            return target.MapFaction == party.MapFaction;
        }

        public bool HasNativeContinuation(MobileParty party, Settlement target)
        {
            return (party.DefaultBehavior == AiBehavior.BesiegeSettlement ||
                    party.DefaultBehavior == AiBehavior.AssaultSettlement) &&
                   party.TargetSettlement == target;
        }

        public AIBehaviorData CreateSiegeBehavior(MobileParty party, Settlement target)
        {
            return new AIBehaviorData(target, AiBehavior.BesiegeSettlement,
                party.NavigationCapability, false, false, false);
        }

        internal static float CalculateDefenderStrength(float militiaStrength, float garrisonStrength,
            IEnumerable<SettlementDefenderPartyStrength> occupants)
        {
            return militiaStrength + garrisonStrength + occupants
                .Where(p => p.IsActive && p.IsHostileToAttacker && !p.IsVillager && !p.IsCaravan &&
                            !p.IsGarrison && !p.IsMilitia)
                .Sum(p => p.Strength);
        }

        private static IEnumerable<SiegeTargetCandidate> ObserveCandidates(MobileParty party)
        {
            return Settlement.All.Where(s => s.IsTown || s.IsCastle).Select(settlement =>
                new SiegeTargetCandidate(settlement, settlement.IsActive,
                    party.MapFaction.IsAtWarWith(settlement.MapFaction),
                    settlement.IsTown ? SpawnSettlementType.Town : SpawnSettlementType.Castle,
                    settlement.SiegeEvent is null || IsMatchingCamp(party, settlement),
                    party.Position.Distance(settlement.GatePosition), GetDefenderStrength(party, settlement)));
        }

        private static bool IsMatchingCamp(MobileParty party, Settlement settlement)
        {
            var camp = settlement.SiegeEvent?.BesiegerCamp;
            return camp is not null && camp.SiegeEvent.BesiegedSettlement == settlement &&
                   (party.BesiegerCamp == camp || camp.IsBesiegerSideParty(party) ||
                    camp.HasInvolvedPartyForEventType(party.Party));
        }

        private static float GetDefenderStrength(MobileParty attacker, Settlement settlement)
        {
            float garrisonStrength = settlement.Town.GarrisonParty?.Party.CalculateCurrentStrength() ?? 0f;
            IEnumerable<SettlementDefenderPartyStrength> occupants = settlement.Parties.Select(p =>
                new SettlementDefenderPartyStrength(p.Party.CalculateCurrentStrength(), p.IsActive,
                    p.MapFaction.IsAtWarWith(attacker.MapFaction), p.IsVillager, p.IsCaravan,
                    p.IsGarrison, p.IsMilitia, p.IsLordParty, p.MapFaction == settlement.MapFaction));
            return CalculateDefenderStrength(settlement.Militia, garrisonStrength, occupants);
        }
    }
}
