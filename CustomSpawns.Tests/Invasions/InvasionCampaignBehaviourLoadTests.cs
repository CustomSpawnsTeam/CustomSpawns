using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using CustomSpawns.AI.Offensives;
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

        [TestCase(InvasionCapitalObjectiveKind.Defence, false)]
        [TestCase(InvasionCapitalObjectiveKind.Defence, true)]
        [TestCase(InvasionCapitalObjectiveKind.Attack, false)]
        [TestCase(InvasionCapitalObjectiveKind.Attack, true)]
        public void LoadedCapitalObjective_RestoresVillageGatheringBeforeAdvancing(
            InvasionCapitalObjectiveKind kind, bool alreadyGathering)
        {
            InvasionCampaignBehaviour behaviour = Behaviour(out Action firstCampaignTick);
            var configurations = new Mock<IInvasionAssaultConfigurationProvider>();
            configurations.Setup(provider => provider.Find("spawn")).Returns(
                new InvasionAssaultRequirements(new[] { SiegeTargetKind.Town }, 1.5f, 1.2f,
                    false, InvasionSupplyLineKind.Coastal));
            behaviour.ConfigureCapitalObjectives(configurations.Object);
            InvasionStateSaveData saved = SavedStateWithCapitalObjective();
            if (alreadyGathering)
            {
                var objective = new InvasionCapitalObjective(kind, "capital", 0d,
                    kind == InvasionCapitalObjectiveKind.Defence ? "capital-siege" : null);
                objective.MarkMobilized(new CapitalArmyMobilizationResult(true, "commander",
                    new[] { "commander", "follower" }, "castle-village"), 0d);
                saved.Invasions[0].CapitalObjective = InvasionCapitalObjectiveSaveData.FromState(objective);
            }

            behaviour.SyncData(new LoadingDataStore(saved));

            var world = new Mock<IInvasionCapitalObjectiveWorld>();
            CapitalObjectiveObservation observation = GatheringObservation(kind, 10f);
            world.Setup(value => value.Observe("clan", "capital", It.IsAny<string?>(),
                    It.IsAny<string?>())).Returns(() => observation);
            world.Setup(value => value.FindMobilizationCandidates("clan")).Returns(new[]
            {
                new CapitalArmyPartyCandidate("commander", 100f, true, true, true),
                new CapitalArmyPartyCandidate("follower", 50f, true, false, false)
            });
            world.Setup(value => value.MobilizeArmy(It.IsAny<CapitalArmyMobilizationPlan>()))
                .Returns(new CapitalArmyMobilizationResult(true, "commander",
                    new[] { "commander", "follower" }, "castle-village"));
            world.Setup(value => value.InviteToArmy("clan", "commander",
                    It.IsAny<IReadOnlyList<string>>()))
                .Returns((string _, string _, IReadOnlyList<string> partyIds) => partyIds);
            var ratios = new ForceRatioRules();
            var coordinator = new InvasionCapitalObjectiveCoordinator(configurations.Object,
                world.Object, new CapitalObjectiveStrengthRules(ratios),
                new CapitalArmyMobilizationPlanner(), new SiegeTargetSelector(ratios),
                new InvasionCapitalSuccession());

            SetPrivateField(behaviour, "_initialized", true);
            SetPrivateField(behaviour, "_invasionCapitalObjectiveWorld", null);
            SetPrivateField(behaviour, "_invasionCapitalObjectiveCoordinator", coordinator);
            UseEmptyCampaign();

            firstCampaignTick();

            Invasion invasion = behaviour.State.FindInvasion("clan")!;
            Assert.That(invasion.CapitalObjective!.Kind, Is.EqualTo(kind));
            Assert.That(invasion.CapitalObjective.Phase,
                Is.EqualTo(InvasionCapitalObjectivePhase.Gathering));
            Assert.That(invasion.CapitalObjective.StagingSettlementId, Is.EqualTo("castle-village"));
            world.Verify(value => value.MobilizeArmy(It.Is<CapitalArmyMobilizationPlan>(plan =>
                plan.ObjectiveKind == kind &&
                plan.TargetSettlementId == "capital")), alreadyGathering ? Times.Never() : Times.Once());
            world.Verify(value => value.InviteToArmy("clan", "commander",
                It.Is<IReadOnlyList<string>>(ids => ids.Count == 0)), Times.Once);
            world.Verify(value => value.BeginAdvance(It.IsAny<string>()), Times.Never);

            firstCampaignTick();
            world.Verify(value => value.MobilizeArmy(It.IsAny<CapitalArmyMobilizationPlan>()),
                alreadyGathering ? Times.Never() : Times.Once());
            world.Verify(value => value.InviteToArmy("clan", "commander",
                It.IsAny<IReadOnlyList<string>>()), Times.Once);

            observation = GatheringObservation(kind, 150f);
            coordinator.UpdateHourly(invasion);

            Assert.That(invasion.CapitalObjective.Phase,
                Is.EqualTo(InvasionCapitalObjectivePhase.Advancing));
            world.Verify(value => value.BeginAdvance("commander"), Times.Once);
        }

        private static CapitalObjectiveObservation GatheringObservation(
            InvasionCapitalObjectiveKind kind, float assembled)
        {
            bool defence = kind == InvasionCapitalObjectiveKind.Defence;
            return new CapitalObjectiveObservation(1d, defence, defence, true, false, defence,
                new CapitalDefenceForces(assembled, 0f, 100f),
                new CapitalAttackForces(assembled, 100f, 50f),
                defence ? "capital-siege" : null);
        }

        private static InvasionCampaignBehaviour Behaviour(out Action firstCampaignTick)
        {
            var spawnDao = new SpawnDao(null!, null!, null!);
            var saveInitialiser = new SaveInitialiser();
            var dynamicSpawnData = new DynamicSpawnData(spawnDao, saveInitialiser);
            var behaviour = new InvasionCampaignBehaviour(spawnDao, dynamicSpawnData,
                saveInitialiser, new Mock<IInvasionCapitalRepository>().Object,
                new Mock<IInvasionClanRepository>().Object, new SpawnGroupService(null!, dynamicSpawnData),
                new Dictionary<string, CustomSpawns.Data.Model.Diplomacy>());

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
