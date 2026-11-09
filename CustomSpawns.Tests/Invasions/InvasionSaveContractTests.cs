using System;
using System.Linq;
using System.Reflection;
using CustomSpawns.Invasions;
using CustomSpawns.Invasions.Adapters;
using CustomSpawns.Invasions.CapitalObjectives;
using CustomSpawns.Spawn.PartyComponents;
using NUnit.Framework;
using TaleWorlds.SaveSystem;
using TaleWorlds.SaveSystem.Definition;

namespace CustomSpawns.Tests.Invasions
{
    public class InvasionSaveContractTests
    {
        [TestCase(typeof(InvasionStateSaveData), 6)]
        [TestCase(typeof(InvasionSaveData), 7)]
        [TestCase(typeof(InvasionCaptureSaveData), 8)]
        [TestCase(typeof(InvasionFootholdSaveData), 9)]
        [TestCase(typeof(InvasionCaptureEvidenceSaveData), 10)]
        [TestCase(typeof(InvasionReinforcementScheduleSaveData), 11)]
        [TestCase(typeof(InvasionCapitalObjectiveSaveData), 12)]
        public void SaveTypesAppendAfterExistingIds(Type type, int localId)
        {
            var context = new DefinitionContext();
            var definer = new CustomSpawnsTypeDefiner();
            typeof(SaveableTypeDefiner).GetMethod("Initialize", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(definer, new object[] { context });
            typeof(CustomSpawnsTypeDefiner).GetMethod("DefineClassTypes", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(definer, null);
            var method = typeof(DefinitionContext).GetMethod("GetClassDefinition",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(Type) }, null)!;
            var definition = (TypeDefinitionBase)method.Invoke(context, new object[] { type })!;
            Assert.AreEqual(7_955_635 + localId, ((TypeSaveId)definition.SaveId).Id);
        }

        [Test]
        public void PureStateHasNoSaveAttributesOrEngineFields()
        {
            foreach (Type type in new[] { typeof(InvasionState), typeof(Invasion), typeof(InvasionFoothold), typeof(InvasionCapture), typeof(InvasionReinforcementSchedule) })
            foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                Assert.IsNull(field.GetCustomAttribute<SaveableFieldAttribute>(), type.Name + "." + field.Name);
                Assert.False(field.FieldType.FullName?.Contains("TaleWorlds") == true);
            }
        }

        [TestCase(typeof(InvasionStateSaveData))]
        [TestCase(typeof(InvasionSaveData))]
        [TestCase(typeof(InvasionCaptureSaveData))]
        [TestCase(typeof(InvasionFootholdSaveData))]
        [TestCase(typeof(InvasionCaptureEvidenceSaveData))]
        [TestCase(typeof(InvasionReinforcementScheduleSaveData))]
        [TestCase(typeof(InvasionCapitalObjectiveSaveData))]
        public void BoundarySnapshotFieldsAllHaveDistinctSaveIds(Type type)
        {
            var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public);
            var attributes = fields.Select(f => f.GetCustomAttribute<SaveableFieldAttribute>()).ToList();
            Assert.That(attributes, Has.None.Null);
            Assert.AreEqual(fields.Length, attributes.Select(a => a!.LocalSaveId).Distinct().Count());
        }

        [Test]
        public void CapitalObjectiveArmyMetadataSurvivesSaveRoundTrip()
        {
            var objective = new InvasionCapitalObjective(InvasionCapitalObjectiveKind.Attack,
                "capital", 10d);
            objective.MarkMobilized(new CapitalArmyMobilizationResult(true, "leader",
                new[] { "leader", "member" }, "staging"), 11d);
            objective.MarkReady();

            InvasionCapitalObjective restored =
                InvasionCapitalObjectiveSaveData.FromState(objective).ToState()!;

            Assert.That(restored.Phase, Is.EqualTo(InvasionCapitalObjectivePhase.Advancing));
            Assert.That(restored.CommanderPartyId, Is.EqualTo("leader"));
            Assert.That(restored.StagingSettlementId, Is.EqualTo("staging"));
            Assert.That(restored.MobilizedPartyIds, Is.EqualTo(new[] { "leader", "member" }));
            Assert.That(restored.AttackDeadlineDays, Is.EqualTo(17d));
        }

        [Test]
        public void ObservedCapitalSiegeWithoutObjectiveArmySurvivesSaveRoundTrip()
        {
            var objective = new InvasionCapitalObjective(InvasionCapitalObjectiveKind.Attack,
                "capital", 10d);
            objective.MarkBesieging();

            InvasionCapitalObjective restored =
                InvasionCapitalObjectiveSaveData.FromState(objective).ToState()!;

            Assert.That(restored.Phase, Is.EqualTo(InvasionCapitalObjectivePhase.Besieging));
            Assert.That(restored.TargetSettlementId, Is.EqualTo("capital"));
            Assert.That(restored.AttackDeadlineDays, Is.EqualTo(17d));
            Assert.That(restored.CommanderPartyId, Is.Null);
            Assert.That(restored.MobilizedPartyIds, Is.Empty);
        }

        [Test]
        public void LegacyActiveObjectiveWithoutArmyMetadataRestoresForRemobilization()
        {
            var saved = new InvasionCapitalObjectiveSaveData
            {
                Kind = (int)InvasionCapitalObjectiveKind.Attack,
                Phase = (int)InvasionCapitalObjectivePhase.Gathering,
                TargetSettlementId = "capital",
                GatherDeadlineDays = 13d,
                AttackDeadlineDays = 17d,
                CommanderPartyId = "legacy-leader"
            };

            InvasionCapitalObjective restored = saved.ToState()!;

            Assert.That(restored.Phase, Is.EqualTo(InvasionCapitalObjectivePhase.Mobilizing));
            Assert.That(restored.CommanderPartyId, Is.Null);
            Assert.That(restored.StagingSettlementId, Is.Null);
            Assert.That(restored.MobilizedPartyIds, Is.Empty);
            Assert.That(restored.GatherDeadlineDays, Is.Null);
            Assert.That(restored.AttackDeadlineDays, Is.EqualTo(17d));
        }
    }
}
