using System;
using System.Collections.Generic;
using System.Linq;
using CustomSpawns.Data.Adapter;
using CustomSpawns.AI.Model;
using CustomSpawns.AI.Offensives;
using CustomSpawns.Spawn;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace CustomSpawns.AI.Adapters
{
    internal sealed class BannerlordBesiegeClosestSettlementAdapter
        : BannerlordSettlementOffensiveAdapter, ISiegeOperations
    {
        private const float UnboundedLandPathDistance = float.MaxValue;
        private readonly Func<MobileParty, IEnumerable<SiegeTargetCandidate>> _getCandidates;
        private readonly Func<MobileParty, Settlement, IEnumerable<RoamingPartyStrength>>
            _getTargetRoamingParties;
        private readonly ISiegeTargetPolicy _siegeTargetPolicy;

        public BannerlordBesiegeClosestSettlementAdapter(DynamicSpawnData dynamicSpawnData) : this(
            observer => observer.SeeingRange, ObserveRoamingParties, ObserveCandidates,
            dynamicSpawnData.FindAccompanyingParties, new NoRestrictionSiegeTargetPolicy(),
            ObserveRoamingPartiesNearTarget)
        {
        }

        internal BannerlordBesiegeClosestSettlementAdapter(DynamicSpawnData dynamicSpawnData,
            Func<MobileParty, Func<Settlement, bool>?> getInitialCoastalTargetFilter) : this(
            observer => observer.SeeingRange, ObserveRoamingParties, ObserveCandidates,
            dynamicSpawnData.FindAccompanyingParties,
            new InvasionCoastalSiegeTargetPolicy(getInitialCoastalTargetFilter, HasReachableLandGate),
            ObserveRoamingPartiesNearTarget)
        {
        }

        internal BannerlordBesiegeClosestSettlementAdapter(Func<MobileParty, float> getSeeingRange,
            Func<MobileParty, IEnumerable<RoamingPartyStrength>> getRoamingParties,
            Func<MobileParty, IEnumerable<SiegeTargetCandidate>> getCandidates,
            Func<MobileParty, IEnumerable<MobileParty>> getAccompanyingParties)
            : this(getSeeingRange, getRoamingParties, getCandidates, getAccompanyingParties,
                new NoRestrictionSiegeTargetPolicy(), (_, _) => Enumerable.Empty<RoamingPartyStrength>())
        {
        }

        internal BannerlordBesiegeClosestSettlementAdapter(Func<MobileParty, float> getSeeingRange,
            Func<MobileParty, IEnumerable<RoamingPartyStrength>> getRoamingParties,
            Func<MobileParty, IEnumerable<SiegeTargetCandidate>> getCandidates,
            Func<MobileParty, IEnumerable<MobileParty>> getAccompanyingParties,
            Func<MobileParty, Settlement, IEnumerable<RoamingPartyStrength>> getTargetRoamingParties)
            : this(getSeeingRange, getRoamingParties, getCandidates, getAccompanyingParties,
                new NoRestrictionSiegeTargetPolicy(), getTargetRoamingParties)
        {
        }

        internal BannerlordBesiegeClosestSettlementAdapter(Func<MobileParty, float> getSeeingRange,
            Func<MobileParty, IEnumerable<RoamingPartyStrength>> getRoamingParties,
            Func<MobileParty, IEnumerable<SiegeTargetCandidate>> getCandidates,
            Func<MobileParty, IEnumerable<MobileParty>> getAccompanyingParties,
            Func<MobileParty, Func<Settlement, bool>?> getInitialCoastalTargetFilter,
            Func<MobileParty, Settlement, bool> hasReachableLandGate)
            : this(getSeeingRange, getRoamingParties, getCandidates, getAccompanyingParties,
                new InvasionCoastalSiegeTargetPolicy(getInitialCoastalTargetFilter, hasReachableLandGate),
                (_, _) => Enumerable.Empty<RoamingPartyStrength>())
        {
        }

        internal BannerlordBesiegeClosestSettlementAdapter(Func<MobileParty, float> getSeeingRange,
            Func<MobileParty, IEnumerable<RoamingPartyStrength>> getRoamingParties,
            Func<MobileParty, IEnumerable<SiegeTargetCandidate>> getCandidates,
            Func<MobileParty, IEnumerable<MobileParty>> getAccompanyingParties,
            ISiegeTargetPolicy siegeTargetPolicy,
            Func<MobileParty, Settlement, IEnumerable<RoamingPartyStrength>> getTargetRoamingParties)
            : base(getSeeingRange, getRoamingParties, getAccompanyingParties)
        {
            _getCandidates = getCandidates;
            _siegeTargetPolicy = siegeTargetPolicy;
            _getTargetRoamingParties = getTargetRoamingParties;
        }

        public Settlement? FindClosestSiegeTarget(MobileParty party, float attackerStrength,
            float minimumRatio, IReadOnlyList<SpawnSettlementType> targetTypes)
        {
            return FindClosestSiegeTarget(party, null, attackerStrength, minimumRatio,
                minimumRatio, targetTypes);
        }

        public Settlement? FindClosestSiegeTarget(MobileParty party, float attackerStrength,
            float minimumTargetRatio, float minimumRoamingHostilesRatio,
            IReadOnlyList<SpawnSettlementType> targetTypes)
        {
            return FindClosestSiegeTarget(party, null, attackerStrength, minimumTargetRatio,
                minimumRoamingHostilesRatio, targetTypes);
        }

        public Settlement? FindAlternativeSiegeTarget(MobileParty party, Settlement excludedTarget,
            float attackerStrength, float minimumRatio, IReadOnlyList<SpawnSettlementType> targetTypes)
        {
            return FindClosestSiegeTarget(party, excludedTarget, attackerStrength, minimumRatio,
                minimumRatio, targetTypes);
        }

        public Settlement? FindAlternativeSiegeTarget(MobileParty party, Settlement excludedTarget,
            float attackerStrength, float minimumTargetRatio, float minimumRoamingHostilesRatio,
            IReadOnlyList<SpawnSettlementType> targetTypes)
        {
            return FindClosestSiegeTarget(party, excludedTarget, attackerStrength, minimumTargetRatio,
                minimumRoamingHostilesRatio, targetTypes);
        }

        private Settlement? FindClosestSiegeTarget(MobileParty party, Settlement? excludedTarget,
            float attackerStrength, float minimumTargetRatio, float minimumRoamingHostilesRatio,
            IReadOnlyList<SpawnSettlementType> targetTypes)
        {
            return _getCandidates(party)
                .Where(candidate => candidate.IsActive && candidate.IsHostile &&
                                    candidate.HasCompatibleSiege && targetTypes.Contains(candidate.Type) &&
                                    candidate.Target != excludedTarget &&
                                    OffensiveStrengthRules.MeetsStrengthRatio(attackerStrength,
                                        candidate.DefenderStrength, minimumTargetRatio))
                .Where(candidate => _siegeTargetPolicy.IsCandidateAllowed(party, candidate))
                .Where(candidate => OffensiveStrengthRules.MeetsStrengthRatio(attackerStrength,
                    GetHostileLordStrengthNearTarget(party, candidate.Target),
                    minimumRoamingHostilesRatio))
                .OrderBy(candidate => candidate.Distance)
                .Select(candidate => candidate.Target)
                .FirstOrDefault();
        }

        public float GetTargetStrength(MobileParty party, Settlement target) =>
            GetDefenderStrength(party, target);

        public float GetActiveSiegeDefenderStrength(MobileParty party, Settlement target)
        {
            return GetActiveSiegeDefenderStrength(party, GetGarrisonStrength(target));
        }

        public float GetHostileLordStrengthNearTarget(MobileParty party, Settlement target)
        {
            // Reconnaissance uses the party's established visibility radius, centred on the land gate
            // it will use to approach this target rather than on the invasion party's current position.
            return OffensiveStrengthRules.SumVisibleHostileLordStrength(
                _getTargetRoamingParties(party, target), GetSeeingRange(party));
        }

        public IFaction GetTargetMapFaction(Settlement target) => target.MapFaction;

        public bool IsMatchingSiegeActive(MobileParty party, Settlement target)
        {
            return IsMatchingCamp(party, target);
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
            if (!target.IsActive || (!target.IsTown && !target.IsCastle) ||
                !party.MapFaction.IsAtWarWith(target.MapFaction) ||
                !IsIntendedTargetFaction(target.MapFaction, intendedTargetFaction) ||
                (target.SiegeEvent is not null && !matchingOperationActive))
            {
                return false;
            }

            return _siegeTargetPolicy.IsExistingTargetAllowed(
                party, target, IsMatchingAssaultActive(party, target));
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

        public AIBehaviorData? AbandonSiegeAndCreateRetreatBehavior(MobileParty party,
            Settlement target)
        {
            party.BesiegerCamp = null;
            MobileParty? threat = MobileParty.All
                .Where(candidate => candidate != party && candidate.IsActive &&
                                    candidate.CurrentSettlement is null && candidate.MapEvent is null &&
                                    candidate.MapFaction.IsAtWarWith(party.MapFaction) &&
                                    !candidate.IsFleeing() &&
                                    OffensiveStrengthRules.IsWithinSeeingRange(
                                        party.Position.Distance(candidate.Position), GetSeeingRange(party)))
                .OrderBy(candidate => party.Position.Distance(candidate.Position))
                .FirstOrDefault();
            if (threat is not null)
            {
                return new AIBehaviorData(threat, AiBehavior.FleeToPoint,
                    party.NavigationCapability, false, false, false);
            }

            Settlement? fallback = FindClosestNonHostileSettlement(party);
            return fallback is null
                ? null
                : new AIBehaviorData(fallback, AiBehavior.GoToSettlement,
                    party.NavigationCapability, false, false, false);
        }

        internal static bool IsIntendedTargetFaction(IFaction currentFaction,
            IFaction? intendedTargetFaction)
        {
            return currentFaction == intendedTargetFaction;
        }

        private static IEnumerable<SiegeTargetCandidate> ObserveCandidates(MobileParty party)
        {
            return Settlement.All.Where(s => s.IsTown || s.IsCastle).Select(settlement =>
                new SiegeTargetCandidate(settlement, settlement.IsActive,
                    party.MapFaction.IsAtWarWith(settlement.MapFaction),
                    settlement.IsTown ? SpawnSettlementType.Town : SpawnSettlementType.Castle,
                    settlement.SiegeEvent is null || IsMatchingCamp(party, settlement),
                    party.Position.Distance(settlement.GatePosition), GetDefenderStrength(party, settlement),
                    settlement.StringId, settlement.HasPort));
        }

        private static IEnumerable<RoamingPartyStrength> ObserveRoamingPartiesNearTarget(
            MobileParty attacker, Settlement target)
        {
            return MobileParty.All
                .Where(p => p.CurrentSettlement is null && p.MapEvent is null)
                .Select(p => new RoamingPartyStrength(p == attacker, p.IsActive,
                    p.MapFaction.IsAtWarWith(attacker.MapFaction), p.Army,
                    target.GatePosition.Distance(p.Position), p.Party.CalculateCurrentStrength(),
                    p.IsLordParty, p.IsFleeing()));
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
                    p.IsGarrison, p.IsMilitia));
            return CalculateDefenderStrength(settlement.Militia, garrisonStrength, occupants);
        }

        private static float GetGarrisonStrength(Settlement settlement)
        {
            return settlement.Town.GarrisonParty?.Party.CalculateCurrentStrength() ?? 0f;
        }

        internal static float CalculateDefenderStrength(float militiaStrength, float garrisonStrength,
            IEnumerable<SettlementDefenderPartyStrength> occupants)
        {
            return militiaStrength + garrisonStrength + occupants
                .Where(p => p.IsActive && p.IsHostileToAttacker && !p.IsVillager && !p.IsCaravan &&
                            !p.IsGarrison && !p.IsMilitia)
                .Sum(p => p.Strength);
        }

        private static bool HasReachableLandGate(MobileParty party, Settlement settlement)
        {
            if (party.IsCurrentlyAtSea)
            {
                return false;
            }

            PathFaceRecord fromFace = party.CurrentNavigationFace;
            PathFaceRecord toFace = settlement.GatePosition.Face;
            if (!settlement.GatePosition.IsOnLand || fromFace.FaceIndex < 0 || toFace.FaceIndex < 0)
            {
                return false;
            }
            int[] invalidTerrainTypes = Campaign.Current.Models.PartyNavigationModel
                .GetInvalidTerrainTypesForNavigationType(party.NavigationCapability);
            bool hasPath = Campaign.Current.MapSceneWrapper.GetPathDistanceBetweenAIFaces(fromFace, toFace,
                party.Position.ToVec2(), settlement.GatePosition.ToVec2(), 0.3f, UnboundedLandPathDistance,
                out float pathDistance, invalidTerrainTypes,
                Campaign.Current.Models.MapDistanceModel.RegionSwitchCostFromLandToSea,
                Campaign.Current.Models.MapDistanceModel.RegionSwitchCostFromSeaToLand);
            return hasPath && !float.IsNaN(pathDistance) && !float.IsInfinity(pathDistance) &&
                pathDistance >= 0f;
        }
    }
}
