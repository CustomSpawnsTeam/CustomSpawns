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

namespace CustomSpawns.AI.Adapters
{
    public sealed class BannerlordBesiegeClosestSettlementAdapter
        : BannerlordSettlementOffensiveAdapter, ISiegeOperations
    {
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

        public BannerlordBesiegeClosestSettlementAdapter(DynamicSpawnData dynamicSpawnData,
            ISiegeTargetPolicy siegeTargetPolicy) : this(
            observer => observer.SeeingRange, ObserveRoamingParties, ObserveCandidates,
            dynamicSpawnData.FindAccompanyingParties, siegeTargetPolicy,
            ObserveRoamingPartiesNearTarget)
        {
        }

        public BannerlordBesiegeClosestSettlementAdapter(Func<MobileParty, float> getSeeingRange,
            Func<MobileParty, IEnumerable<RoamingPartyStrength>> getRoamingParties,
            Func<MobileParty, IEnumerable<SiegeTargetCandidate>> getCandidates,
            Func<MobileParty, IEnumerable<MobileParty>> getAccompanyingParties)
            : this(getSeeingRange, getRoamingParties, getCandidates, getAccompanyingParties,
                new NoRestrictionSiegeTargetPolicy(), (_, _) => Enumerable.Empty<RoamingPartyStrength>())
        {
        }

        public BannerlordBesiegeClosestSettlementAdapter(Func<MobileParty, float> getSeeingRange,
            Func<MobileParty, IEnumerable<RoamingPartyStrength>> getRoamingParties,
            Func<MobileParty, IEnumerable<SiegeTargetCandidate>> getCandidates,
            Func<MobileParty, IEnumerable<MobileParty>> getAccompanyingParties,
            Func<MobileParty, Settlement, IEnumerable<RoamingPartyStrength>> getTargetRoamingParties)
            : this(getSeeingRange, getRoamingParties, getCandidates, getAccompanyingParties,
                new NoRestrictionSiegeTargetPolicy(), getTargetRoamingParties)
        {
        }

        public BannerlordBesiegeClosestSettlementAdapter(Func<MobileParty, float> getSeeingRange,
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
            Func<SiegeTargetCandidate, float> targetDistance = _siegeTargetPolicy.GetTargetDistance(party);
            // A party that masses on its objective commits before it is strong enough to take it, so the
            // garrison is not weighed here; it is weighed again at the gate, over the force that arrived.
            bool gathers = _siegeTargetPolicy.GathersBeforeAssault(party);
            return _getCandidates(party)
                .Where(candidate => candidate.IsActive && candidate.IsHostile &&
                                    (candidate.HasCompatibleSiege ||
                                     _siegeTargetPolicy.IsJoinableSiege(party, candidate.Target)) &&
                                    targetTypes.Contains(candidate.Type) &&
                                    candidate.Target != excludedTarget &&
                                    (gathers || OffensiveStrengthRules.MeetsStrengthRatio(attackerStrength,
                                        candidate.DefenderStrength, minimumTargetRatio)))
                .Where(candidate => _siegeTargetPolicy.IsCandidateAllowed(party, candidate))
                // Nor are the lords around a candidate weighed for a party that masses: the first party to
                // commit is its whole force, so a ratio asked here would reject the objective its siblings
                // were going to join and the force could never form. Danger is still asked every tick
                // afterwards, where it holds the gathering back rather than taking its objective away.
                .Where(candidate => gathers || OffensiveStrengthRules.MeetsStrengthRatio(attackerStrength,
                    GetHostileLordStrengthNearTarget(party, candidate.Target),
                    minimumRoamingHostilesRatio))
                .OrderBy(targetDistance)
                .Select(candidate => candidate.Target)
                .FirstOrDefault();
        }

        /**
         * A grouped attacking force is sized up the same way a garrison is: as one combined force,
         * counting what all of its parties can bring to the walls rather than what the one deciding
         * carries. Escorts belong to that force themselves, so every party counts its own strength
         * once. A party attacking on its own is assessed as before.
         * The pooled force and the party's own assessment overlap, since an army member or an escort
         * can be a member of that force too, so the two cannot be added without counting parties
         * twice. The greater of them is taken instead: the party's own assessment stays a floor, so a
         * force gaining a member is never assessed as weaker than it was on its own, and the force
         * that is actually present is never under-counted.
         */
        public override float GetAttackerStrength(MobileParty party)
        {
            return Math.Max(base.GetAttackerStrength(party),
                GetAttackingForceWithEscorts(party)
                    .Sum(member => member.Party.CalculateCurrentStrength()));
        }

        /**
         * Who fights for an objective: every party of the force and the escorts travelling with each of
         * them, the deciding party's own included. An escort never records an objective of its own, since
         * the siege behaviour never runs for one, so pooling by recorded objective alone would lose them.
         * The deciding party's escorts cannot be left to its own assessment instead: that assessment is
         * taken as a floor and not added, so leaving them out of the force would drop them from any force
         * that has siblings in it.
         */
        private IEnumerable<MobileParty> GetAttackingForceWithEscorts(MobileParty party)
        {
            return _siegeTargetPolicy.GetAttackingForce(party)
                .SelectMany(member => GetAccompanyingParties(member).Prepend(member))
                .Distinct();
        }

        // What the party brings by itself: its own troops and whatever travels with it, without the
        // siblings that merely share its objective. What is with it, rather than what is committed to it.
        public float GetOwnAttackerStrength(MobileParty party) => base.GetAttackerStrength(party);

        public bool GathersBeforeAssault(MobileParty party) =>
            _siegeTargetPolicy.GathersBeforeAssault(party);

        public bool IsSupportableSiegeActive(MobileParty party, Settlement target) =>
            _siegeTargetPolicy.IsJoinableSiege(party, target);

        // What the force that has actually turned up at the town can bring to the walls, measured from the
        // land gate: one party still marching does not count, nor does one resting in a settlement, nor
        // does one already pinned in a battle of its own -- it cannot hold the walls against anybody else,
        // and the party pinning it is left off the defending side too, since the roaming scan skips
        // parties that are in a battle. Counting one side of that fight and not the other is what makes a
        // camp sit still while a relief force eats its siblings. A party running from a fight it has just
        // broken off is left out for the same reason it is left off the defending side: it brings nothing
        // to the walls, whatever it is still worth on paper.
        // Nothing the deciding party is worth on paper goes in: an army it leads is counted through the
        // force, by whichever of its members are standing here.
        public float GetArrivedAttackerStrength(MobileParty party, Settlement target)
        {
            return OffensiveStrengthRules.CalculateArrivedAttackerStrength(
                GetAttackingForceWithEscorts(party)
                    .Where(member => member.CurrentSettlement is null && member.MapEvent is null &&
                                     !member.IsFleeing())
                    .Select(member => new AttackingPartyStrength(
                        target.GatePosition.Distance(member.Position), GetSeeingRange(member),
                        member.Party.CalculateCurrentStrength())));
        }

        public float GetTargetStrength(MobileParty party, Settlement target) =>
            GetDefenderStrength(party, target);

        // What stands against a camp already at the town: the same town figure the launch gate weighed
        // before committing, so the two gates cannot disagree about the place, plus the relief force near
        // it. The scan is centred on the land gate, exactly as the arrived force on the other side of the
        // ratio is, so a party counts as relief when it is near what it would relieve rather than near
        // whichever besieger happens to be deciding.
        public float GetActiveSiegeDefenderStrength(MobileParty party, Settlement target)
        {
            return OffensiveStrengthRules.CalculateActiveSiegeDefenderStrength(
                GetDefenderStrength(party, target), _getTargetRoamingParties(party, target),
                GetSeeingRange(party));
        }

        public float GetHostileLordStrengthNearTarget(MobileParty party, Settlement target)
        {
            // Reconnaissance uses the party's established visibility radius, centred on the land gate
            // it will use to approach this target rather than on the party's current position.
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

        // A fight at the town engages part of the force and leaves the rest of it counting only itself: the
        // ones in the battle are left out of the arrived figure while the town's garrison and militia stay
        // on the other side of the sum whole. So the force is asked, not the deciding party alone,
        // otherwise being attacked at the camp is what splits it. It has to be a fight at the town: a party
        // intercepted a province away is in a battle that has nothing to do with these walls, and letting
        // that release the hold would order an assault the moment the force is jumped in transit. The
        // deciding party is in the force, so it is judged on where its fight is like everybody else.
        public bool IsForcePinnedInBattle(MobileParty party, Settlement target)
        {
            return GetAttackingForceWithEscorts(party).Any(member => member.MapEvent is not null &&
                       OffensiveStrengthRules.IsWithinSeeingRange(
                           target.GatePosition.Distance(member.Position), GetSeeingRange(member)));
        }

        public bool IsCurrentTargetValid(MobileParty party, Settlement target,
            bool matchingOperationActive, IFaction? intendedTargetFaction)
        {
            if (!target.IsActive || (!target.IsTown && !target.IsCastle) ||
                !party.MapFaction.IsAtWarWith(target.MapFaction) ||
                !IsIntendedTargetFaction(target.MapFaction, intendedTargetFaction) ||
                (target.SiegeEvent is not null && !matchingOperationActive &&
                 !_siegeTargetPolicy.IsJoinableSiege(party, target)))
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

        public AIBehaviorData CreateSiegeBehavior(MobileParty party, Settlement target)
        {
            return new AIBehaviorData(target, AiBehavior.BesiegeSettlement,
                party.NavigationCapability, false, false, false);
        }

        public AIBehaviorData? AbandonSiegeAndCreateRetreatBehavior(MobileParty party,
            Settlement target)
        {
            var camp = party.BesiegerCamp;
            if (camp is not null)
            {
                foreach (MobileParty accompanyingParty in GetAccompanyingParties(party)
                             .Where(candidate => candidate.BesiegerCamp == camp).ToList())
                    accompanyingParty.BesiegerCamp = null;
                party.BesiegerCamp = null;
            }

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

        public static bool IsIntendedTargetFaction(IFaction currentFaction,
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
            // Both readers of this sweep want live hostiles near the town and nobody else, and pricing a
            // party is the expensive part of describing one, so the ones neither can use are dropped first.
            return MobileParty.All
                .Where(p => p.CurrentSettlement is null && p.MapEvent is null && p.IsActive &&
                            p.MapFaction.IsAtWarWith(attacker.MapFaction))
                .Select(p => new RoamingPartyStrength(p == attacker, p.IsActive,
                    p.MapFaction.IsAtWarWith(attacker.MapFaction),
                    target.GatePosition.Distance(p.Position), p.Party.CalculateCurrentStrength(),
                    p.IsLordParty, p.IsFleeing(), p.IsVillager || p.IsCaravan));
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

        public static float CalculateDefenderStrength(float militiaStrength, float garrisonStrength,
            IEnumerable<SettlementDefenderPartyStrength> occupants)
        {
            return militiaStrength + garrisonStrength + occupants
                .Where(p => p.IsActive && p.IsHostileToAttacker && !p.IsVillager && !p.IsCaravan &&
                            !p.IsGarrison && !p.IsMilitia)
                .Sum(p => p.Strength);
        }
    }
}
