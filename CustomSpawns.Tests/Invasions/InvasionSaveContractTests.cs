using System;
using System.Linq;
using System.Reflection;
using CustomSpawns.Invasions;
using CustomSpawns.Invasions.Adapters;
using CustomSpawns.Spawn.PartyComponents;
using NUnit.Framework;
using TaleWorlds.SaveSystem;
using TaleWorlds.SaveSystem.Definition;

namespace CustomSpawns.Tests.Invasions
{
    public class InvasionSaveContractTests
    {
        [TestCase(typeof(InvasionSaveData), 6)]
        [TestCase(typeof(InvasionGroupSaveData), 7)]
        [TestCase(typeof(InvasionCaptureSaveData), 8)]
        [TestCase(typeof(InvasionCreditSaveData), 9)]
        [TestCase(typeof(InvasionCaptureEvidenceSaveData), 10)]
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
            foreach (Type type in new[] { typeof(InvasionState), typeof(InvasionGroupState), typeof(InvasionTownCredit), typeof(InvasionCaptureContext) })
            foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                Assert.IsNull(field.GetCustomAttribute<SaveableFieldAttribute>(), type.Name + "." + field.Name);
                Assert.False(field.FieldType.FullName?.Contains("TaleWorlds") == true);
            }
        }

        [TestCase(typeof(InvasionSaveData))]
        [TestCase(typeof(InvasionGroupSaveData))]
        [TestCase(typeof(InvasionCaptureSaveData))]
        [TestCase(typeof(InvasionCreditSaveData))]
        [TestCase(typeof(InvasionCaptureEvidenceSaveData))]
        public void BoundarySnapshotFieldsAllHaveDistinctSaveIds(Type type)
        {
            var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public);
            var attributes = fields.Select(f => f.GetCustomAttribute<SaveableFieldAttribute>()).ToList();
            Assert.That(attributes, Has.None.Null);
            Assert.AreEqual(fields.Length, attributes.Select(a => a!.LocalSaveId).Distinct().Count());
        }
    }
}
