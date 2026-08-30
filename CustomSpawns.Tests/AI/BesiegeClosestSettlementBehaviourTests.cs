using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using CustomSpawns.AI;
using CustomSpawns.AI.Model;
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
        public void DefenderStrength_IncludesEligibleNonLordOccupant()
        {
            var occupant = new SettlementDefenderPartyStrength(25f, isLordParty: false);

            Assert.That(BannerlordBesiegeClosestSettlementAdapter.CalculateDefenderStrength(
                0f, 0f, new[] { occupant }), Is.EqualTo(25f));
        }

        [Test]
        public void DefenderStrength_IncludesAttackerHostileThirdPartyOccupant()
        {
            var occupant = new SettlementDefenderPartyStrength(25f,
                isHostileToAttacker: true, isSettlementFaction: false);

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
                VisibleHostileStrength = 200f
            };
            BesiegeClosestSettlementBehaviour provider = Provider(operations);

            PartyBehaviourResult unsafeResult = Evaluate(provider, party);
            operations.VisibleHostileStrength = 0f;
            PartyBehaviourResult safeResult = Evaluate(provider, party);

            Assert.That(unsafeResult.AIBehavior?.AiBehavior, Is.EqualTo(AiBehavior.PatrolAroundPoint));
            Assert.That(safeResult.AIBehavior?.AiBehavior, Is.EqualTo(AiBehavior.BesiegeSettlement));
            Assert.That(provider.GetState(party).Target, Is.SameAs(target));
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
        public void UnsafeApproach_ReturnsToReconInsteadOfForcingContact()
        {
            MobileParty party = DummyParty();
            Settlement target = DummySettlement();
            Settlement patrol = DummySettlement();
            var operations = new OperationsStub { SiegeTarget = target, PatrolTarget = patrol };
            BesiegeClosestSettlementBehaviour provider = Provider(operations);
            Evaluate(provider, party);
            SettlementOffensiveBehaviourState state = provider.GetState(party);
            state.Phase = SettlementOffensivePhase.Approach;
            operations.VisibleHostileStrength = 200f;

            PartyBehaviourResult result = Evaluate(provider, party);

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
            SettlementOffensiveBehaviourState state = provider.GetState(party);
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
            SettlementOffensiveBehaviourState state = provider.GetState(party);
            operations.MatchingSiegeActive = true;
            operations.TargetStrength = 200f;
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
            SettlementOffensiveBehaviourState state = provider.GetState(party);
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
            SettlementOffensiveBehaviourState state = provider.GetState(party);
            state.Target = target;
            state.Phase = SettlementOffensivePhase.Approach;
            state.ElapsedOperationDays = 10f;
            operations.MatchingSiegeActive = !assault;
            operations.MatchingAssaultActive = assault;

            PartyBehaviourResult result = Evaluate(provider, party);

            Assert.That(result.AIBehavior?.AiBehavior, Is.EqualTo(AiBehavior.BesiegeSettlement));
            Assert.That(state.Phase, Is.EqualTo(SettlementOffensivePhase.Active));
            Assert.That(state.ElapsedOperationDays, Is.EqualTo(10f));
        }

        [Test]
        public void CaptureByAttackerAfterActiveSiege_EntersCooldown()
        {
            MobileParty party = DummyParty();
            Settlement target = DummySettlement();
            var operations = new OperationsStub { SiegeTarget = target };
            BesiegeClosestSettlementBehaviour provider = Provider(operations);
            Evaluate(provider, party);
            SettlementOffensiveBehaviourState state = provider.GetState(party);
            state.Target = target;
            state.Phase = SettlementOffensivePhase.Active;
            operations.TargetComplete = true;

            PartyBehaviourResult result = Evaluate(provider, party);

            Assert.That(result.IsApplicable, Is.False);
            Assert.That(state.Phase, Is.EqualTo(SettlementOffensivePhase.Cooldown));
            Assert.That(state.FailedAttempts, Is.Zero);
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
            SettlementOffensiveBehaviourState state = provider.GetState(party);
            state.Target = oldTarget;
            state.Phase = SettlementOffensivePhase.Active;
            operations.CurrentTargetValid = false;
            operations.SiegeTarget = replacement;

            PartyBehaviourResult result = Evaluate(provider, party);

            Assert.That(state.Target, Is.SameAs(replacement));
            Assert.That(state.FailedAttempts, Is.Zero);
            Assert.That(result.AIBehavior?.Party, Is.SameAs(replacement));
        }

        [Test]
        public void InterruptedActiveSiege_UsesSameTargetGraceAndDeduplicatesFailure()
        {
            MobileParty party = DummyParty();
            Settlement target = DummySettlement();
            var operations = new OperationsStub { SiegeTarget = target };
            BesiegeClosestSettlementBehaviour provider = Provider(operations);
            Evaluate(provider, party);
            SettlementOffensiveBehaviourState state = provider.GetState(party);
            state.Target = target;
            state.Phase = SettlementOffensivePhase.Active;
            state.RecoveryGraceDays = 1f;
            state.ElapsedOperationDays = 2f;

            PartyBehaviourResult first = Evaluate(provider, party);
            PartyBehaviourResult duplicate = Evaluate(provider, party);

            Assert.That(first.AIBehavior?.Party, Is.SameAs(target));
            Assert.That(duplicate.AIBehavior?.Party, Is.SameAs(target));
            Assert.That(state.Phase, Is.EqualTo(SettlementOffensivePhase.Recovery));
            Assert.That(state.FailedAttempts, Is.EqualTo(1));
            Assert.That(state.ElapsedOperationDays, Is.EqualTo(2f));
        }

        [Test]
        public void InterruptionAfterFinalAllowedResume_DiscardsAndRetargetsImmediately()
        {
            MobileParty party = DummyParty();
            Settlement oldTarget = DummySettlement();
            Settlement replacement = DummySettlement();
            var operations = new OperationsStub { SiegeTarget = oldTarget };
            BesiegeClosestSettlementBehaviour provider = Provider(operations);
            Evaluate(provider, party);
            SettlementOffensiveBehaviourState state = provider.GetState(party);
            state.Target = oldTarget;
            state.Phase = SettlementOffensivePhase.Active;
            state.FailedAttempts = 2;
            operations.SiegeTarget = replacement;

            PartyBehaviourResult result = Evaluate(provider, party);

            Assert.That(state.Target, Is.SameAs(replacement));
            Assert.That(result.AIBehavior?.Party, Is.SameAs(replacement));
        }

        [Test]
        public void RecoveryGraceExpiry_DiscardsAndRetargets()
        {
            MobileParty party = DummyParty();
            Settlement oldTarget = DummySettlement();
            Settlement replacement = DummySettlement();
            var operations = new OperationsStub { SiegeTarget = oldTarget };
            BesiegeClosestSettlementBehaviour provider = Provider(operations);
            Evaluate(provider, party);
            SettlementOffensiveBehaviourState state = provider.GetState(party);
            state.Target = oldTarget;
            state.Phase = SettlementOffensivePhase.Recovery;
            state.RecoveryGraceDays = 0f;
            operations.SiegeTarget = replacement;

            PartyBehaviourResult result = Evaluate(provider, party);

            Assert.That(state.Target, Is.SameAs(replacement));
            Assert.That(result.AIBehavior?.Party, Is.SameAs(replacement));
        }

        [Test]
        public void SearchOperationWindowExpiry_EntersCooldownAndFallsThrough()
        {
            MobileParty party = DummyParty();
            var operations = new OperationsStub();
            BesiegeClosestSettlementBehaviour provider = Provider(operations);
            Evaluate(provider, party);
            SettlementOffensiveBehaviourState state = provider.GetState(party);
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
            var loadedState = new SettlementOffensiveBehaviourState { FailedAttempts = 2 };
            var store = new DataStoreStub(new Dictionary<MobileParty, SettlementOffensiveBehaviourState>
            {
                [party] = loadedState
            });
            BesiegeClosestSettlementBehaviour provider = Provider(new OperationsStub());

            provider.SyncData(store);

            Assert.That(store.SeenKey, Is.EqualTo(BesiegeClosestSettlementBehaviour.SaveKey));
            Assert.That(provider.GetState(party), Is.SameAs(loadedState));
        }

        private static SiegeTargetCandidate Candidate(Settlement target, SpawnSettlementType type,
            float distance, float defenders, bool compatible = true)
        {
            return new SiegeTargetCandidate(target, true, true, type, compatible, distance, defenders);
        }

        private static BesiegeClosestSettlementBehaviour Provider(OperationsStub operations)
        {
            return new BesiegeClosestSettlementBehaviour(operations, (minimum, _) => minimum);
        }

        private static PartyBehaviourResult Evaluate(BesiegeClosestSettlementBehaviour provider,
            MobileParty party)
        {
            return provider.GetPriorityBehaviour(party, null!, Configuration());
        }

        private static BesiegeClosestSettlementData Configuration()
        {
            return new BesiegeClosestSettlementData(new[] { SpawnSettlementType.Town, SpawnSettlementType.Castle },
                1f, 1f, 1f, 1f, 2f, 10f, 10f, 1f, 2, 3f);
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
            public float TargetStrength { get; set; } = 10f;
            public Settlement? SiegeTarget { get; set; }
            public Settlement? PatrolTarget { get; set; }
            public bool MatchingSiegeActive { get; set; }
            public bool MatchingAssaultActive { get; set; }
            public bool CurrentTargetValid { get; set; } = true;
            public bool TargetComplete { get; set; }
            public bool NativeContinuation { get; set; }
            public AiBehavior ShortTermBehavior { get; set; }

            public float GetAttackerStrength(MobileParty party) => AttackerStrength;
            public float GetVisibleHostileStrength(MobileParty party) => VisibleHostileStrength;
            public Settlement? FindClosestSiegeTarget(MobileParty party, float attackerStrength,
                float minimumRatio, IReadOnlyList<SpawnSettlementType> targetTypes) => SiegeTarget;
            public Settlement? FindClosestNonHostileSettlement(MobileParty party) => PatrolTarget;
            public float GetTargetStrength(MobileParty party, Settlement target) => TargetStrength;
            public IFaction GetTargetMapFaction(Settlement target) => null!;
            public bool IsMatchingSiegeActive(MobileParty party, Settlement target) => MatchingSiegeActive;
            public bool IsMatchingAssaultActive(MobileParty party, Settlement target) => MatchingAssaultActive;
            public bool IsCurrentTargetValid(MobileParty party, Settlement target,
                bool matchingOperationActive, IFaction? intendedTargetFaction) => CurrentTargetValid;
            public bool IsTargetComplete(MobileParty party, Settlement target) => TargetComplete;
            public bool HasNativeContinuation(MobileParty party, Settlement target) => NativeContinuation;
            public AiBehavior GetShortTermBehavior(MobileParty party) => ShortTermBehavior;

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
