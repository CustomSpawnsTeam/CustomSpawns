using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using CustomSpawns.AI.Adapters;
using CustomSpawns.AI.Behaviours;
using CustomSpawns.AI.Model;
using CustomSpawns.AI.Offensives;
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
        private PartyStateStore<SettlementOffensiveBehaviourState> _states = null!;

        [Test]
        public void Provider_HandlesTypedRaidConfiguration()
        {
            Assert.That(new RaidClosestVillageBehaviour(
                    new BannerlordRaidClosestVillageAdapter(DummyDynamicSpawnData()),
                    (minimum, _) => minimum).ConfigurationType,
                Is.EqualTo(typeof(RaidClosestVillageData)));
        }

        [Test]
        public void Search_SelectsPatrolAndKeepsProviderApplicable()
        {
            MobileParty party = DummyParty();
            Settlement patrol = DummySettlement();
            var operations = new OperationsStub { PatrolTarget = patrol };
            RaidClosestVillageBehaviour provider = Provider(operations);

            PartyBehaviourResult result = Evaluate(provider, party);

            Assert.That(result.IsApplicable, Is.True);
            Assert.That(result.AIBehavior?.AiBehavior, Is.EqualTo(AiBehavior.PatrolAroundPoint));
            Assert.That(result.AIBehavior?.Party, Is.SameAs(patrol));
            Assert.That(operations.FindRaidTargetCalls, Is.EqualTo(1));
            Assert.That(operations.FindPatrolTargetCalls, Is.EqualTo(1));
            Assert.That(_states.GetOrAdd(party).Phase, Is.EqualTo(SettlementOffensivePhase.Search));
        }

        [Test]
        public void Search_WithoutPatrolRemainsApplicableWithoutBehavior()
        {
            MobileParty party = DummyParty();
            RaidClosestVillageBehaviour provider = Provider(new OperationsStub());

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
            var operations = new OperationsStub
            {
                RaidTarget = target,
                PatrolTarget = patrol,
                VisibleHostileStrength = 200f
            };
            RaidClosestVillageBehaviour provider = Provider(operations);

            PartyBehaviourResult unsafeResult = Evaluate(provider, party);
            operations.VisibleHostileStrength = 0f;
            PartyBehaviourResult safeResult = Evaluate(provider, party);

            Assert.That(unsafeResult.AIBehavior?.AiBehavior, Is.EqualTo(AiBehavior.PatrolAroundPoint));
            Assert.That(_states.GetOrAdd(party).Target, Is.SameAs(target));
            Assert.That(safeResult.AIBehavior?.AiBehavior, Is.EqualTo(AiBehavior.RaidSettlement));
            Assert.That(operations.VisibleHostileStrengthCalls, Is.EqualTo(2));
        }

        [Test]
        public void ConcreteAdapter_UsesEachObserverSeeingRangeAndInclusiveDistanceBoundary()
        {
            MobileParty boundaryObserver = DummyParty();
            MobileParty shortRangeObserver = DummyParty();
            var observations = new[]
            {
                new RoamingPartyStrength(false, true, true, 5f, 20f),
                new RoamingPartyStrength(false, true, true, 5.001f, 100f)
            };
            int rangeReads = 0;
            int observationReads = 0;
            var operations = new BannerlordRaidClosestVillageAdapter(
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

            float atBoundary = operations.GetVisibleHostileStrength(boundaryObserver);
            float outsideShorterObserverRange = operations.GetVisibleHostileStrength(shortRangeObserver);

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
            var operations = new OperationsStub { RaidTarget = target };
            RaidClosestVillageBehaviour provider = Provider(operations);
            Evaluate(provider, party);
            SettlementOffensiveBehaviourState state = _states.GetOrAdd(party);
            state.Phase = SettlementOffensivePhase.Active;
            state.Target = target;
            state.ElapsedOperationDays = 2f;
            operations.MatchingRaidActive = true;

            PartyBehaviourResult result = Evaluate(provider, party);

            Assert.That(result.AIBehavior?.AiBehavior, Is.EqualTo(AiBehavior.RaidSettlement));
            Assert.That(state.Phase, Is.EqualTo(SettlementOffensivePhase.Active));
            Assert.That(state.ElapsedOperationDays, Is.EqualTo(2f));
        }

        [Test]
        public void InterruptedActiveRaid_ImmediatelyRetriesSameTarget()
        {
            MobileParty party = DummyParty();
            Settlement target = DummySettlement();
            var operations = new OperationsStub { RaidTarget = target };
            RaidClosestVillageBehaviour provider = Provider(operations);
            Evaluate(provider, party);
            SettlementOffensiveBehaviourState state = _states.GetOrAdd(party);
            state.Phase = SettlementOffensivePhase.Active;
            state.Target = target;
            state.ElapsedOperationDays = 2f;

            PartyBehaviourResult result = Evaluate(provider, party);

            Assert.That(result.AIBehavior?.AiBehavior, Is.EqualTo(AiBehavior.RaidSettlement));
            Assert.That(result.AIBehavior?.Party, Is.SameAs(target));
            Assert.That(state.Target, Is.SameAs(target));
            Assert.That(state.Phase, Is.EqualTo(SettlementOffensivePhase.Approach));
            Assert.That(state.ElapsedOperationDays, Is.EqualTo(2f + 1f / 24f));
        }

        [Test]
        public void InvalidActiveTarget_RetargetsWithoutFailureAndRaidsReplacement()
        {
            MobileParty party = DummyParty();
            Settlement oldTarget = DummySettlement();
            Settlement replacement = DummySettlement();
            var operations = new OperationsStub { RaidTarget = oldTarget };
            RaidClosestVillageBehaviour provider = Provider(operations);
            Evaluate(provider, party);
            SettlementOffensiveBehaviourState state = _states.GetOrAdd(party);
            state.Phase = SettlementOffensivePhase.Active;
            state.Target = oldTarget;
            operations.CurrentTargetValid = false;
            operations.RaidTarget = replacement;

            PartyBehaviourResult result = Evaluate(provider, party);

            Assert.That(state.Target, Is.SameAs(replacement));
            Assert.That(result.AIBehavior?.AiBehavior, Is.EqualTo(AiBehavior.RaidSettlement));
            Assert.That(result.AIBehavior?.Party, Is.SameAs(replacement));
        }

        [Test]
        public void SyncData_UsesRaidSpecificKeyAndRestoresStateDictionary()
        {
            MobileParty party = DummyParty();
            var loadedState = new SettlementOffensiveBehaviourState
            {
                Phase = SettlementOffensivePhase.Approach
            };
            var loaded = new Dictionary<MobileParty, SettlementOffensiveBehaviourState>
            {
                [party] = loadedState
            };
            var dataStore = new DataStoreStub(loaded);
            RaidClosestVillageBehaviour provider = Provider(new OperationsStub());

            provider.SyncData(dataStore);

            Assert.That(dataStore.SeenKey, Is.EqualTo(RaidClosestVillageBehaviour.SaveKey));
            Assert.That(_states.GetOrAdd(party), Is.SameAs(loadedState));
        }

        [Test]
        public void Cooldown_ReturnsNotApplicableBeforeRuntimeBoundaryEvaluation()
        {
            MobileParty party = DummyParty();
            var operations = new OperationsStub();
            RaidClosestVillageBehaviour provider = Provider(operations);
            Evaluate(provider, party);
            SettlementOffensiveBehaviourState state = _states.GetOrAdd(party);
            state.Phase = SettlementOffensivePhase.Cooldown;
            state.CooldownRemainingDays = 1f;
            int strengthCalls = operations.AttackerStrengthCalls;

            PartyBehaviourResult result = Evaluate(provider, party);

            Assert.That(result.IsApplicable, Is.False);
            Assert.That(result.AIBehavior, Is.Null);
            Assert.That(operations.AttackerStrengthCalls, Is.EqualTo(strengthCalls));
            Assert.That(state.CooldownRemainingDays, Is.EqualTo(1f - 1f / 24f));
        }

        [Test]
        public void TargetRatio_ZeroMilitiaIsAlwaysAQualifiedTarget()
        {
            Assert.That(OffensiveStrengthRules.MeetsStrengthRatio(0f, 0f, float.MaxValue),
                Is.True);
        }

        [Test]
        public void ConcreteAdapter_AddsTheAccompanyingPartiesOfTheAssessedPartyToItsOwnStrength()
        {
            MobileParty party = DummyPartyWithPartyBase();
            MobileParty accompanyingParty = DummyPartyWithPartyBase();
            MobileParty? assessedParty = null;
            var counted = new List<MobileParty>();
            var operations = new BannerlordRaidClosestVillageAdapter(_ => 5f,
                _ => new RoamingPartyStrength[0], observed =>
                {
                    assessedParty = observed;
                    return Counted(accompanyingParty, counted);
                });

            operations.GetAttackerStrength(party);

            Assert.That(assessedParty, Is.SameAs(party));
            Assert.That(counted, Is.EqualTo(new[] { accompanyingParty }));
        }

        [Test]
        public void ConcreteAdapter_ArmyLeaderTakesTheArmyTotalWithoutCountingAccompanyingPartiesAgain()
        {
            MobileParty party = ArmyLeadingParty();
            bool accompanyingPartiesConsulted = false;
            var operations = new BannerlordRaidClosestVillageAdapter(_ => 5f,
                _ => new RoamingPartyStrength[0], _ =>
                {
                    accompanyingPartiesConsulted = true;
                    return new List<MobileParty>();
                });

            operations.GetAttackerStrength(party);

            Assert.That(accompanyingPartiesConsulted, Is.False);
        }

        [Test]
        public void TargetRatio_EscortStrengthAdmitsAVillageTheLeaderPartyAloneWouldSkip()
        {
            Settlement village = DummySettlement();
            OperationsStub withEscorts = StrengthRuledOperations(village, 150f);
            OperationsStub alone = StrengthRuledOperations(village, 50f);

            PartyBehaviourResult escortedResult = Evaluate(Provider(withEscorts), DummyParty());
            PartyBehaviourResult loneResult = Evaluate(Provider(alone), DummyParty());

            Assert.That(withEscorts.FindRaidTargetStrength, Is.EqualTo(150f));
            Assert.That(escortedResult.AIBehavior?.AiBehavior, Is.EqualTo(AiBehavior.RaidSettlement));
            Assert.That(escortedResult.AIBehavior?.Party, Is.SameAs(village));
            Assert.That(alone.FindRaidTargetStrength, Is.EqualTo(50f));
            Assert.That(loneResult.AIBehavior, Is.Null);
        }

        private static OperationsStub StrengthRuledOperations(Settlement village, float attackerStrength)
        {
            const float villageStrength = 60f;
            return new OperationsStub
            {
                AttackerStrength = attackerStrength,
                RaidTarget = village,
                TargetStrength = villageStrength,
                RaidTargetDefenderStrength = villageStrength
            };
        }

        private RaidClosestVillageBehaviour Provider(OperationsStub operations)
        {
            _states = new PartyStateStore<SettlementOffensiveBehaviourState>(RaidClosestVillageBehaviour.SaveKey,
                _ => true);
            return new RaidClosestVillageBehaviour(operations, (minimum, _) => minimum, _states);
        }

        private static PartyBehaviourResult Evaluate(RaidClosestVillageBehaviour provider,
            MobileParty party)
        {
            return provider.GetPriorityBehaviour(party, null!, Configuration());
        }

        private static RaidClosestVillageData Configuration()
        {
            return new RaidClosestVillageData(1f, 1f, 1f, 1f, 2f,
                10f, 10f, 3f);
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

        private sealed class OperationsStub : IRaidOperations
        {
            public float AttackerStrength { get; set; } = 100f;
            public float VisibleHostileStrength { get; set; }
            public Settlement? RaidTarget { get; set; }
            public Settlement? PatrolTarget { get; set; }
            public float TargetStrength { get; set; } = 10f;
            public bool MatchingRaidActive { get; set; }

            /**
             * Opt-in: when set, FindClosestRaidTarget only hands the target back when the real strength
             * rule admits it against this defender strength, instead of returning it unconditionally.
             */
            public float? RaidTargetDefenderStrength { get; set; }
            public bool CurrentTargetValid { get; set; } = true;
            public bool TargetComplete { get; set; }
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
                if (RaidTargetDefenderStrength.HasValue &&
                    !OffensiveStrengthRules.MeetsStrengthRatio(attackerStrength,
                        RaidTargetDefenderStrength.Value, minimumRatio))
                {
                    return null;
                }
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
