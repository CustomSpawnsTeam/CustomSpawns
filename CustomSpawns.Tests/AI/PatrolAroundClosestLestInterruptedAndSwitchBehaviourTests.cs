using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using CustomSpawns.AI;
using CustomSpawns.Data.Adapter;
using CustomSpawns.Data.Dto;
using NUnit.Framework;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.SaveSystem;

namespace CustomSpawns.Tests.AI
{
    public class PatrolAroundClosestLestInterruptedAndSwitchBehaviourTests
    {
        [TestCase(AiBehavior.EngageParty)]
        [TestCase(AiBehavior.GoAroundParty)]
        [TestCase(AiBehavior.JoinParty)]
        [TestCase(AiBehavior.FleeToPoint)]
        public void IsPatrolInterrupted_RecognizesTemporaryPartyActions(AiBehavior behavior)
        {
            Assert.True(PatrolAroundClosestLestInterruptedAndSwitchBehaviour.IsPatrolInterrupted(
                behavior, AiBehavior.None));
            Assert.True(PatrolAroundClosestLestInterruptedAndSwitchBehaviour.IsPatrolInterrupted(
                AiBehavior.None, behavior));
        }

        [Test]
        public void UpdateUninterruptedPatrolDays_ResetsWhenInterrupted()
        {
            Assert.That(PatrolAroundClosestLestInterruptedAndSwitchBehaviour.UpdateUninterruptedPatrolDays(
                3f, true), Is.Zero);
        }

        [Test]
        public void UpdateUninterruptedPatrolDays_AdvancesOneHourWhenUninterrupted()
        {
            Assert.That(PatrolAroundClosestLestInterruptedAndSwitchBehaviour.UpdateUninterruptedPatrolDays(
                1f, false), Is.EqualTo(1f + 1f / 24f));
        }

        [Test]
        public void ShouldSwitchPatrolTarget_RequiresStablePeriodToElapse()
        {
            Assert.False(PatrolAroundClosestLestInterruptedAndSwitchBehaviour.ShouldSwitchPatrolTarget(
                1.99f, 2f, false));
            Assert.True(PatrolAroundClosestLestInterruptedAndSwitchBehaviour.ShouldSwitchPatrolTarget(
                2f, 2f, false));
        }

        [Test]
        public void ShouldSwitchPatrolTarget_DoesNotSwitchWhileInterrupted()
        {
            Assert.False(PatrolAroundClosestLestInterruptedAndSwitchBehaviour.ShouldSwitchPatrolTarget(
                0f, 0f, true));
        }

        [Test]
        public void StablePatrolDays_AreRetainedAcrossRepeatedTicksAndLoadedState()
        {
            int rollCount = 0;
            float loadedStableDays = 3.75f;

            float firstTick = PatrolAroundClosestLestInterruptedAndSwitchBehaviour.RetainOrRollStablePatrolDays(
                true, loadedStableDays, Roll);
            float secondTick = PatrolAroundClosestLestInterruptedAndSwitchBehaviour.RetainOrRollStablePatrolDays(
                true, firstTick, Roll);

            Assert.That(firstTick, Is.EqualTo(loadedStableDays));
            Assert.That(secondTick, Is.EqualTo(loadedStableDays));
            Assert.That(rollCount, Is.Zero);

            float Roll()
            {
                rollCount++;
                return 9f;
            }
        }

        [Test]
        public void SyncData_LoadedStateIsEvaluatedByProductionProviderWithoutRerolling()
        {
            MobileParty party = (MobileParty)FormatterServices.GetUninitializedObject(typeof(MobileParty));
            Settlement target = (Settlement)FormatterServices.GetUninitializedObject(typeof(Settlement));
            var loadedState = new PatrolAroundClosestLestInterruptedAndSwitchBehaviour.PatrolState
            {
                Target = target,
                UninterruptedPatrolDays = 1.25f,
                StablePatrolDays = 3.75f
            };
            var store = new DataStoreStub(new Dictionary<MobileParty,
                PatrolAroundClosestLestInterruptedAndSwitchBehaviour.PatrolState>
            {
                [party] = loadedState
            });
            var provider = new PatrolAroundClosestLestInterruptedAndSwitchBehaviour(
                _ => MobileParty.NavigationType.Default,
                new PartyStateStore<PatrolAroundClosestLestInterruptedAndSwitchBehaviour.PatrolState>(
                    PatrolAroundClosestLestInterruptedAndSwitchBehaviour.SaveKey, _ => true));

            provider.SyncData(store);
            PartyBehaviourResult result = provider.GetPriorityBehaviour(party, null!,
                new PatrolAroundClosestLestInterruptedAndSwitchData(8f, 9f,
                    new List<SpawnSettlementType>()));

            Assert.Multiple(() =>
            {
                Assert.That(store.SeenKey, Is.EqualTo(
                    PatrolAroundClosestLestInterruptedAndSwitchBehaviour.SaveKey));
                Assert.That(result.AIBehavior?.Party, Is.SameAs(target));
                Assert.That(loadedState.Target, Is.SameAs(target));
                Assert.That(loadedState.StablePatrolDays, Is.EqualTo(3.75f));
                Assert.That(loadedState.UninterruptedPatrolDays, Is.EqualTo(1.25f + 1f / 24f));
            });
        }

        [Test]
        public void SyncData_RepairsNullLoadedDictionary()
        {
            var provider = new PatrolAroundClosestLestInterruptedAndSwitchBehaviour();
            provider.SyncData(new DataStoreStub(null, isLoading: true));
            var capture = new DataStoreStub();

            provider.SyncData(capture);

            Assert.That(capture.Captured, Is.InstanceOf<Dictionary<MobileParty,
                PatrolAroundClosestLestInterruptedAndSwitchBehaviour.PatrolState>>());
        }

        [Test]
        public void PatrolState_UsesStableDistinctSaveIds()
        {
            Assert.That(SaveId("_target"), Is.EqualTo(1));
            Assert.That(SaveId("_uninterruptedPatrolDays"), Is.EqualTo(2));
            Assert.That(SaveId("_stablePatrolDays"), Is.EqualTo(3));
        }

        private static short SaveId(string fieldName)
        {
            FieldInfo field = typeof(PatrolAroundClosestLestInterruptedAndSwitchBehaviour.PatrolState)
                .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)!;
            return field.GetCustomAttribute<SaveableFieldAttribute>()!.LocalSaveId;
        }

        private sealed class DataStoreStub : IDataStore
        {
            private readonly object? _loaded;

            public DataStoreStub(object? loaded = null, bool isLoading = false)
            {
                _loaded = loaded;
                IsLoading = isLoading || loaded is not null;
            }

            public bool IsSaving => !IsLoading;
            public bool IsLoading { get; }
            public string? SeenKey { get; private set; }
            public object? Captured { get; private set; }

            public bool SyncData<T>(string key, ref T data)
            {
                SeenKey = key;
                if (IsLoading)
                {
                    data = (T)_loaded!;
                }
                Captured = data;
                return true;
            }
        }
    }
}
