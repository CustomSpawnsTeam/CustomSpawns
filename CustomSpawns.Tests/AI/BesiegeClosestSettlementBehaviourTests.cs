using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using CustomSpawns.AI.Adapters;
using CustomSpawns.AI.Behaviours;
using CustomSpawns.AI.Model;
using CustomSpawns.AI.Offensives;
using CustomSpawns.Data.Adapter;
using CustomSpawns.Data.Dto;
using CustomSpawns.Invasions.Reinforcements.Adapters;
using CustomSpawns.Spawn;
using NUnit.Framework;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace CustomSpawns.Tests.AI
{
    public class BesiegeClosestSettlementBehaviourTests
    {
        private PartyStateStore<SettlementOffensiveBehaviourState> _states = null!;

        [Test]
        public void Provider_HandlesTypedSiegeConfiguration()
        {
            Assert.That(new BesiegeClosestSettlementBehaviour(
                    new BannerlordBesiegeClosestSettlementAdapter(DummyDynamicSpawnData()),
                    (minimum, _) => minimum).ConfigurationType,
                Is.EqualTo(typeof(BesiegeClosestSettlementData)));
        }

        [Test]
        public void ConcreteAdapter_FiltersTownCastleAndIncompatibleSiegesThenChoosesClosestQualified()
        {
            MobileParty party = DummyParty();
            Settlement village = DummySettlement();
            Settlement weakTown = DummySettlement();
            Settlement incompatibleCastle = DummySettlement();
            Settlement qualifiedCastle = DummySettlement();
            var candidates = new[]
            {
                Candidate(village, SpawnSettlementType.Village, 1f, 1f),
                Candidate(weakTown, SpawnSettlementType.Town, 2f, 200f),
                Candidate(incompatibleCastle, SpawnSettlementType.Castle, 3f, 10f, false),
                Candidate(qualifiedCastle, SpawnSettlementType.Castle, 4f, 50f)
            };
            var operations = new BannerlordBesiegeClosestSettlementAdapter(_ => 5f, _ => new RoamingPartyStrength[0],
                _ => candidates, _ => new MobileParty[0]);

            Settlement? result = operations.FindClosestSiegeTarget(party, 100f, 1.5f,
                new[] { SpawnSettlementType.Town, SpawnSettlementType.Castle });

            Assert.That(result, Is.SameAs(qualifiedCastle));
        }

        [Test]
        public void ConcreteAdapter_ZeroDefendersAndConfiguredTownOnlyAreHandledSafely()
        {
            MobileParty party = DummyParty();
            Settlement castle = DummySettlement();
            Settlement town = DummySettlement();
            var operations = new BannerlordBesiegeClosestSettlementAdapter(_ => 5f,
                _ => new RoamingPartyStrength[0], _ => new[]
                {
                    Candidate(castle, SpawnSettlementType.Castle, 1f, 0f),
                    Candidate(town, SpawnSettlementType.Town, 2f, 0f)
                }, _ => new MobileParty[0]);

            Settlement? result = operations.FindClosestSiegeTarget(party, 0f, float.MaxValue,
                new[] { SpawnSettlementType.Town });

            Assert.That(result, Is.SameAs(town));
        }

        [Test]
        public void ConcreteAdapter_AlternativeTargetExcludesTheHeldTarget()
        {
            MobileParty party = DummyParty();
            Settlement heldTarget = DummySettlement();
            Settlement alternative = DummySettlement();
            var operations = new BannerlordBesiegeClosestSettlementAdapter(_ => 5f,
                _ => new RoamingPartyStrength[0], _ => new[]
                {
                    Candidate(heldTarget, SpawnSettlementType.Town, 1f, 10f),
                    Candidate(alternative, SpawnSettlementType.Castle, 2f, 10f)
                }, _ => new MobileParty[0]);

            Settlement? result = operations.FindAlternativeSiegeTarget(party, heldTarget, 100f, 1f,
                new[] { SpawnSettlementType.Town, SpawnSettlementType.Castle });

            Assert.That(result, Is.SameAs(alternative));
        }

        [Test]
        public void ConcreteAdapter_LordNearTargetButAwayFromAttackerRejectsThatTarget()
        {
            MobileParty party = DummyParty();
            Settlement threatened = DummySettlement();
            Settlement safe = DummySettlement();
            var operations = new BannerlordBesiegeClosestSettlementAdapter(_ => 5f,
                _ => new RoamingPartyStrength[0], _ => new[]
                {
                    Candidate(threatened, SpawnSettlementType.Town, 1f, 10f),
                    Candidate(safe, SpawnSettlementType.Town, 2f, 10f)
                }, _ => new MobileParty[0], (_, target) => target == threatened
                    ? new[] { LordThreat(5f, 200f) }
                    : new RoamingPartyStrength[0]);

            Settlement? result = operations.FindClosestSiegeTarget(party, 100f, 1f,
                new[] { SpawnSettlementType.Town });

            Assert.That(result, Is.SameAs(safe));
        }

        [Test]
        public void ConcreteAdapter_LordNearAttackerButAwayFromTargetDoesNotRejectTarget()
        {
            MobileParty party = DummyParty();
            Settlement target = DummySettlement();
            var operations = new BannerlordBesiegeClosestSettlementAdapter(_ => 5f,
                _ => new[] { LordThreat(1f, 200f) }, _ => new[]
                {
                    Candidate(target, SpawnSettlementType.Town, 1f, 10f)
                }, _ => new MobileParty[0], (_, _) => new RoamingPartyStrength[0]);

            Settlement? result = operations.FindClosestSiegeTarget(party, 100f, 1f,
                new[] { SpawnSettlementType.Town });

            Assert.That(result, Is.SameAs(target));
        }

        [Test]
        public void ConcreteAdapter_AlternativeTargetChoosesSafeCandidateOverCloserThreatenedOne()
        {
            MobileParty party = DummyParty();
            Settlement heldTarget = DummySettlement();
            Settlement threatened = DummySettlement();
            Settlement safe = DummySettlement();
            var operations = new BannerlordBesiegeClosestSettlementAdapter(_ => 5f,
                _ => new RoamingPartyStrength[0], _ => new[]
                {
                    Candidate(heldTarget, SpawnSettlementType.Town, 1f, 10f),
                    Candidate(threatened, SpawnSettlementType.Town, 2f, 10f),
                    Candidate(safe, SpawnSettlementType.Town, 3f, 10f)
                }, _ => new MobileParty[0], (_, target) => target == threatened
                    ? new[] { LordThreat(5f, 200f) }
                    : new RoamingPartyStrength[0]);

            Settlement? result = operations.FindAlternativeSiegeTarget(party, heldTarget, 100f, 1f,
                new[] { SpawnSettlementType.Town });

            Assert.That(result, Is.SameAs(safe));
        }

        [Test]
        public void ConcreteAdapter_BeforeFirstCoastalConquestPrefersReachableCoastalTown()
        {
            MobileParty party = DummyParty();
            Settlement inlandCastle = DummySettlement();
            Settlement inlandTown = DummySettlement();
            Settlement coastalTown = DummySettlement();
            var operations = new BannerlordBesiegeClosestSettlementAdapter(_ => 5f,
                _ => new RoamingPartyStrength[0], _ => new[]
                {
                    Candidate(inlandCastle, SpawnSettlementType.Castle, 1f, 10f, true, "inland-castle"),
                    Candidate(inlandTown, SpawnSettlementType.Town, 2f, 10f, true, "inland-town"),
                    Candidate(coastalTown, SpawnSettlementType.Town, 3f, 10f, true, "coastal-town")
                }, _ => new MobileParty[0], new InvasionCoastalSiegeTargetPolicy(
                    Landing(_ => settlement => settlement == coastalTown, party),
                    MapPaths(settlement => settlement == coastalTown)),
                (_, _) => new RoamingPartyStrength[0]);

            Settlement? result = operations.FindClosestSiegeTarget(party, 100f, 1.5f,
                new[] { SpawnSettlementType.Town, SpawnSettlementType.Castle });

            Assert.That(result, Is.SameAs(coastalTown));
        }

        [Test]
        public void ConcreteAdapter_BeforeFirstCoastalConquestHasNoInlandFallbackWhenLandGateFails()
        {
            MobileParty party = DummyParty();
            Settlement inlandTown = DummySettlement();
            Settlement coastalTown = DummySettlement();
            var operations = new BannerlordBesiegeClosestSettlementAdapter(_ => 5f,
                _ => new RoamingPartyStrength[0], _ => new[]
                {
                    Candidate(inlandTown, SpawnSettlementType.Town, 1f, 10f, true, "inland-town"),
                    Candidate(coastalTown, SpawnSettlementType.Town, 2f, 10f, true, "coastal-town")
                }, _ => new MobileParty[0], new InvasionCoastalSiegeTargetPolicy(
                    Landing(_ => settlement => settlement == coastalTown, party),
                    MapPaths(_ => false)),
                (_, _) => new RoamingPartyStrength[0]);

            Settlement? result = operations.FindClosestSiegeTarget(party, 100f, 1.5f,
                new[] { SpawnSettlementType.Town, SpawnSettlementType.Castle });

            Assert.That(result, Is.Null);
        }

        // A gathering party commits to its objective before it can take it, so the garrison is not
        // weighed when the objective is chosen; a party that does not gather still skips what it cannot take.
        [Test]
        public void ConcreteAdapter_AGatheringPartyCommitsToATownItIsTooWeakToTakeAlone()
        {
            MobileParty party = DummyParty();
            Settlement strongTown = DummySettlement();
            Settlement weakTown = DummySettlement();
            var candidates = new[]
            {
                Candidate(strongTown, SpawnSettlementType.Town, 1f, 200f, true, "coastal-town"),
                Candidate(weakTown, SpawnSettlementType.Town, 4f, 10f, true, "coastal-town")
            };

            Settlement? gathering = new BannerlordBesiegeClosestSettlementAdapter(_ => 5f,
                    _ => new RoamingPartyStrength[0], _ => candidates, _ => new MobileParty[0],
                    new InvasionCoastalSiegeTargetPolicy(Landing(_ => _ => true, party),
                        MapPaths(_ => true)), (_, _) => new RoamingPartyStrength[0])
                .FindClosestSiegeTarget(party, 100f, 1.5f, new[] { SpawnSettlementType.Town });
            Settlement? alone = new BannerlordBesiegeClosestSettlementAdapter(_ => 5f,
                    _ => new RoamingPartyStrength[0], _ => candidates, _ => new MobileParty[0],
                    new InvasionCoastalSiegeTargetPolicy(NoLiveInvasion(party), MapPaths(_ => true)),
                    (_, _) => new RoamingPartyStrength[0])
                .FindClosestSiegeTarget(party, 100f, 1.5f, new[] { SpawnSettlementType.Town });

            Assert.That(gathering, Is.SameAs(strongTown));
            Assert.That(alone, Is.SameAs(weakTown));
        }

        [Test]
        public void ConcreteAdapter_CoastalPolicyKeepsExistingEligibilityChecks()
        {
            MobileParty party = DummyParty();
            Settlement neutralTown = DummySettlement();
            Settlement strongTown = DummySettlement();
            Settlement incompatibleTown = DummySettlement();
            Settlement viableTown = DummySettlement();
            var operations = new BannerlordBesiegeClosestSettlementAdapter(_ => 5f,
                _ => new RoamingPartyStrength[0], _ => new[]
                {
                    new SiegeTargetCandidate(neutralTown, true, false, SpawnSettlementType.Town, true,
                        1f, 10f, "coastal-town", true),
                    Candidate(strongTown, SpawnSettlementType.Town, 2f, 200f, true, "coastal-town"),
                    Candidate(incompatibleTown, SpawnSettlementType.Town, 3f, 10f, false, "coastal-town"),
                    Candidate(viableTown, SpawnSettlementType.Town, 4f, 10f, true, "coastal-town")
                }, _ => new MobileParty[0], new InvasionCoastalSiegeTargetPolicy(
                    NoLiveInvasion(party), MapPaths(_ => true)),
                (_, _) => new RoamingPartyStrength[0]);

            Settlement? result = operations.FindClosestSiegeTarget(party, 100f, 1.5f,
                new[] { SpawnSettlementType.Town, SpawnSettlementType.Castle });

            Assert.That(result, Is.SameAs(viableTown));
        }

        [Test]
        public void ConcreteAdapter_AfterFirstCoastalConquestDoesNotApplyLandGate()
        {
            MobileParty party = DummyParty();
            Settlement inlandCastle = DummySettlement();
            var operations = new BannerlordBesiegeClosestSettlementAdapter(_ => 5f,
                _ => new RoamingPartyStrength[0], _ => new[]
                {
                    Candidate(inlandCastle, SpawnSettlementType.Castle, 1f, 10f, true, "inland-castle")
                }, _ => new MobileParty[0], new InvasionCoastalSiegeTargetPolicy(
                    Landing(_ => null, party), MapPaths(_ => false)),
                (_, _) => new RoamingPartyStrength[0]);

            Settlement? result = operations.FindClosestSiegeTarget(party, 100f, 1.5f,
                new[] { SpawnSettlementType.Town, SpawnSettlementType.Castle });

            Assert.That(result, Is.SameAs(inlandCastle));
        }

        [Test]
        public void ConcreteAdapter_OrdinaryPartyDoesNotApplyCoastalOrLandGate()
        {
            MobileParty party = DummyParty();
            Settlement nearestCastle = DummySettlement();
            Settlement town = DummySettlement();
            var operations = new BannerlordBesiegeClosestSettlementAdapter(_ => 5f,
                _ => new RoamingPartyStrength[0], _ => new[]
                {
                    Candidate(nearestCastle, SpawnSettlementType.Castle, 1f, 10f),
                    Candidate(town, SpawnSettlementType.Town, 2f, 10f)
                }, _ => new MobileParty[0], new InvasionCoastalSiegeTargetPolicy(
                    Landing(_ => null, party), MapPaths(_ => false)),
                (_, _) => new RoamingPartyStrength[0]);

            Settlement? result = operations.FindClosestSiegeTarget(party, 100f, 1.5f,
                new[] { SpawnSettlementType.Town, SpawnSettlementType.Castle });

            Assert.That(result, Is.SameAs(nearestCastle));
        }

        [Test]
        public void DefenderStrength_IncludesEligibleNonLordOccupant()
        {
            var occupant = new SettlementDefenderPartyStrength(25f);

            Assert.That(BannerlordBesiegeClosestSettlementAdapter.CalculateDefenderStrength(
                0f, 0f, new[] { occupant }), Is.EqualTo(25f));
        }

        [Test]
        public void DefenderStrength_IncludesAttackerHostileThirdPartyOccupant()
        {
            var occupant = new SettlementDefenderPartyStrength(25f, isHostileToAttacker: true);

            Assert.That(BannerlordBesiegeClosestSettlementAdapter.CalculateDefenderStrength(
                0f, 0f, new[] { occupant }), Is.EqualTo(25f));
        }

        [Test]
        public void DefenderStrength_ExcludesVillagerCaravanInactiveAndNonHostileOccupants()
        {
            var occupants = new[]
            {
                new SettlementDefenderPartyStrength(10f, isVillager: true),
                new SettlementDefenderPartyStrength(20f, isCaravan: true),
                new SettlementDefenderPartyStrength(30f, isActive: false),
                new SettlementDefenderPartyStrength(40f, isHostileToAttacker: false)
            };

            Assert.That(BannerlordBesiegeClosestSettlementAdapter.CalculateDefenderStrength(
                0f, 0f, occupants), Is.Zero);
        }

        [Test]
        public void DefenderStrength_CountsGarrisonAndMilitiaExactlyOnce()
        {
            var occupants = new[]
            {
                new SettlementDefenderPartyStrength(30f, isGarrison: true),
                new SettlementDefenderPartyStrength(20f, isMilitia: true),
                new SettlementDefenderPartyStrength(10f)
            };

            Assert.That(BannerlordBesiegeClosestSettlementAdapter.CalculateDefenderStrength(
                20f, 30f, occupants), Is.EqualTo(60f));
        }

        [Test]
        public void IntendedOwnership_RejectsThirdPartyCapture()
        {
            IFaction intendedDefender = DummyKingdom();
            IFaction thirdParty = DummyKingdom();

            Assert.That(BannerlordBesiegeClosestSettlementAdapter.IsIntendedTargetFaction(
                intendedDefender, intendedDefender), Is.True);
            Assert.That(BannerlordBesiegeClosestSettlementAdapter.IsIntendedTargetFaction(
                thirdParty, intendedDefender), Is.False);
        }

        [Test]
        public void ObserverVisibility_UnsafeReconPatrolsThenSafeDirectSkipBesieges()
        {
            MobileParty party = DummyParty();
            Settlement target = DummySettlement();
            Settlement patrol = DummySettlement();
            var operations = new OperationsStub
            {
                SiegeTarget = target,
                PatrolTarget = patrol,
                HostileLordStrengthNearTarget = 200f
            };
            BesiegeClosestSettlementBehaviour provider = Provider(operations);

            PartyBehaviourResult unsafeResult = Evaluate(provider, party);
            operations.HostileLordStrengthNearTarget = 0f;
            PartyBehaviourResult safeResult = Evaluate(provider, party);

            Assert.That(unsafeResult.AIBehavior?.AiBehavior, Is.EqualTo(AiBehavior.PatrolAroundPoint));
            Assert.That(safeResult.AIBehavior?.AiBehavior, Is.EqualTo(AiBehavior.BesiegeSettlement));
            Assert.That(_states.GetOrAdd(party).Target, Is.SameAs(target));
        }

        [Test]
        public void ConcreteAdapter_UsesEachObserverSeeingRangeAndInclusiveBoundary()
        {
            MobileParty boundaryObserver = DummyParty();
            MobileParty shorterObserver = DummyParty();
            var observations = new[]
            {
                new RoamingPartyStrength(false, true, true, 5f, 20f),
                new RoamingPartyStrength(false, true, true, 5.001f, 100f)
            };
            var operations = new BannerlordBesiegeClosestSettlementAdapter(
                observer => observer == boundaryObserver ? 5f : 4.999f,
                _ => observations, _ => new SiegeTargetCandidate[0], _ => new MobileParty[0]);

            Assert.That(operations.GetVisibleHostileStrength(boundaryObserver), Is.EqualTo(20f));
            Assert.That(operations.GetVisibleHostileStrength(shorterObserver), Is.Zero);
        }

        [Test]
        public void ConcreteAdapter_HostileLordThreatNearTargetExcludesNonLordParties()
        {
            MobileParty observer = DummyParty();
            Settlement target = DummySettlement();
            var observations = new[]
            {
                new RoamingPartyStrength(false, true, true, 5f, 20f, true),
                new RoamingPartyStrength(false, true, true, 5f, 100f)
            };
            var operations = new BannerlordBesiegeClosestSettlementAdapter(_ => 5f,
                _ => new RoamingPartyStrength[0], _ => new SiegeTargetCandidate[0],
                _ => new MobileParty[0], (_, _) => observations);

            Assert.That(operations.GetHostileLordStrengthNearTarget(observer, target), Is.EqualTo(20f));
        }

        [Test]
        public void UnsafeApproach_ReturnsToReconInsteadOfForcingContact()
        {
            MobileParty party = DummyParty();
            Settlement target = DummySettlement();
            Settlement patrol = DummySettlement();
            var operations = new OperationsStub { SiegeTarget = target, PatrolTarget = patrol };
            BesiegeClosestSettlementBehaviour provider = Provider(operations);
            Evaluate(provider, party);
            SettlementOffensiveBehaviourState state = _states.GetOrAdd(party);
            state.Phase = SettlementOffensivePhase.Approach;
            operations.HostileLordStrengthNearTarget = 200f;

            PartyBehaviourResult result = Evaluate(provider, party);

            Assert.That(state.Target, Is.Null);
            Assert.That(state.Phase, Is.EqualTo(SettlementOffensivePhase.Search));
            Assert.That(state.AccumulatedClearReconDays, Is.Zero);
            Assert.That(result.AIBehavior?.AiBehavior, Is.EqualTo(AiBehavior.PatrolAroundPoint));
        }

        [Test]
        public void HostileLordThreat_WithdrawsFromUnsafeReconWhenNoAlternativeExists()
        {
            MobileParty party = DummyParty();
            Settlement target = DummySettlement();
            var operations = new OperationsStub { SiegeTarget = target, PatrolTarget = DummySettlement() };
            BesiegeClosestSettlementBehaviour provider = Provider(operations);
            Evaluate(provider, party);
            SettlementOffensiveBehaviourState state = _states.GetOrAdd(party);
            state.Phase = SettlementOffensivePhase.Recon;
            state.AccumulatedClearReconDays = 1f / 24f;
            operations.HostileLordStrengthNearTarget = 200f;

            Evaluate(provider, party);

            Assert.That(state.Target, Is.Null);
            Assert.That(state.Phase, Is.EqualTo(SettlementOffensivePhase.Search));
            Assert.That(state.AccumulatedClearReconDays, Is.Zero);
        }

        [Test]
        public void HostileLordThreatWithoutSafeAlternative_PatrolsThenRetriesWhenTargetIsSafe()
        {
            MobileParty party = DummyParty();
            Settlement target = DummySettlement();
            var operations = new OperationsStub
            {
                SiegeTarget = target,
                PatrolTarget = DummySettlement()
            };
            BesiegeClosestSettlementBehaviour provider = Provider(operations);
            Evaluate(provider, party);
            SettlementOffensiveBehaviourState state = _states.GetOrAdd(party);
            state.Phase = SettlementOffensivePhase.Recon;
            operations.HostileLordStrengthNearTarget = 200f;

            PartyBehaviourResult unsafeResult = Evaluate(provider, party);
            operations.HostileLordStrengthNearTarget = 0f;
            PartyBehaviourResult retryResult = Evaluate(provider, party);

            Assert.That(unsafeResult.AIBehavior?.AiBehavior, Is.EqualTo(AiBehavior.PatrolAroundPoint));
            Assert.That(state.Target, Is.SameAs(target));
            Assert.That(state.Phase, Is.EqualTo(SettlementOffensivePhase.Approach));
            Assert.That(retryResult.AIBehavior?.AiBehavior, Is.EqualTo(AiBehavior.BesiegeSettlement));
        }

        [Test]
        public void VisibleNonLordThreat_DoesNotResetReconProgress()
        {
            MobileParty party = DummyParty();
            Settlement target = DummySettlement();
            var operations = new OperationsStub
            {
                SiegeTarget = target,
                PatrolTarget = DummySettlement(),
                VisibleHostileStrength = 200f,
                TargetStrength = 100f
            };
            BesiegeClosestSettlementBehaviour provider = Provider(operations);
            Evaluate(provider, party);
            SettlementOffensiveBehaviourState state = _states.GetOrAdd(party);
            state.Phase = SettlementOffensivePhase.Recon;
            state.AccumulatedClearReconDays = 1f / 24f;

            Evaluate(provider, party);

            Assert.That(state.Phase, Is.EqualTo(SettlementOffensivePhase.Recon));
            Assert.That(state.AccumulatedClearReconDays, Is.EqualTo(2f / 24f));
        }

        [Test]
        public void HostileLordThreat_ReselectsAnEligibleAlternativeTarget()
        {
            MobileParty party = DummyParty();
            Settlement target = DummySettlement();
            Settlement alternative = DummySettlement();
            var operations = new OperationsStub
            {
                SiegeTarget = target,
                AlternativeSiegeTarget = alternative,
                PatrolTarget = DummySettlement(),
                HostileLordStrengthNearTarget = 200f
            };
            BesiegeClosestSettlementBehaviour provider = Provider(operations);

            PartyBehaviourResult result = Evaluate(provider, party);

            SettlementOffensiveBehaviourState state = _states.GetOrAdd(party);
            Assert.That(state.Target, Is.SameAs(alternative));
            Assert.That(state.Phase, Is.EqualTo(SettlementOffensivePhase.Recon));
            Assert.That(result.AIBehavior?.AiBehavior, Is.EqualTo(AiBehavior.PatrolAroundPoint));
        }

        [Test]
        public void Reconnaissance_WatchesTheChosenTownInsteadOfMarchingToFriendlyGround()
        {
            MobileParty party = DummyParty();
            Settlement target = DummySettlement();
            Settlement friendlyGround = DummySettlement();
            var operations = new OperationsStub
            {
                SiegeTarget = target,
                PatrolTarget = friendlyGround,
                TargetStrength = 100f
            };
            BesiegeClosestSettlementBehaviour provider = Provider(operations);

            PartyBehaviourResult result = Evaluate(provider, party);

            SettlementOffensiveBehaviourState state = _states.GetOrAdd(party);
            Assert.That(state.Phase, Is.EqualTo(SettlementOffensivePhase.Recon));
            Assert.That(result.AIBehavior?.AiBehavior, Is.EqualTo(AiBehavior.PatrolAroundPoint));
            Assert.That(result.AIBehavior?.Party, Is.SameAs(target));
            Assert.That(result.AIBehavior?.Party, Is.Not.SameAs(friendlyGround));
        }

        [Test]
        public void TooWeakForApproachedTarget_DropsItInsteadOfPressingADoomedSiege()
        {
            MobileParty party = DummyParty();
            Settlement target = DummySettlement();
            Settlement patrol = DummySettlement();
            var operations = new OperationsStub { SiegeTarget = target, PatrolTarget = patrol };
            BesiegeClosestSettlementBehaviour provider = Provider(operations);
            Evaluate(provider, party);
            SettlementOffensiveBehaviourState state = _states.GetOrAdd(party);
            state.Phase = SettlementOffensivePhase.Approach;
            operations.TargetStrength = 200f;
            operations.SiegeTarget = null;

            PartyBehaviourResult result = Evaluate(provider, party);

            Assert.That(state.Target, Is.Null);
            Assert.That(state.Phase, Is.EqualTo(SettlementOffensivePhase.Search));
            Assert.That(result.AIBehavior?.AiBehavior, Is.EqualTo(AiBehavior.PatrolAroundPoint));
        }

        [Test]
        public void TooWeakDuringActiveSiege_AbandonsTheSiegeUnderway()
        {
            MobileParty party = DummyParty();
            Settlement target = DummySettlement();
            Settlement patrol = DummySettlement();
            var operations = new OperationsStub { SiegeTarget = target, PatrolTarget = patrol };
            BesiegeClosestSettlementBehaviour provider = Provider(operations);
            Evaluate(provider, party);
            SettlementOffensiveBehaviourState state = _states.GetOrAdd(party);
            operations.MatchingSiegeActive = true;
            operations.ActiveSiegeDefenderStrength = 200f;
            operations.SiegeTarget = null;

            PartyBehaviourResult result = Evaluate(provider, party);

            Assert.That(state.Target, Is.Null);
            Assert.That(state.Phase, Is.EqualTo(SettlementOffensivePhase.Search));
            Assert.That(result.AIBehavior?.AiBehavior, Is.EqualTo(AiBehavior.PatrolAroundPoint));
        }

        [Test]
        public void StillStrongEnoughForHeldTarget_KeepsBesiegingIt()
        {
            MobileParty party = DummyParty();
            Settlement target = DummySettlement();
            Settlement patrol = DummySettlement();
            var operations = new OperationsStub { SiegeTarget = target, PatrolTarget = patrol };
            BesiegeClosestSettlementBehaviour provider = Provider(operations);
            Evaluate(provider, party);
            SettlementOffensiveBehaviourState state = _states.GetOrAdd(party);
            state.Phase = SettlementOffensivePhase.Approach;
            operations.TargetStrength = 100f;
            operations.SiegeTarget = null;

            PartyBehaviourResult result = Evaluate(provider, party);

            Assert.That(state.Target, Is.SameAs(target));
            Assert.That(result.AIBehavior?.AiBehavior, Is.EqualTo(AiBehavior.BesiegeSettlement));
        }

        [Test]
        public void Gathering_HoldsNearTheTargetWhileTheForceThatHasArrivedIsTooWeak()
        {
            MobileParty party = DummyParty();
            Settlement target = DummySettlement();
            var operations = new OperationsStub
            {
                SiegeTarget = target,
                PatrolTarget = DummySettlement(),
                Gathers = true,
                ArrivedAttackerStrength = 5f
            };
            BesiegeClosestSettlementBehaviour provider = Provider(operations);
            Evaluate(provider, party);
            SettlementOffensiveBehaviourState state = _states.GetOrAdd(party);
            state.Phase = SettlementOffensivePhase.Approach;

            PartyBehaviourResult result = Evaluate(provider, party);

            Assert.That(state.Target, Is.SameAs(target));
            Assert.That(state.Phase, Is.EqualTo(SettlementOffensivePhase.Gather));
            Assert.That(result.AIBehavior?.AiBehavior, Is.EqualTo(AiBehavior.PatrolAroundPoint));
        }

        [Test]
        public void Gathering_AttacksOnceTheForceThatHasArrivedIsStrongEnough()
        {
            MobileParty party = DummyParty();
            Settlement target = DummySettlement();
            var operations = new OperationsStub
            {
                SiegeTarget = target,
                PatrolTarget = DummySettlement(),
                Gathers = true,
                ArrivedAttackerStrength = 5f
            };
            BesiegeClosestSettlementBehaviour provider = Provider(operations);
            Evaluate(provider, party);
            SettlementOffensiveBehaviourState state = _states.GetOrAdd(party);
            state.Phase = SettlementOffensivePhase.Approach;

            PartyBehaviourResult held = Evaluate(provider, party);
            operations.ArrivedAttackerStrength = 100f;
            PartyBehaviourResult launched = Evaluate(provider, party);

            Assert.That(held.AIBehavior?.AiBehavior, Is.EqualTo(AiBehavior.PatrolAroundPoint));
            Assert.That(state.Phase, Is.EqualTo(SettlementOffensivePhase.Approach));
            Assert.That(launched.AIBehavior?.AiBehavior, Is.EqualTo(AiBehavior.BesiegeSettlement));
        }

        [Test]
        public void Gathering_DoesNotTurnBackTheMomentTheArrivedForceDipsUnderTheLine()
        {
            MobileParty party = DummyParty();
            Settlement target = DummySettlement();
            // Marching is what moves the parties the arrived figure is counted from, so a force sitting on
            // the line must not step forward and back for the whole operation window.
            var operations = new OperationsStub
            {
                SiegeTarget = target,
                PatrolTarget = DummySettlement(),
                Gathers = true,
                ArrivedAttackerStrength = 100f
            };
            BesiegeClosestSettlementBehaviour provider = Provider(operations);
            Evaluate(provider, party);
            SettlementOffensiveBehaviourState state = _states.GetOrAdd(party);
            state.Phase = SettlementOffensivePhase.Approach;

            operations.ArrivedAttackerStrength = 9f;
            PartyBehaviourResult stillMarching = Evaluate(provider, party);
            operations.ArrivedAttackerStrength = 7f;
            PartyBehaviourResult turnedBack = Evaluate(provider, party);

            Assert.That(stillMarching.AIBehavior?.AiBehavior, Is.EqualTo(AiBehavior.BesiegeSettlement));
            Assert.That(turnedBack.AIBehavior?.AiBehavior, Is.EqualTo(AiBehavior.PatrolAroundPoint));
            Assert.That(state.Phase, Is.EqualTo(SettlementOffensivePhase.Gather));
        }

        [Test]
        public void Gathering_AsksForTheWholeRequirementOnItsFirstDecisionAfterRecon()
        {
            MobileParty party = DummyParty();
            Settlement target = DummySettlement();
            // A party that has just finished looking has not set off, so the slack a marching force is
            // given must not be handed to it: it has to hold until its force can take the place.
            var operations = new OperationsStub
            {
                SiegeTarget = target,
                PatrolTarget = DummySettlement(),
                Gathers = true,
                ArrivedAttackerStrength = 9f
            };
            BesiegeClosestSettlementBehaviour provider = Provider(operations);

            PartyBehaviourResult result = Evaluate(provider, party);

            Assert.That(_states.GetOrAdd(party).Phase, Is.EqualTo(SettlementOffensivePhase.Gather));
            Assert.That(result.AIBehavior?.Party, Is.SameAs(target));
        }

        [Test]
        public void Gathering_WeighsWhatItBringsAgainstLordsNearTheTargetWhileNothingHasArrived()
        {
            MobileParty party = DummyParty();
            Settlement target = DummySettlement();
            // Nothing has arrived, so reconnaissance can only weigh what the party is bringing. Were it to
            // read the arrived figure alone it would find itself outnumbered by lords it outmatches two to
            // one, and would sit in reconnaissance instead of closing on its objective.
            var operations = new OperationsStub
            {
                SiegeTarget = target,
                PatrolTarget = DummySettlement(),
                Gathers = true,
                AttackerStrength = 100f,
                ArrivedAttackerStrength = 0f,
                HostileLordStrengthNearTarget = 50f,
                TargetStrength = 50f
            };
            BesiegeClosestSettlementBehaviour provider = Provider(operations);

            PartyBehaviourResult result = Evaluate(provider, party);

            Assert.That(_states.GetOrAdd(party).Phase, Is.EqualTo(SettlementOffensivePhase.Gather));
            Assert.That(result.AIBehavior?.Party, Is.SameAs(target));
        }

        [Test]
        public void GatheringOperationWindowExpiry_EntersCooldownAndFallsThrough()
        {
            MobileParty party = DummyParty();
            Settlement target = DummySettlement();
            // A hold nobody joins is ended by the operation window, which is the only thing that ends it.
            var operations = new OperationsStub
            {
                SiegeTarget = target,
                PatrolTarget = DummySettlement(),
                Gathers = true,
                ArrivedAttackerStrength = 5f
            };
            BesiegeClosestSettlementBehaviour provider = Provider(operations);
            Evaluate(provider, party);
            SettlementOffensiveBehaviourState state = _states.GetOrAdd(party);
            state.Phase = SettlementOffensivePhase.Gather;
            state.ElapsedOperationDays = state.OperationWindowDays;

            PartyBehaviourResult result = Evaluate(provider, party);

            Assert.That(result.IsApplicable, Is.False);
            Assert.That(state.Phase, Is.EqualTo(SettlementOffensivePhase.Cooldown));
            Assert.That(state.Target, Is.Null);
            Assert.That(state.CooldownRemainingDays, Is.EqualTo(3f));
        }

        [Test]
        public void Gathering_MarchesToSupportASiegeAlreadyRunningOnItsTarget()
        {
            MobileParty party = DummyParty();
            Settlement target = DummySettlement();
            var operations = new OperationsStub
            {
                SiegeTarget = target,
                PatrolTarget = DummySettlement(),
                Gathers = true,
                ArrivedAttackerStrength = 0f,
                SupportableSiegeActive = true
            };
            BesiegeClosestSettlementBehaviour provider = Provider(operations);
            Evaluate(provider, party);
            _states.GetOrAdd(party).Phase = SettlementOffensivePhase.Approach;

            PartyBehaviourResult result = Evaluate(provider, party);

            Assert.That(result.AIBehavior?.AiBehavior, Is.EqualTo(AiBehavior.BesiegeSettlement));
        }

        /**
         * The lords near the town are the relief force the running siege is fighting, so a sibling that
         * waits in reconnaissance for them to leave leaves the camp to face them without it.
         */
        [Test]
        public void Gathering_JoinsASiegeAlreadyRunningOnItsTargetDespiteLordsNearIt()
        {
            MobileParty party = DummyParty();
            Settlement target = DummySettlement();
            var operations = new OperationsStub
            {
                SiegeTarget = target,
                PatrolTarget = DummySettlement(),
                Gathers = true,
                ArrivedAttackerStrength = 0f,
                HostileLordStrengthNearTarget = 1000f,
                TargetStrength = 200f,
                SupportableSiegeActive = true
            };
            BesiegeClosestSettlementBehaviour provider = Provider(operations);

            PartyBehaviourResult result = Evaluate(provider, party);

            Assert.That(result.AIBehavior?.AiBehavior, Is.EqualTo(AiBehavior.BesiegeSettlement));
        }

        [Test]
        public void Gathering_JoinsAFightItsForceIsAlreadyInAtTheGateDespiteLordsNearIt()
        {
            MobileParty party = DummyParty();
            Settlement target = DummySettlement();
            var operations = new OperationsStub
            {
                SiegeTarget = target,
                PatrolTarget = DummySettlement(),
                Gathers = true,
                ArrivedAttackerStrength = 0f,
                HostileLordStrengthNearTarget = 1000f,
                TargetStrength = 200f,
                ForcePinnedInBattle = true
            };
            BesiegeClosestSettlementBehaviour provider = Provider(operations);

            PartyBehaviourResult result = Evaluate(provider, party);

            Assert.That(result.AIBehavior?.AiBehavior, Is.EqualTo(AiBehavior.BesiegeSettlement));
        }

        [Test]
        public void LoneParty_StillHoldsInReconWhenLordsAreNearASiegeItCouldSupport()
        {
            MobileParty party = DummyParty();
            Settlement target = DummySettlement();
            var operations = new OperationsStub
            {
                SiegeTarget = target,
                PatrolTarget = DummySettlement(),
                HostileLordStrengthNearTarget = 1000f,
                SupportableSiegeActive = true
            };
            BesiegeClosestSettlementBehaviour provider = Provider(operations);

            PartyBehaviourResult result = Evaluate(provider, party);

            Assert.That(result.AIBehavior?.AiBehavior, Is.Not.EqualTo(AiBehavior.BesiegeSettlement));
        }

        [Test]
        public void Gathering_KeepsItsObjectiveWhileStillMarchingPastLordsNearTheTarget()
        {
            MobileParty party = DummyParty();
            Settlement target = DummySettlement();
            // Nothing has arrived yet, which is the normal state of a party that has just committed. Recon
            // must weigh the lords near the target against the force the party is bringing, or no gather
            // ever survives its first tick.
            var operations = new OperationsStub
            {
                SiegeTarget = target,
                PatrolTarget = DummySettlement(),
                Gathers = true,
                ArrivedAttackerStrength = 0f,
                AttackerStrength = 100f,
                HostileLordStrengthNearTarget = 10f,
                TargetStrength = 200f
            };
            BesiegeClosestSettlementBehaviour provider = Provider(operations);
            Evaluate(provider, party);
            SettlementOffensiveBehaviourState state = _states.GetOrAdd(party);
            operations.SiegeTarget = null;

            Evaluate(provider, party);

            Assert.That(state.Target, Is.SameAs(target));
            Assert.That(state.Phase, Is.EqualTo(SettlementOffensivePhase.Recon));
        }

        [Test]
        public void ActiveCamp_WithdrawsWhenOnlyPartiesStillMarchingWouldHoldIt()
        {
            MobileParty party = DummyParty();
            Settlement target = DummySettlement();
            var retreat = new AIBehaviorData(DummyParty(), AiBehavior.FleeToPoint,
                MobileParty.NavigationType.Default, false, false, false);
            // The camp stands at the town, so the relief force is weighed against what is standing there,
            // not against siblings a province away.
            var operations = new OperationsStub
            {
                SiegeTarget = target,
                MatchingSiegeActive = true,
                AttackerStrength = 1000f,
                ArrivedAttackerStrength = 10f,
                ActiveSiegeDefenderStrength = 100f,
                RetreatBehavior = retreat
            };
            BesiegeClosestSettlementBehaviour provider = Provider(operations);
            Evaluate(provider, party);
            SettlementOffensiveBehaviourState state = _states.GetOrAdd(party);
            state.Target = target;

            PartyBehaviourResult result = Evaluate(provider, party);

            Assert.That(result.AIBehavior?.AiBehavior, Is.EqualTo(AiBehavior.FleeToPoint));
            Assert.That(state.Target, Is.Null);
        }

        [Test]
        public void ActiveCamp_KeepsACampFoundedOnTheMarginTheMarchWasLetThroughOn()
        {
            MobileParty party = DummyParty();
            Settlement target = DummySettlement();
            var retreat = new AIBehaviorData(DummyParty(), AiBehavior.FleeToPoint,
                MobileParty.NavigationType.Default, false, false, false);
            // The march that built this camp was let through just under the requirement. Asking the camp
            // for the whole requirement would tear it down the tick after it was built, and the retreat
            // would walk the force away from the town it is trying to mass at.
            var operations = new OperationsStub
            {
                SiegeTarget = target,
                Gathers = true,
                MatchingSiegeActive = true,
                ArrivedAttackerStrength = 8.5f,
                ActiveSiegeDefenderStrength = 10f,
                RetreatBehavior = retreat
            };
            BesiegeClosestSettlementBehaviour provider = Provider(operations);
            Evaluate(provider, party);
            SettlementOffensiveBehaviourState state = _states.GetOrAdd(party);
            state.Target = target;

            PartyBehaviourResult result = Evaluate(provider, party);

            Assert.That(operations.AbandonSiegeCalls, Is.Zero);
            Assert.That(state.Target, Is.SameAs(target));
            Assert.That(result.AIBehavior?.AiBehavior, Is.Not.EqualTo(AiBehavior.FleeToPoint));
        }

        [Test]
        public void Gathering_IsNotTalkedOutOfItsTargetByTheStrengthRatio()
        {
            MobileParty party = DummyParty();
            Settlement target = DummySettlement();
            var operations = new OperationsStub
            {
                SiegeTarget = target,
                PatrolTarget = DummySettlement(),
                Gathers = true,
                ArrivedAttackerStrength = 5f
            };
            BesiegeClosestSettlementBehaviour provider = Provider(operations);
            Evaluate(provider, party);
            SettlementOffensiveBehaviourState state = _states.GetOrAdd(party);
            state.Phase = SettlementOffensivePhase.Approach;
            operations.TargetStrength = 200f;
            operations.SiegeTarget = null;

            Evaluate(provider, party);

            Assert.That(state.Target, Is.SameAs(target));
            Assert.That(state.Phase, Is.EqualTo(SettlementOffensivePhase.Gather));
        }

        [Test]
        public void Gathering_HoldsItsObjectiveInReconInsteadOfLookingForQuieterGround()
        {
            MobileParty party = DummyParty();
            Settlement target = DummySettlement();
            Settlement quieterGround = DummySettlement();
            // The recorded objective is what makes the siblings one force, so a gathering party keeps it
            // while the ground is dangerous. A party attacking alone moves on, which the lord-threat
            // reselection test states.
            var operations = new OperationsStub
            {
                SiegeTarget = target,
                AlternativeSiegeTarget = DummySettlement(),
                PatrolTarget = quieterGround,
                Gathers = true,
                AttackerStrength = 0f,
                ArrivedAttackerStrength = 0f,
                HostileLordStrengthNearTarget = 200f
            };
            BesiegeClosestSettlementBehaviour provider = Provider(operations);

            PartyBehaviourResult result = Evaluate(provider, party);

            SettlementOffensiveBehaviourState state = _states.GetOrAdd(party);
            Assert.That(state.Target, Is.SameAs(target));
            Assert.That(state.Phase, Is.EqualTo(SettlementOffensivePhase.Recon));
            // Holding means watching the town it has chosen, not walking off to the nearest friendly
            // ground: the objective stays recorded and the party stays on it.
            Assert.That(result.AIBehavior?.AiBehavior, Is.EqualTo(AiBehavior.PatrolAroundPoint));
            Assert.That(result.AIBehavior?.Party, Is.SameAs(target));
            Assert.That(result.AIBehavior?.Party, Is.Not.SameAs(quieterGround));
        }

        [Test]
        public void ActiveCampInBattle_StandsItsGroundInsteadOfWithdrawingMidFight()
        {
            MobileParty party = DummyParty();
            Settlement target = DummySettlement();
            var retreat = new AIBehaviorData(DummyParty(), AiBehavior.FleeToPoint,
                MobileParty.NavigationType.Default, false, false, false);
            // The relief force the camp is fighting counts towards the defenders, so the withdrawal test
            // would always fail while the fight is on, over a fight the camp may well be winning.
            var operations = new OperationsStub
            {
                SiegeTarget = target,
                MatchingSiegeActive = true,
                ForcePinnedInBattle = true,
                ArrivedAttackerStrength = 10f,
                ActiveSiegeDefenderStrength = 100f,
                RetreatBehavior = retreat
            };
            BesiegeClosestSettlementBehaviour provider = Provider(operations);
            Evaluate(provider, party);
            SettlementOffensiveBehaviourState state = _states.GetOrAdd(party);
            state.Target = target;

            PartyBehaviourResult result = Evaluate(provider, party);

            Assert.That(state.Target, Is.SameAs(target));
            Assert.That(operations.AbandonSiegeCalls, Is.EqualTo(0));
            Assert.That(result.AIBehavior?.AiBehavior, Is.EqualTo(AiBehavior.BesiegeSettlement));
        }

        [Test]
        public void ActiveCampWithSiblingsInTheFight_HoldsInsteadOfLeavingThemToIt()
        {
            MobileParty party = DummyParty();
            Settlement target = DummySettlement();
            var retreat = new AIBehaviorData(DummyParty(), AiBehavior.FleeToPoint,
                MobileParty.NavigationType.Default, false, false, false);
            // The siblings taken into the fight stop counting towards what holds the camp, while the town
            // they are all besieging counts whole, so the ones left over read themselves as alone against
            // it. Packing up on that reading is what splits the force the moment it is attacked.
            var operations = new OperationsStub
            {
                SiegeTarget = target,
                Gathers = true,
                MatchingSiegeActive = true,
                ForcePinnedInBattle = true,
                ArrivedAttackerStrength = 1f,
                ActiveSiegeDefenderStrength = 100f,
                RetreatBehavior = retreat
            };
            BesiegeClosestSettlementBehaviour provider = Provider(operations);
            Evaluate(provider, party);
            SettlementOffensiveBehaviourState state = _states.GetOrAdd(party);
            state.Target = target;

            PartyBehaviourResult result = Evaluate(provider, party);

            Assert.That(state.Target, Is.SameAs(target));
            Assert.That(operations.AbandonSiegeCalls, Is.EqualTo(0));
            Assert.That(result.AIBehavior?.AiBehavior, Is.Not.EqualTo(AiBehavior.FleeToPoint));
        }

        [Test]
        public void JustCommittedParty_WeighsTheForceItHasJoinedAndNotItselfAlone()
        {
            MobileParty party = DummyParty();
            Settlement target = DummySettlement();
            // Joining an objective is what makes the party part of its force, so the figure taken before
            // the objective was recorded counted the party alone and is too small to clear the target by
            // the margin that sends a force straight at it without scouting first.
            var operations = new OperationsStub
            {
                SiegeTarget = target,
                AlternativeSiegeTarget = DummySettlement(),
                PatrolTarget = DummySettlement(),
                AttackerStrength = 10f,
                AttackerStrengthOnceCommitted = 100f,
                HostileLordStrengthNearTarget = 20f,
                TargetStrength = 50f
            };
            BesiegeClosestSettlementBehaviour provider = Provider(operations);

            PartyBehaviourResult result = Evaluate(provider, party);

            SettlementOffensiveBehaviourState state = _states.GetOrAdd(party);
            Assert.That(state.Target, Is.SameAs(target));
            Assert.That(result.AIBehavior?.AiBehavior, Is.EqualTo(AiBehavior.BesiegeSettlement));
        }

        [Test]
        public void ConcreteAdapter_AddsTheAccompanyingPartiesOfTheAssessedPartyToItsOwnStrength()
        {
            MobileParty party = DummyPartyWithPartyBase();
            MobileParty accompanyingParty = DummyPartyWithPartyBase();
            MobileParty? assessedParty = null;
            var counted = new List<MobileParty>();
            var operations = new BannerlordBesiegeClosestSettlementAdapter(_ => 5f,
                _ => new RoamingPartyStrength[0], _ => new SiegeTargetCandidate[0], observed =>
                {
                    assessedParty = observed;
                    return Counted(accompanyingParty, counted);
                });

            operations.GetAttackerStrength(party);

            Assert.That(assessedParty, Is.SameAs(party));
            // The escorts are asked for twice, once for the party's own assessment and once for the force
            // it belongs to, and which of the two is the greater is what counts. No party is counted twice
            // within either, which is what the set says.
            Assert.That(counted.Distinct(), Is.EqualTo(new[] { accompanyingParty }));
        }

        [Test]
        public void ConcreteAdapter_KeepsAssessingThePartyItselfWhenItIsPartOfAPooledForce()
        {
            MobileParty party = DummyPartyWithPartyBase();
            MobileParty comrade = DummyPartyWithPartyBase();
            MobileParty accompanyingParty = DummyPartyWithPartyBase();
            var counted = new List<MobileParty>();
            var operations = new BannerlordBesiegeClosestSettlementAdapter(_ => 5f,
                _ => new RoamingPartyStrength[0], _ => new SiegeTargetCandidate[0],
                _ => Counted(accompanyingParty, counted),
                new InvasionCoastalSiegeTargetPolicy(Landing(_ => _ => true, party, comrade),
                    MapPaths(_ => true)),
                (_, _) => new RoamingPartyStrength[0]);

            operations.GetAttackerStrength(party);

            Assert.That(counted.Distinct(), Is.EqualTo(new[] { accompanyingParty }));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ActiveCampOrAssault_FreezesClockAndKeepsSiegeUnderway(bool assault)
        {
            MobileParty party = DummyParty();
            Settlement target = DummySettlement();
            var operations = new OperationsStub { SiegeTarget = target };
            BesiegeClosestSettlementBehaviour provider = Provider(operations);
            Evaluate(provider, party);
            SettlementOffensiveBehaviourState state = _states.GetOrAdd(party);
            state.Target = target;
            state.Phase = SettlementOffensivePhase.Approach;
            state.ElapsedOperationDays = 10f;
            operations.MatchingSiegeActive = true;
            operations.MatchingAssaultActive = assault;

            PartyBehaviourResult result = Evaluate(provider, party);

            Assert.That(result.AIBehavior?.AiBehavior, Is.EqualTo(AiBehavior.BesiegeSettlement));
            Assert.That(state.Phase, Is.EqualTo(SettlementOffensivePhase.Active));
            Assert.That(state.ElapsedOperationDays, Is.EqualTo(10f));
        }

        [Test]
        public void ReinforcedActiveSiege_AbandonsTargetAndRetreats()
        {
            MobileParty party = DummyParty();
            Settlement target = DummySettlement();
            var retreat = new AIBehaviorData(DummyParty(), AiBehavior.FleeToPoint,
                MobileParty.NavigationType.Default, false, false, false);
            var operations = new OperationsStub
            {
                SiegeTarget = target,
                MatchingSiegeActive = true,
                // A camp is given the same slack a march is given, so the relief force has to put the
                // camp clearly short rather than a hair short before it packs up.
                ActiveSiegeDefenderStrength = 200f,
                RetreatBehavior = retreat
            };
            BesiegeClosestSettlementBehaviour provider = Provider(operations);
            Evaluate(provider, party);

            PartyBehaviourResult result = Evaluate(provider, party);

            Assert.That(result.AIBehavior?.AiBehavior, Is.EqualTo(AiBehavior.FleeToPoint));
            Assert.That(_states.GetOrAdd(party).Target, Is.Null);
            Assert.That(operations.AbandonSiegeCalls, Is.EqualTo(1));
        }

        [Test]
        public void CampDrivenOff_SitsOutTheCooldownInsteadOfMarchingStraightBack()
        {
            MobileParty party = DummyParty();
            Settlement target = DummySettlement();
            var retreat = new AIBehaviorData(DummyParty(), AiBehavior.FleeToPoint,
                MobileParty.NavigationType.Default, false, false, false);
            // A force that masses weighs no strength when it chooses, so nothing but the cooldown stops it
            // picking the very town that has just thrown it off.
            var operations = new OperationsStub
            {
                SiegeTarget = target,
                PatrolTarget = DummySettlement(),
                Gathers = true,
                MatchingSiegeActive = true,
                ActiveSiegeDefenderStrength = 200f,
                RetreatBehavior = retreat
            };
            BesiegeClosestSettlementBehaviour provider = Provider(operations);
            Evaluate(provider, party);
            Evaluate(provider, party);

            PartyBehaviourResult afterWithdrawing = Evaluate(provider, party);

            SettlementOffensiveBehaviourState state = _states.GetOrAdd(party);
            Assert.That(state.Phase, Is.EqualTo(SettlementOffensivePhase.Cooldown));
            Assert.That(state.Target, Is.Null);
            Assert.That(afterWithdrawing.AIBehavior?.AiBehavior,
                Is.Not.EqualTo(AiBehavior.BesiegeSettlement));
            Assert.That(operations.AbandonSiegeCalls, Is.EqualTo(1));
        }

        [Test]
        public void TargetReinforcedMidMarch_SwitchesToAnotherTownRatherThanBenchingItself()
        {
            MobileParty party = DummyParty();
            Settlement reinforced = DummySettlement();
            Settlement weaker = DummySettlement();
            // Nothing drove this party off anything: it is still marching, and the choice that picked the
            // town weighed the very strength that has now turned against it, so it will not pick it again.
            var operations = new OperationsStub
            {
                SiegeTarget = reinforced,
                PatrolTarget = DummySettlement()
            };
            BesiegeClosestSettlementBehaviour provider = Provider(operations);
            Evaluate(provider, party);
            SettlementOffensiveBehaviourState state = _states.GetOrAdd(party);
            state.Phase = SettlementOffensivePhase.Approach;
            operations.TargetStrength = 200f;
            operations.SiegeTarget = weaker;

            Evaluate(provider, party);

            Assert.That(state.Target, Is.SameAs(weaker));
            Assert.That(state.Phase, Is.Not.EqualTo(SettlementOffensivePhase.Cooldown));
            Assert.That(operations.AbandonSiegeCalls, Is.Zero);
        }

        [Test]
        public void ReliefForceNearTheTown_IsWeighedBeforeTheForceIsLetOffItsHold()
        {
            MobileParty party = DummyParty();
            Settlement target = DummySettlement();
            // The town's own walls are weak enough for this force, but a relief force stands beside it, and
            // that is the sum the camp it would found is judged against. Reading the walls alone would send
            // it in only for the camp gate to throw it straight back out.
            var operations = new OperationsStub
            {
                SiegeTarget = target,
                PatrolTarget = DummySettlement(),
                Gathers = true,
                ArrivedAttackerStrength = 20f,
                TargetStrength = 10f,
                ActiveSiegeDefenderStrength = 50f
            };
            BesiegeClosestSettlementBehaviour provider = Provider(operations);
            Evaluate(provider, party);
            SettlementOffensiveBehaviourState state = _states.GetOrAdd(party);
            state.Phase = SettlementOffensivePhase.Approach;

            PartyBehaviourResult result = Evaluate(provider, party);

            Assert.That(state.Target, Is.SameAs(target));
            Assert.That(state.Phase, Is.EqualTo(SettlementOffensivePhase.Gather));
            Assert.That(result.AIBehavior?.AiBehavior, Is.EqualTo(AiBehavior.PatrolAroundPoint));
        }

        [Test]
        public void SalliedOutOnShortOfTheWalls_AttacksIntoTheFightInsteadOfWaitingItOut()
        {
            MobileParty party = DummyParty();
            Settlement target = DummySettlement();
            // The fight takes its own side out of the arrived figure and leaves the town whole, so the hold
            // can only ever fail while it runs. Asking it anyway leaves the rest of the force milling about
            // short of the walls while the sally eats the parties that are in it.
            var operations = new OperationsStub
            {
                SiegeTarget = target,
                PatrolTarget = DummySettlement(),
                Gathers = true,
                ForcePinnedInBattle = true,
                ArrivedAttackerStrength = 1f,
                ActiveSiegeDefenderStrength = 100f
            };
            BesiegeClosestSettlementBehaviour provider = Provider(operations);
            Evaluate(provider, party);
            SettlementOffensiveBehaviourState state = _states.GetOrAdd(party);
            state.Phase = SettlementOffensivePhase.Approach;

            PartyBehaviourResult result = Evaluate(provider, party);

            Assert.That(state.Phase, Is.EqualTo(SettlementOffensivePhase.Approach));
            Assert.That(result.AIBehavior?.AiBehavior, Is.EqualTo(AiBehavior.BesiegeSettlement));
        }

        [Test]
        public void LoneBesieger_WithdrawsOnTheThresholdItWasNeverGivenSlackOn()
        {
            MobileParty party = DummyParty();
            Settlement target = DummySettlement();
            // The slack belongs to a force that was let through on it. A party that besieges on its own was
            // never held short of anything, so standing at this figure is standing too weak, not standing
            // on a margin it earned.
            var operations = new OperationsStub
            {
                SiegeTarget = target,
                PatrolTarget = DummySettlement(),
                MatchingSiegeActive = true,
                ArrivedAttackerStrength = 8.5f,
                ActiveSiegeDefenderStrength = 10f,
                RetreatBehavior = new AIBehaviorData(DummySettlement(), AiBehavior.FleeToPoint,
                    MobileParty.NavigationType.Default, false, false, false)
            };
            BesiegeClosestSettlementBehaviour provider = Provider(operations);

            Evaluate(provider, party);
            PartyBehaviourResult result = Evaluate(provider, party);

            Assert.That(operations.AbandonSiegeCalls, Is.EqualTo(1));
            Assert.That(result.AIBehavior?.AiBehavior, Is.EqualTo(AiBehavior.FleeToPoint));
        }

        [Test]
        public void DepletedPartyWhoseForceHasMassed_IsNotScaredOffByLordsItsForceOutweighs()
        {
            MobileParty party = DummyParty();
            Settlement target = DummySettlement();
            // What can be brought against the lords nearby is the greater of what this party has and what
            // the force standing at the gate has. Weighing the party alone drops a spent leader back to
            // looking around, and a lone besieger gives up the objective its siblings are standing on.
            var operations = new OperationsStub
            {
                SiegeTarget = target,
                PatrolTarget = DummySettlement(),
                AttackerStrength = 10f,
                ArrivedAttackerStrength = 100f,
                HostileLordStrengthNearTarget = 50f
            };
            BesiegeClosestSettlementBehaviour provider = Provider(operations);
            Evaluate(provider, party);
            SettlementOffensiveBehaviourState state = _states.GetOrAdd(party);
            state.Phase = SettlementOffensivePhase.Approach;

            PartyBehaviourResult result = Evaluate(provider, party);

            Assert.That(state.Target, Is.SameAs(target));
            Assert.That(state.Phase, Is.EqualTo(SettlementOffensivePhase.Approach));
            Assert.That(result.AIBehavior?.AiBehavior, Is.EqualTo(AiBehavior.BesiegeSettlement));
        }

        [Test]
        public void DangerousStrengthDuringAssault_DoesNotInterruptBattleUnderway()
        {
            MobileParty party = DummyParty();
            Settlement target = DummySettlement();
            var operations = new OperationsStub
            {
                SiegeTarget = target,
                MatchingSiegeActive = true,
                MatchingAssaultActive = true,
                ActiveSiegeDefenderStrength = 200f
            };
            BesiegeClosestSettlementBehaviour provider = Provider(operations);
            Evaluate(provider, party);

            PartyBehaviourResult result = Evaluate(provider, party);

            Assert.That(result.AIBehavior?.AiBehavior, Is.EqualTo(AiBehavior.BesiegeSettlement));
            Assert.That(_states.GetOrAdd(party).Target, Is.SameAs(target));
            Assert.That(operations.AbandonSiegeCalls, Is.Zero);
        }

        [Test]
        public void CaptureByAttackerAfterActiveSiege_EntersCooldown()
        {
            MobileParty party = DummyParty();
            Settlement target = DummySettlement();
            var operations = new OperationsStub { SiegeTarget = target };
            BesiegeClosestSettlementBehaviour provider = Provider(operations);
            Evaluate(provider, party);
            SettlementOffensiveBehaviourState state = _states.GetOrAdd(party);
            state.Target = target;
            state.Phase = SettlementOffensivePhase.Active;
            operations.TargetComplete = true;

            PartyBehaviourResult result = Evaluate(provider, party);

            Assert.That(result.IsApplicable, Is.False);
            Assert.That(state.Phase, Is.EqualTo(SettlementOffensivePhase.Cooldown));
        }

        [Test]
        public void PeaceThirdPartyOrIncompatibleSiege_InvalidatesAndRetargetsWithoutFailure()
        {
            MobileParty party = DummyParty();
            Settlement oldTarget = DummySettlement();
            Settlement replacement = DummySettlement();
            var operations = new OperationsStub { SiegeTarget = oldTarget };
            BesiegeClosestSettlementBehaviour provider = Provider(operations);
            Evaluate(provider, party);
            SettlementOffensiveBehaviourState state = _states.GetOrAdd(party);
            state.Target = oldTarget;
            state.Phase = SettlementOffensivePhase.Active;
            operations.CurrentTargetValid = false;
            operations.SiegeTarget = replacement;

            PartyBehaviourResult result = Evaluate(provider, party);

            Assert.That(state.Target, Is.SameAs(replacement));
            Assert.That(result.AIBehavior?.Party, Is.SameAs(replacement));
        }

        [Test]
        public void InterruptedActiveSiege_ImmediatelyRetriesSameTarget()
        {
            MobileParty party = DummyParty();
            Settlement target = DummySettlement();
            var operations = new OperationsStub { SiegeTarget = target };
            BesiegeClosestSettlementBehaviour provider = Provider(operations);
            Evaluate(provider, party);
            SettlementOffensiveBehaviourState state = _states.GetOrAdd(party);
            state.Target = target;
            state.Phase = SettlementOffensivePhase.Active;
            state.ElapsedOperationDays = 2f;

            PartyBehaviourResult result = Evaluate(provider, party);

            Assert.That(result.AIBehavior?.Party, Is.SameAs(target));
            Assert.That(state.Phase, Is.EqualTo(SettlementOffensivePhase.Approach));
            Assert.That(state.ElapsedOperationDays, Is.EqualTo(2f + 1f / 24f));
        }

        [Test]
        public void SearchOperationWindowExpiry_EntersCooldownAndFallsThrough()
        {
            MobileParty party = DummyParty();
            var operations = new OperationsStub();
            BesiegeClosestSettlementBehaviour provider = Provider(operations);
            Evaluate(provider, party);
            SettlementOffensiveBehaviourState state = _states.GetOrAdd(party);
            state.Phase = SettlementOffensivePhase.Search;
            state.ElapsedOperationDays = state.OperationWindowDays;

            PartyBehaviourResult result = Evaluate(provider, party);

            Assert.That(result.IsApplicable, Is.False);
            Assert.That(state.Phase, Is.EqualTo(SettlementOffensivePhase.Cooldown));
            Assert.That(state.CooldownRemainingDays, Is.EqualTo(3f));
        }

        [Test]
        public void SyncData_UsesSiegeSpecificKeyAndRestoresSharedStateDictionary()
        {
            MobileParty party = DummyParty();
            var loadedState = new SettlementOffensiveBehaviourState { Phase = SettlementOffensivePhase.Approach };
            var store = new DataStoreStub(new Dictionary<MobileParty, SettlementOffensiveBehaviourState>
            {
                [party] = loadedState
            });
            BesiegeClosestSettlementBehaviour provider = Provider(new OperationsStub());

            provider.SyncData(store);

            Assert.That(store.SeenKey, Is.EqualTo(BesiegeClosestSettlementBehaviour.SaveKey));
            Assert.That(_states.GetOrAdd(party), Is.SameAs(loadedState));
        }

        private static SiegeTargetCandidate Candidate(Settlement target, SpawnSettlementType type,
            float distance, float defenders, bool compatible = true, string targetId = "", bool hasPort = false)
        {
            return new SiegeTargetCandidate(target, true, true, type, compatible, distance, defenders,
                targetId, hasPort);
        }

        private static RoamingPartyStrength LordThreat(float distance, float strength)
        {
            return new RoamingPartyStrength(false, true, true, distance, strength, true);
        }

        private BesiegeClosestSettlementBehaviour Provider(OperationsStub operations)
        {
            _states = new PartyStateStore<SettlementOffensiveBehaviourState>(BesiegeClosestSettlementBehaviour.SaveKey,
                _ => true);
            return new BesiegeClosestSettlementBehaviour(operations, (minimum, _) => minimum, _states);
        }

        private static PartyBehaviourResult Evaluate(BesiegeClosestSettlementBehaviour provider,
            MobileParty party)
        {
            return provider.GetPriorityBehaviour(party, null!, Configuration());
        }

        private static BesiegeClosestSettlementData Configuration()
        {
            return new BesiegeClosestSettlementData(new[] { SpawnSettlementType.Town, SpawnSettlementType.Castle },
                1f, 1f, 1f, 1f, 2f, 10f, 10f, 3f);
        }

        private static IEnumerable<MobileParty> Counted(MobileParty party, ICollection<MobileParty> counted)
        {
            counted.Add(party);
            yield return party;
        }

        private static IInvasionLandingQuery Landing(Func<MobileParty, Func<Settlement, bool>?> filter,
            params MobileParty[] landingParties)
        {
            return new LandingStub(filter, landingParties, true);
        }

        private static IInvasionLandingQuery NoLiveInvasion(params MobileParty[] landingParties)
        {
            return new LandingStub(_ => null, landingParties, false);
        }

        private sealed class LandingStub : IInvasionLandingQuery
        {
            private readonly Func<MobileParty, Func<Settlement, bool>?> _filter;
            private readonly IReadOnlyList<MobileParty> _landingParties;
            private readonly bool _servesLiveInvasion;

            public LandingStub(Func<MobileParty, Func<Settlement, bool>?> filter,
                IReadOnlyList<MobileParty> landingParties, bool servesLiveInvasion)
            {
                _filter = filter;
                _landingParties = landingParties;
                _servesLiveInvasion = servesLiveInvasion;
            }

            public Func<Settlement, bool>? GetInitialCoastalTargetFilter(MobileParty party) => _filter(party);

            public IReadOnlyList<MobileParty> GetLandingParties(MobileParty party) => _landingParties;

            public bool ServesLiveInvasion(MobileParty party) => _servesLiveInvasion;
        }

        private static IMapPathQuery MapPaths(Func<Settlement, bool> hasReachableLandGate)
        {
            return new MapPathStub(hasReachableLandGate);
        }

        private sealed class MapPathStub : IMapPathQuery
        {
            private readonly Func<Settlement, bool> _hasReachableLandGate;

            public MapPathStub(Func<Settlement, bool> hasReachableLandGate)
            {
                _hasReachableLandGate = hasReachableLandGate;
            }

            public bool HasReachableLandGate(MobileParty party, Settlement target) =>
                _hasReachableLandGate(target);
        }

        private static MobileParty DummyParty()
        {
            return (MobileParty)FormatterServices.GetUninitializedObject(typeof(MobileParty));
        }

        private static MobileParty DummyPartyWithPartyBase()
        {
            MobileParty party = DummyParty();
            typeof(MobileParty).GetField("<Party>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(party, FormatterServices.GetUninitializedObject(typeof(PartyBase)));
            return party;
        }

        private static Settlement DummySettlement()
        {
            return (Settlement)FormatterServices.GetUninitializedObject(typeof(Settlement));
        }

        private static DynamicSpawnData DummyDynamicSpawnData()
        {
            return (DynamicSpawnData)FormatterServices.GetUninitializedObject(typeof(DynamicSpawnData));
        }

        private static Kingdom DummyKingdom()
        {
            return (Kingdom)FormatterServices.GetUninitializedObject(typeof(Kingdom));
        }

        private sealed class OperationsStub : ISiegeOperations
        {
            public float AttackerStrength { get; set; } = 100f;
            // What the party is worth once it has an objective and so a force. Left unset, the party is
            // worth the same whether or not it has committed, which is every test but the pooling one.
            public float AttackerStrengthOnceCommitted { get; set; } = -1f;
            public float VisibleHostileStrength { get; set; }
            public float HostileLordStrengthNearTarget { get; set; }
            public float TargetStrength { get; set; } = 10f;
            public float ActiveSiegeDefenderStrength { get; set; } = 10f;
            public Settlement? SiegeTarget { get; set; }
            public Settlement? AlternativeSiegeTarget { get; set; }
            public Settlement? PatrolTarget { get; set; }
            public bool MatchingSiegeActive { get; set; }
            public bool MatchingAssaultActive { get; set; }
            public bool CurrentTargetValid { get; set; } = true;
            public bool TargetComplete { get; set; }
            public bool Gathers { get; set; }
            public bool SupportableSiegeActive { get; set; }
            public bool ForcePinnedInBattle { get; set; }
            // A party standing in its own siege camp has arrived, so the default mirrors the party's own
            // assessment; the gathering tests state their own figure.
            public float ArrivedAttackerStrength { get; set; } = 100f;
            public AIBehaviorData? RetreatBehavior { get; set; }
            public int AbandonSiegeCalls { get; private set; }
            private bool _attackerStrengthAsked;

            // The first answer is the one taken before an objective is recorded, so every answer after it
            // is a party that has joined a force.
            public float GetAttackerStrength(MobileParty party)
            {
                if (_attackerStrengthAsked && AttackerStrengthOnceCommitted >= 0f)
                {
                    return AttackerStrengthOnceCommitted;
                }

                _attackerStrengthAsked = true;
                return AttackerStrength;
            }

            public float GetVisibleHostileStrength(MobileParty party) => VisibleHostileStrength;
            public Settlement? FindClosestSiegeTarget(MobileParty party, float attackerStrength,
                float minimumTargetRatio, float minimumRoamingHostilesRatio,
                IReadOnlyList<SpawnSettlementType> targetTypes) => SiegeTarget;
            public Settlement? FindAlternativeSiegeTarget(MobileParty party, Settlement excludedTarget,
                float attackerStrength, float minimumTargetRatio, float minimumRoamingHostilesRatio,
                IReadOnlyList<SpawnSettlementType> targetTypes) => AlternativeSiegeTarget;
            public Settlement? FindClosestNonHostileSettlement(MobileParty party) => PatrolTarget;
            public float GetTargetStrength(MobileParty party, Settlement target) => TargetStrength;
            public float GetActiveSiegeDefenderStrength(MobileParty party, Settlement target) =>
                ActiveSiegeDefenderStrength;
            public float GetHostileLordStrengthNearTarget(MobileParty party, Settlement target) =>
                HostileLordStrengthNearTarget;
            public IFaction GetTargetMapFaction(Settlement target) => null!;
            public bool IsMatchingSiegeActive(MobileParty party, Settlement target) => MatchingSiegeActive;
            public bool IsMatchingAssaultActive(MobileParty party, Settlement target) => MatchingAssaultActive;
            public bool IsCurrentTargetValid(MobileParty party, Settlement target,
                bool matchingOperationActive, IFaction? intendedTargetFaction) => CurrentTargetValid;
            public bool IsTargetComplete(MobileParty party, Settlement target) => TargetComplete;
            public bool GathersBeforeAssault(MobileParty party) => Gathers;
            public bool IsSupportableSiegeActive(MobileParty party, Settlement target) =>
                SupportableSiegeActive;
            public bool IsForcePinnedInBattle(MobileParty party, Settlement target) =>
                ForcePinnedInBattle;
            public float GetArrivedAttackerStrength(MobileParty party, Settlement target) =>
                ArrivedAttackerStrength;
            // What the party has with it, which pooling never enters: the figure taken before an objective
            // was recorded is exactly that, so the stub answers with it however often it is asked.
            public float GetOwnAttackerStrength(MobileParty party) => AttackerStrength;
            public AIBehaviorData CreatePatrolBehavior(MobileParty party, Settlement target)
            {
                return new AIBehaviorData(target, AiBehavior.PatrolAroundPoint,
                    MobileParty.NavigationType.Default, false, false, false);
            }

            public AIBehaviorData CreateSiegeBehavior(MobileParty party, Settlement target)
            {
                return new AIBehaviorData(target, AiBehavior.BesiegeSettlement,
                    MobileParty.NavigationType.Default, false, false, false);
            }

            public AIBehaviorData? AbandonSiegeAndCreateRetreatBehavior(MobileParty party,
                Settlement target)
            {
                AbandonSiegeCalls++;
                return RetreatBehavior;
            }
        }

        private sealed class DataStoreStub : IDataStore
        {
            private readonly object _loaded;

            public DataStoreStub(object loaded) => _loaded = loaded;
            public bool IsSaving => false;
            public bool IsLoading => true;
            public string? SeenKey { get; private set; }

            public bool SyncData<T>(string key, ref T data)
            {
                SeenKey = key;
                data = (T)_loaded;
                return true;
            }
        }
    }
}
