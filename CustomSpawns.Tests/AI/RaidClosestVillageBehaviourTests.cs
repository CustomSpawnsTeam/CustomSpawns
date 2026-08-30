using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using CustomSpawns.AI;
using CustomSpawns.AI.Model;
using CustomSpawns.Data.Dto;
using CustomSpawns.Spawn;
using NUnit.Framework;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace CustomSpawns.Tests.AI
{
    public class RaidClosestVillageBehaviourTests
    {
        [Test]
        public void Provider_HandlesTypedRaidConfiguration()
        {
            Assert.That(new RaidClosestVillageBehaviour(DummyDynamicSpawnData()).ConfigurationType,
                Is.EqualTo(typeof(RaidClosestVillageData)));
        }

        [Test]
        public void Search_SelectsPatrolAndKeepsProviderApplicable()
        {
            MobileParty party = DummyParty();
            Settlement patrol = DummySettlement();
            var context = new ContextStub { PatrolTarget = patrol };
            RaidClosestVillageBehaviour provider = Provider(context);

            PartyBehaviourResult result = Evaluate(provider, party);

            Assert.That(result.IsApplicable, Is.True);
            Assert.That(result.AIBehavior?.AiBehavior, Is.EqualTo(AiBehavior.PatrolAroundPoint));
            Assert.That(result.AIBehavior?.Party, Is.SameAs(patrol));
            Assert.That(context.FindRaidTargetCalls, Is.EqualTo(1));
            Assert.That(context.FindPatrolTargetCalls, Is.EqualTo(1));
            Assert.That(provider.GetState(party).Phase, Is.EqualTo(SettlementOffensivePhase.Search));
        }

        [Test]
        public void Search_WithoutPatrolRemainsApplicableWithoutBehavior()
        {
            MobileParty party = DummyParty();
            RaidClosestVillageBehaviour provider = Provider(new ContextStub());

            PartyBehaviourResult result = Evaluate(provider, party);

            Assert.That(result.IsApplicable, Is.True);
            Assert.That(result.AIBehavior, Is.Null);
        }

        [Test]
        public void ObserverVisibility_UnsafeReconPatrolsThenSafeDirectSkipRaids()
        {
            MobileParty party = DummyParty();
            Settlement target = DummySettlement();
            Settlement patrol = DummySettlement();
            var context = new ContextStub
            {
                RaidTarget = target,
                PatrolTarget = patrol,
                VisibleHostileStrength = 200f
            };
            RaidClosestVillageBehaviour provider = Provider(context);

            PartyBehaviourResult unsafeResult = Evaluate(provider, party);
            context.VisibleHostileStrength = 0f;
            PartyBehaviourResult safeResult = Evaluate(provider, party);

            Assert.That(unsafeResult.AIBehavior?.AiBehavior, Is.EqualTo(AiBehavior.PatrolAroundPoint));
            Assert.That(provider.GetState(party).Target, Is.SameAs(target));
            Assert.That(safeResult.AIBehavior?.AiBehavior, Is.EqualTo(AiBehavior.RaidSettlement));
            Assert.That(context.VisibleHostileStrengthCalls, Is.EqualTo(2));
        }

        [Test]
        public void ConcreteContext_UsesEachObserverSeeingRangeAndInclusiveDistanceBoundary()
        {
            MobileParty boundaryObserver = DummyParty();
            MobileParty shortRangeObserver = DummyParty();
            var observations = new[]
            {
                new RoamingPartyStrength(false, true, true, null, 5f, 20f),
                new RoamingPartyStrength(false, true, true, null, 5.001f, 100f)
            };
            int rangeReads = 0;
            int observationReads = 0;
            var context = new BannerlordRaidClosestVillageContext(
                observer =>
                {
                    rangeReads++;
                    return observer == boundaryObserver ? 5f : 4.999f;
                },
                observer =>
                {
                    observationReads++;
                    Assert.That(observer, Is.SameAs(boundaryObserver).Or.SameAs(shortRangeObserver));
                    return observations;
                },
                _ => new List<MobileParty>());

            float atBoundary = context.GetVisibleHostileStrength(boundaryObserver);
            float outsideShorterObserverRange = context.GetVisibleHostileStrength(shortRangeObserver);

            Assert.That(atBoundary, Is.EqualTo(20f));
            Assert.That(outsideShorterObserverRange, Is.Zero);
            Assert.That(rangeReads, Is.EqualTo(2));
            Assert.That(observationReads, Is.EqualTo(2));
        }

        [Test]
        public void ActiveRaid_ReturnsRaidAndFreezesOperationClock()
        {
            MobileParty party = DummyParty();
            Settlement target = DummySettlement();
            var context = new ContextStub { RaidTarget = target };
            RaidClosestVillageBehaviour provider = Provider(context);
            Evaluate(provider, party);
            SettlementOffensiveBehaviourState state = provider.GetState(party);
            state.Phase = SettlementOffensivePhase.Active;
            state.Target = target;
            state.ElapsedOperationDays = 2f;
            context.MatchingRaidActive = true;

            PartyBehaviourResult result = Evaluate(provider, party);

            Assert.That(result.AIBehavior?.AiBehavior, Is.EqualTo(AiBehavior.RaidSettlement));
            Assert.That(state.Phase, Is.EqualTo(SettlementOffensivePhase.Active));
            Assert.That(state.ElapsedOperationDays, Is.EqualTo(2f));
        }

        [TestCase(AiBehavior.EngageParty)]
        [TestCase(AiBehavior.FleeToPoint)]
        public void Recovery_NativeFleeOrEngageTakesPrecedence(AiBehavior shortTermBehavior)
        {
            MobileParty party = DummyParty();
            Settlement target = DummySettlement();
            var context = new ContextStub
            {
                RaidTarget = target,
                ShortTermBehavior = shortTermBehavior
            };
            RaidClosestVillageBehaviour provider = Provider(context);
            Evaluate(provider, party);
            SettlementOffensiveBehaviourState state = provider.GetState(party);
            state.Phase = SettlementOffensivePhase.Recovery;
            state.Target = target;
            state.RecoveryGraceDays = 1f;
            state.RecoveryElapsedDays = 0f;
            state.ElapsedOperationDays = 2f;

            PartyBehaviourResult result = Evaluate(provider, party);

            Assert.That(result.IsApplicable, Is.True);
            Assert.That(result.AIBehavior, Is.Null);
            Assert.That(state.Target, Is.SameAs(target));
            Assert.That(state.ElapsedOperationDays, Is.EqualTo(2f));
            Assert.That(state.RecoveryElapsedDays, Is.EqualTo(1f / 24f));
        }

        [Test]
        public void Recovery_WithoutNativeSafetyInterruptionRepushesSameTarget()
        {
            MobileParty party = DummyParty();
            Settlement target = DummySettlement();
            var context = new ContextStub { RaidTarget = target };
            RaidClosestVillageBehaviour provider = Provider(context);
            Evaluate(provider, party);
            SettlementOffensiveBehaviourState state = provider.GetState(party);
            state.Phase = SettlementOffensivePhase.Recovery;
            state.Target = target;
            state.RecoveryGraceDays = 1f;

            PartyBehaviourResult result = Evaluate(provider, party);

            Assert.That(result.AIBehavior?.AiBehavior, Is.EqualTo(AiBehavior.RaidSettlement));
            Assert.That(result.AIBehavior?.Party, Is.SameAs(target));
            Assert.That(state.Target, Is.SameAs(target));
        }

        [Test]
        public void InvalidActiveTarget_RetargetsWithoutFailureAndRaidsReplacement()
        {
            MobileParty party = DummyParty();
            Settlement oldTarget = DummySettlement();
            Settlement replacement = DummySettlement();
            var context = new ContextStub { RaidTarget = oldTarget };
            RaidClosestVillageBehaviour provider = Provider(context);
            Evaluate(provider, party);
            SettlementOffensiveBehaviourState state = provider.GetState(party);
            state.Phase = SettlementOffensivePhase.Active;
            state.Target = oldTarget;
            context.CurrentTargetValid = false;
            context.RaidTarget = replacement;

            PartyBehaviourResult result = Evaluate(provider, party);

            Assert.That(state.Target, Is.SameAs(replacement));
            Assert.That(state.FailedAttempts, Is.Zero);
            Assert.That(result.AIBehavior?.AiBehavior, Is.EqualTo(AiBehavior.RaidSettlement));
            Assert.That(result.AIBehavior?.Party, Is.SameAs(replacement));
        }

        [Test]
        public void SyncData_UsesRaidSpecificKeyAndRestoresStateDictionary()
        {
            MobileParty party = DummyParty();
            var loadedState = new SettlementOffensiveBehaviourState
            {
                Phase = SettlementOffensivePhase.Recovery,
                FailedAttempts = 2
            };
            var loaded = new Dictionary<MobileParty, SettlementOffensiveBehaviourState>
            {
                [party] = loadedState
            };
            var dataStore = new DataStoreStub(loaded);
            RaidClosestVillageBehaviour provider = Provider(new ContextStub());

            provider.SyncData(dataStore);

            Assert.That(dataStore.SeenKey, Is.EqualTo(RaidClosestVillageBehaviour.SaveKey));
            Assert.That(provider.GetState(party), Is.SameAs(loadedState));
        }

        [Test]
        public void Cooldown_ReturnsNotApplicableBeforeRuntimeBoundaryEvaluation()
        {
            MobileParty party = DummyParty();
            var context = new ContextStub();
            RaidClosestVillageBehaviour provider = Provider(context);
            Evaluate(provider, party);
            SettlementOffensiveBehaviourState state = provider.GetState(party);
            state.Phase = SettlementOffensivePhase.Cooldown;
            state.CooldownRemainingDays = 1f;
            int strengthCalls = context.AttackerStrengthCalls;

            PartyBehaviourResult result = Evaluate(provider, party);

            Assert.That(result.IsApplicable, Is.False);
            Assert.That(result.AIBehavior, Is.Null);
            Assert.That(context.AttackerStrengthCalls, Is.EqualTo(strengthCalls));
            Assert.That(state.CooldownRemainingDays, Is.EqualTo(1f - 1f / 24f));
        }

        [Test]
        public void TargetRatio_ZeroMilitiaIsAlwaysAQualifiedTarget()
        {
            Assert.That(SettlementOffensiveBehaviour.MeetsStrengthRatio(0f, 0f, float.MaxValue),
                Is.True);
        }

        [Test]
        public void ConcreteContext_AddsTheAccompanyingPartiesOfTheAssessedPartyToItsOwnStrength()
        {
            MobileParty party = DummyPartyWithPartyBase();
            MobileParty accompanyingParty = DummyPartyWithPartyBase();
            MobileParty? assessedParty = null;
            var counted = new List<MobileParty>();
            var context = new BannerlordRaidClosestVillageContext(_ => 5f,
                _ => new RoamingPartyStrength[0], observed =>
                {
                    assessedParty = observed;
                    return Counted(accompanyingParty, counted);
                });

            context.GetAttackerStrength(party);

            Assert.That(assessedParty, Is.SameAs(party));
            Assert.That(counted, Is.EqualTo(new[] { accompanyingParty }));
        }

        [Test]
        public void ConcreteContext_ArmyLeaderTakesTheArmyTotalWithoutCountingAccompanyingPartiesAgain()
        {
            MobileParty party = ArmyLeadingParty();
            bool accompanyingPartiesConsulted = false;
            var context = new BannerlordRaidClosestVillageContext(_ => 5f,
                _ => new RoamingPartyStrength[0], _ =>
                {
                    accompanyingPartiesConsulted = true;
                    return new List<MobileParty>();
                });

            context.GetAttackerStrength(party);

            Assert.That(accompanyingPartiesConsulted, Is.False);
        }

        [Test]
        public void TargetRatio_EscortStrengthAdmitsAVillageTheLeaderPartyAloneWouldSkip()
        {
            MobileParty party = DummyParty();
            Settlement village = DummySettlement();
            var candidates = new[] { new RaidTargetCandidate<Settlement>(village, true, 1f, 60f) };
            var context = new ContextStub { AttackerStrength = 150f };

            Evaluate(Provider(context), party);

            Assert.That(context.FindRaidTargetStrength, Is.EqualTo(150f));
            Assert.That(SettlementOffensiveBehaviour.ChooseClosestTarget(candidates, 50f, 1f), Is.Null);
            Assert.That(SettlementOffensiveBehaviour.ChooseClosestTarget(candidates,
                context.FindRaidTargetStrength, 1f), Is.SameAs(village));
        }

        private static RaidClosestVillageBehaviour Provider(ContextStub context)
        {
            return new RaidClosestVillageBehaviour(context, (minimum, _) => minimum);
        }

        private static PartyBehaviourResult Evaluate(RaidClosestVillageBehaviour provider,
            MobileParty party)
        {
            return provider.GetPriorityBehaviour(party, null!, Configuration());
        }

        private static RaidClosestVillageData Configuration()
        {
            return new RaidClosestVillageData(1f, 1f, 1f, 1f, 2f,
                10f, 10f, 1f, 2, 3f);
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

        private static MobileParty ArmyLeadingParty()
        {
            MobileParty party = DummyPartyWithPartyBase();
            var army = (Army)FormatterServices.GetUninitializedObject(typeof(Army));
            typeof(Army).GetField("<LeaderParty>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(army, party);
            typeof(MobileParty).GetField("_army", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(party, army);
            typeof(MobileParty).GetField("_attachedParties", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(party, new MBList<MobileParty>());
            return party;
        }

        private static DynamicSpawnData DummyDynamicSpawnData()
        {
            return (DynamicSpawnData)FormatterServices.GetUninitializedObject(typeof(DynamicSpawnData));
        }

        private static Settlement DummySettlement()
        {
            return (Settlement)FormatterServices.GetUninitializedObject(typeof(Settlement));
        }

        private sealed class ContextStub : IRaidClosestVillageContext
        {
            public float AttackerStrength { get; set; } = 100f;
            public float VisibleHostileStrength { get; set; }
            public Settlement? RaidTarget { get; set; }
            public Settlement? PatrolTarget { get; set; }
            public float TargetStrength { get; set; } = 10f;
            public bool MatchingRaidActive { get; set; }
            public bool CurrentTargetValid { get; set; } = true;
            public bool TargetComplete { get; set; }
            public bool NativeContinuation { get; set; }
            public AiBehavior ShortTermBehavior { get; set; }
            public int AttackerStrengthCalls { get; private set; }
            public int VisibleHostileStrengthCalls { get; private set; }
            public int FindRaidTargetCalls { get; private set; }
            public float FindRaidTargetStrength { get; private set; }
            public int FindPatrolTargetCalls { get; private set; }

            public float GetAttackerStrength(MobileParty party)
            {
                AttackerStrengthCalls++;
                return AttackerStrength;
            }

            public float GetVisibleHostileStrength(MobileParty party)
            {
                VisibleHostileStrengthCalls++;
                return VisibleHostileStrength;
            }

            public Settlement? FindClosestRaidTarget(MobileParty party, float attackerStrength,
                float minimumRatio)
            {
                FindRaidTargetCalls++;
                FindRaidTargetStrength = attackerStrength;
                return RaidTarget;
            }

            public Settlement? FindClosestNonHostileSettlement(MobileParty party)
            {
                FindPatrolTargetCalls++;
                return PatrolTarget;
            }

            public float GetTargetStrength(Settlement target) => TargetStrength;

            public bool IsMatchingRaidActive(MobileParty party, Settlement target) => MatchingRaidActive;

            public bool IsCurrentTargetValid(MobileParty party, Settlement target,
                bool matchingRaidActive) => CurrentTargetValid;

            public bool IsTargetComplete(Settlement target) => TargetComplete;

            public bool HasNativeContinuation(MobileParty party, Settlement target) => NativeContinuation;

            public AiBehavior GetShortTermBehavior(MobileParty party) => ShortTermBehavior;

            public AIBehaviorData CreatePatrolBehavior(MobileParty party, Settlement target)
            {
                return new AIBehaviorData(target, AiBehavior.PatrolAroundPoint,
                    MobileParty.NavigationType.Default, false, false, false);
            }

            public AIBehaviorData CreateRaidBehavior(MobileParty party, Settlement target)
            {
                return new AIBehaviorData(target, AiBehavior.RaidSettlement,
                    MobileParty.NavigationType.Default, false, false, false);
            }
        }

        private sealed class DataStoreStub : IDataStore
        {
            private readonly object _loaded;

            public DataStoreStub(object loaded)
            {
                _loaded = loaded;
            }

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
