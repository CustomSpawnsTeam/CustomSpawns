using System.Collections.Generic;
using System.Runtime.Serialization;
using CustomSpawns.AI;
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
            Assert.That(new BesiegeClosestSettlementBehaviour().ConfigurationType,
                Is.EqualTo(typeof(BesiegeClosestSettlementData)));
        }

        [Test]
        public void ConcreteContext_FiltersTownCastleAndIncompatibleSiegesThenChoosesClosestQualified()
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
            var context = new BannerlordBesiegeClosestSettlementContext(_ => 5f, _ => new RoamingPartyStrength[0],
                _ => candidates);

            Settlement? result = context.FindClosestSiegeTarget(party, 100f, 1.5f,
                new[] { SpawnSettlementType.Town, SpawnSettlementType.Castle });

            Assert.That(result, Is.SameAs(qualifiedCastle));
        }

        [Test]
        public void ConcreteContext_ZeroDefendersAndConfiguredTownOnlyAreHandledSafely()
        {
            MobileParty party = DummyParty();
            Settlement castle = DummySettlement();
            Settlement town = DummySettlement();
            var context = new BannerlordBesiegeClosestSettlementContext(_ => 5f,
                _ => new RoamingPartyStrength[0], _ => new[]
                {
                    Candidate(castle, SpawnSettlementType.Castle, 1f, 0f),
                    Candidate(town, SpawnSettlementType.Town, 2f, 0f)
                });

            Settlement? result = context.FindClosestSiegeTarget(party, 0f, float.MaxValue,
                new[] { SpawnSettlementType.Town });

            Assert.That(result, Is.SameAs(town));
        }

        [Test]
        public void DefenderStrength_SumsMilitiaGarrisonAndLordPartiesOnce()
        {
            Assert.That(BannerlordBesiegeClosestSettlementContext.SumDefenderStrength(20f, 30f,
                new[] { 40f, 10f }), Is.EqualTo(100f));
        }

        [Test]
        public void IntendedOwnership_RejectsThirdPartyCapture()
        {
            IFaction intendedDefender = DummyKingdom();
            IFaction thirdParty = DummyKingdom();

            Assert.That(BannerlordBesiegeClosestSettlementContext.IsIntendedTargetFaction(
                intendedDefender, intendedDefender), Is.True);
            Assert.That(BannerlordBesiegeClosestSettlementContext.IsIntendedTargetFaction(
                thirdParty, intendedDefender), Is.False);
        }

        [Test]
        public void ObserverVisibility_UnsafeReconPatrolsThenSafeDirectSkipBesieges()
        {
            MobileParty party = DummyParty();
            Settlement target = DummySettlement();
            Settlement patrol = DummySettlement();
            var context = new ContextStub
            {
                SiegeTarget = target,
                PatrolTarget = patrol,
                VisibleHostileStrength = 200f
            };
            BesiegeClosestSettlementBehaviour provider = Provider(context);

            PartyBehaviourResult unsafeResult = Evaluate(provider, party);
            context.VisibleHostileStrength = 0f;
            PartyBehaviourResult safeResult = Evaluate(provider, party);

            Assert.That(unsafeResult.AIBehavior?.AiBehavior, Is.EqualTo(AiBehavior.PatrolAroundPoint));
            Assert.That(safeResult.AIBehavior?.AiBehavior, Is.EqualTo(AiBehavior.BesiegeSettlement));
            Assert.That(provider.GetState(party).Target, Is.SameAs(target));
        }

        [Test]
        public void ConcreteContext_UsesEachObserverSeeingRangeAndInclusiveBoundary()
        {
            MobileParty boundaryObserver = DummyParty();
            MobileParty shorterObserver = DummyParty();
            var observations = new[]
            {
                new RoamingPartyStrength(false, true, true, null, 5f, 20f),
                new RoamingPartyStrength(false, true, true, null, 5.001f, 100f)
            };
            var context = new BannerlordBesiegeClosestSettlementContext(
                observer => observer == boundaryObserver ? 5f : 4.999f,
                _ => observations, _ => new SiegeTargetCandidate[0]);

            Assert.That(context.GetVisibleHostileStrength(boundaryObserver), Is.EqualTo(20f));
            Assert.That(context.GetVisibleHostileStrength(shorterObserver), Is.Zero);
        }

        [Test]
        public void UnsafeApproach_ReturnsToReconInsteadOfForcingContact()
        {
            MobileParty party = DummyParty();
            Settlement target = DummySettlement();
            Settlement patrol = DummySettlement();
            var context = new ContextStub { SiegeTarget = target, PatrolTarget = patrol };
            BesiegeClosestSettlementBehaviour provider = Provider(context);
            Evaluate(provider, party);
            SettlementOffensiveBehaviourState state = provider.GetState(party);
            state.Phase = SettlementOffensivePhase.Approach;
            context.VisibleHostileStrength = 200f;

            PartyBehaviourResult result = Evaluate(provider, party);

            Assert.That(state.Phase, Is.EqualTo(SettlementOffensivePhase.Recon));
            Assert.That(result.AIBehavior?.AiBehavior, Is.EqualTo(AiBehavior.PatrolAroundPoint));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ActiveCampOrAssault_FreezesClockAndKeepsSiegeUnderway(bool assault)
        {
            MobileParty party = DummyParty();
            Settlement target = DummySettlement();
            var context = new ContextStub { SiegeTarget = target };
            BesiegeClosestSettlementBehaviour provider = Provider(context);
            Evaluate(provider, party);
            SettlementOffensiveBehaviourState state = provider.GetState(party);
            state.Target = target;
            state.Phase = SettlementOffensivePhase.Approach;
            state.ElapsedOperationDays = 10f;
            context.MatchingSiegeActive = !assault;
            context.MatchingAssaultActive = assault;

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
            var context = new ContextStub { SiegeTarget = target };
            BesiegeClosestSettlementBehaviour provider = Provider(context);
            Evaluate(provider, party);
            SettlementOffensiveBehaviourState state = provider.GetState(party);
            state.Target = target;
            state.Phase = SettlementOffensivePhase.Active;
            context.TargetComplete = true;

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
            var context = new ContextStub { SiegeTarget = oldTarget };
            BesiegeClosestSettlementBehaviour provider = Provider(context);
            Evaluate(provider, party);
            SettlementOffensiveBehaviourState state = provider.GetState(party);
            state.Target = oldTarget;
            state.Phase = SettlementOffensivePhase.Active;
            context.CurrentTargetValid = false;
            context.SiegeTarget = replacement;

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
            var context = new ContextStub { SiegeTarget = target };
            BesiegeClosestSettlementBehaviour provider = Provider(context);
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
            var context = new ContextStub { SiegeTarget = oldTarget };
            BesiegeClosestSettlementBehaviour provider = Provider(context);
            Evaluate(provider, party);
            SettlementOffensiveBehaviourState state = provider.GetState(party);
            state.Target = oldTarget;
            state.Phase = SettlementOffensivePhase.Active;
            state.FailedAttempts = 2;
            context.SiegeTarget = replacement;

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
            var context = new ContextStub { SiegeTarget = oldTarget };
            BesiegeClosestSettlementBehaviour provider = Provider(context);
            Evaluate(provider, party);
            SettlementOffensiveBehaviourState state = provider.GetState(party);
            state.Target = oldTarget;
            state.Phase = SettlementOffensivePhase.Recovery;
            state.RecoveryGraceDays = 0f;
            context.SiegeTarget = replacement;

            PartyBehaviourResult result = Evaluate(provider, party);

            Assert.That(state.Target, Is.SameAs(replacement));
            Assert.That(result.AIBehavior?.Party, Is.SameAs(replacement));
        }

        [Test]
        public void SearchOperationWindowExpiry_EntersCooldownAndFallsThrough()
        {
            MobileParty party = DummyParty();
            var context = new ContextStub();
            BesiegeClosestSettlementBehaviour provider = Provider(context);
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
            BesiegeClosestSettlementBehaviour provider = Provider(new ContextStub());

            provider.SyncData(store);

            Assert.That(store.SeenKey, Is.EqualTo(BesiegeClosestSettlementBehaviour.SaveKey));
            Assert.That(provider.GetState(party), Is.SameAs(loadedState));
        }

        private static SiegeTargetCandidate Candidate(Settlement target, SpawnSettlementType type,
            float distance, float defenders, bool compatible = true)
        {
            return new SiegeTargetCandidate(target, true, true, type, compatible, distance, defenders);
        }

        private static BesiegeClosestSettlementBehaviour Provider(ContextStub context)
        {
            return new BesiegeClosestSettlementBehaviour(context, (minimum, _) => minimum);
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

        private static MobileParty DummyParty()
        {
            return (MobileParty)FormatterServices.GetUninitializedObject(typeof(MobileParty));
        }

        private static Settlement DummySettlement()
        {
            return (Settlement)FormatterServices.GetUninitializedObject(typeof(Settlement));
        }

        private static Kingdom DummyKingdom()
        {
            return (Kingdom)FormatterServices.GetUninitializedObject(typeof(Kingdom));
        }

        private sealed class ContextStub : IBesiegeClosestSettlementContext
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
            public float GetTargetStrength(Settlement target) => TargetStrength;
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
