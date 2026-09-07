using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using CustomSpawns.AI.Adapters;
using CustomSpawns.AI.Behaviours;
using CustomSpawns.AI.Model;
using CustomSpawns.AI.Offensives;
using CustomSpawns.Data.Adapter;
using CustomSpawns.Data.Dto;
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
                }, _ => new MobileParty[0], _ => settlement => settlement == coastalTown,
                (_, settlement) => settlement == coastalTown);

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
                }, _ => new MobileParty[0], _ => settlement => settlement == coastalTown, (_, _) => false);

            Settlement? result = operations.FindClosestSiegeTarget(party, 100f, 1.5f,
                new[] { SpawnSettlementType.Town, SpawnSettlementType.Castle });

            Assert.That(result, Is.Null);
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
                }, _ => new MobileParty[0], _ => _ => true, (_, _) => true);

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
                }, _ => new MobileParty[0], _ => null, (_, _) => false);

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
                }, _ => new MobileParty[0], _ => null, (_, _) => false);

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
                new RoamingPartyStrength(false, true, true, null, 5f, 20f),
                new RoamingPartyStrength(false, true, true, null, 5.001f, 100f)
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
                new RoamingPartyStrength(false, true, true, null, 5f, 20f, true),
                new RoamingPartyStrength(false, true, true, null, 5f, 100f)
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
            Assert.That(counted, Is.EqualTo(new[] { accompanyingParty }));
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
                ActiveSiegeDefenderStrength = 101f,
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
            return new RoamingPartyStrength(false, true, true, null, distance, strength, true);
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
            public AIBehaviorData? RetreatBehavior { get; set; }
            public int AbandonSiegeCalls { get; private set; }

            public float GetAttackerStrength(MobileParty party) => AttackerStrength;
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
