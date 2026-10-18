using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using CustomSpawns.Data.Dao;
using CustomSpawns.Invasions;
using CustomSpawns.Invasions.Adapters;
using CustomSpawns.Invasions.CapitalObjectives;
using CustomSpawns.Spawn;
using CustomSpawns.UtilityBehaviours;
using Moq;
using NUnit.Framework;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

namespace CustomSpawns.Tests.Invasions
{
    public class InvasionCampaignBehaviourLoadTests
    {
        private Campaign? _previousCampaign;

        [SetUp]
        public void SetUp()
        {
            _previousCampaign = Campaign.Current;
        }

        [TearDown]
        public void TearDown()
        {
            SetCurrentCampaign(_previousCampaign);
        }

        [Test]
        public void LoadedCapitalObjective_IsReconciledOnceOnTheFirstCampaignTick()
        {
            InvasionCampaignBehaviour behaviour = Behaviour(out Action firstCampaignTick);
            var configurations = new Mock<IInvasionAssaultConfigurationProvider>();
            behaviour.ConfigureCapitalObjectives(configurations.Object);

            behaviour.SyncData(new LoadingDataStore(SavedStateWithCapitalObjective()));

            Assert.That(behaviour.State.FindInvasion("clan")?.CapitalObjective, Is.Not.Null);
            configurations.Verify(provider => provider.Find(It.IsAny<string>()), Times.Never);

            // Another event may initialize the behaviour before SaveInitialiser reaches its callback.
            SetPrivateField(behaviour, "_initialized", true);
            SetPrivateField(behaviour, "_invasionCapitalObjectiveWorld", null);
            UseEmptyCampaign();

            firstCampaignTick();

            Assert.That(behaviour.State.FindInvasion("clan")?.CapitalObjective, Is.Null);
            int configurationReadsAfterFirstTick = configurations.Invocations.Count;
            Assert.That(configurationReadsAfterFirstTick, Is.GreaterThan(0));

            firstCampaignTick();

            Assert.That(configurations.Invocations.Count, Is.EqualTo(configurationReadsAfterFirstTick));
        }

        private static InvasionCampaignBehaviour Behaviour(out Action firstCampaignTick)
        {
            var spawnDao = new SpawnDao(null!, null!, null!);
            var saveInitialiser = new SaveInitialiser();
            var dynamicSpawnData = new DynamicSpawnData(spawnDao, saveInitialiser);
            var behaviour = new InvasionCampaignBehaviour(spawnDao, dynamicSpawnData,
                saveInitialiser, new Mock<IInvasionCapitalRepository>().Object,
                new Mock<IInvasionClanRepository>().Object);

            var actions = (List<Action>)typeof(SaveInitialiser).GetField("_actions",
                BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(saveInitialiser)!;
            firstCampaignTick = actions[actions.Count - 1];

            return behaviour;
        }

        private static InvasionStateSaveData SavedStateWithCapitalObjective() => new()
        {
            Invasions = new List<InvasionSaveData>
            {
                new()
                {
                    SpawnTemplateId = "spawn",
                    ClanId = "clan",
                    CapitalSettlementId = "capital",
                    CapitalObjective = new InvasionCapitalObjectiveSaveData
                    {
                        Kind = (int)InvasionCapitalObjectiveKind.Attack,
                        Phase = (int)InvasionCapitalObjectivePhase.Advancing,
                        TargetSettlementId = "capital",
                        AttackDeadlineDays = 7d
                    }
                }
            }
        };

        private static void SetPrivateField(object target, string fieldName, object? value)
        {
            target.GetType().GetField(fieldName,
                    BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(target, value);
        }

        private static void UseEmptyCampaign()
        {
            var objects = (CampaignObjectManager)FormatterServices.GetUninitializedObject(
                typeof(CampaignObjectManager));
            SetPrivateField(objects, "_clans", new MBList<Clan>());
            var campaign = (Campaign)FormatterServices.GetUninitializedObject(typeof(Campaign));
            SetPrivateField(campaign, "<CampaignObjectManager>k__BackingField", objects);
            SetCurrentCampaign(campaign);
        }

        private static void SetCurrentCampaign(Campaign? campaign) =>
            typeof(Campaign).GetField("<Current>k__BackingField",
                    BindingFlags.Static | BindingFlags.NonPublic)!
                .SetValue(null, campaign);

        private sealed class LoadingDataStore : IDataStore
        {
            private readonly InvasionStateSaveData _savedState;

            public LoadingDataStore(InvasionStateSaveData savedState)
            {
                _savedState = savedState;
            }

            public bool IsSaving => false;
            public bool IsLoading => true;

            public bool SyncData<T>(string key, ref T data)
            {
                if (key == "_invasionState")
                {
                    data = (T)(object)_savedState;
                }
                else if (key == "_invasionResupplyParties")
                {
                    data = (T)(object)new List<string>();
                }

                return true;
            }
        }
    }
}
