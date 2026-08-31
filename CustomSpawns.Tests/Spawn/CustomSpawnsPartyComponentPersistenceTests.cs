using System.Collections.Generic;
using System.Reflection;
using CustomSpawns.AI;
using CustomSpawns.AI.Model;
using CustomSpawns.Data.Dao;
using CustomSpawns.Spawn;
using CustomSpawns.Spawn.PartyComponents;
using NUnit.Framework;
using TaleWorlds.SaveSystem;
using TaleWorlds.SaveSystem.Definition;

namespace CustomSpawns.Tests.Spawn
{
    public class CustomSpawnsPartyComponentPersistenceTests
    {
        [Test]
        public void PersistedState_UsesStableDistinctSaveIds()
        {
            Assert.AreEqual(1, SaveId("_partyOwner"));
            Assert.AreEqual(2, SaveId("_name"));
            Assert.AreEqual(3, SaveId("_homeSettlement"));
            Assert.AreEqual(4, SaveId("_leader"));
        }

        [Test]
        public void TakeFromRegistry_OnlyReturnsThePartyDataOnce()
        {
            CsPartyData partyData = new CsPartyData("primary", "primary", true, null);
            var registry = new Dictionary<string, CsPartyData> { ["party"] = partyData };
            var spawnCounts = new Dictionary<string, int> { ["primary"] = 1 };

            CsPartyData? firstRemoval = DynamicSpawnData.TakeFromRegistry(registry, "party");
            Assert.AreSame(partyData, firstRemoval);
            Assert.True(SpawnBehaviour.TryDecrementPrimarySpawnCount(spawnCounts, firstRemoval!));

            CsPartyData? secondRemoval = DynamicSpawnData.TakeFromRegistry(registry, "party");
            Assert.IsNull(secondRemoval);
            Assert.AreEqual(0, spawnCounts["primary"]);
            Assert.That(registry, Is.Empty);
        }

        [Test]
        public void RemovalAccounting_DecrementsOnlyPrimaryTemplatesOnce()
        {
            var spawnCounts = new Dictionary<string, int> { ["primary"] = 2, ["reused"] = 4 };
            CsPartyData primary = new CsPartyData("primary", "primary", true, null);
            CsPartyData accompanying = new CsPartyData("reused", "primary", false, null);

            Assert.True(SpawnBehaviour.TryDecrementPrimarySpawnCount(spawnCounts, primary));
            Assert.False(SpawnBehaviour.TryDecrementPrimarySpawnCount(spawnCounts, accompanying));
            Assert.AreEqual(1, spawnCounts["primary"]);
            Assert.AreEqual(4, spawnCounts["reused"]);
        }

        [Test]
        public void DeathMessage_SuppressesOnlyReinforcementsAndPersistingOriginalInvasions()
        {
            CsPartyData original = new CsPartyData("primary", "primary", true, null);
            CsPartyData escort = new CsPartyData("escort", "primary", false, null, null, "Escort");
            CsPartyData reinforcement = new CsPartyData("reinforcement", "primary", false, null, null,
                "Reinforcement");

            Assert.True(SpawnBehaviour.ShouldSuppressDeathMessage(original, _ => true));
            Assert.False(SpawnBehaviour.ShouldSuppressDeathMessage(original, _ => false));
            Assert.False(SpawnBehaviour.ShouldSuppressDeathMessage(escort, _ => true));
            Assert.True(SpawnBehaviour.ShouldSuppressDeathMessage(reinforcement, _ => false));
        }

        [Test]
        public void PrimaryIdentity_RestoresAndRemovesTheOwningSpawnCounter()
        {
            var spawnCounts = new Dictionary<string, int> { ["primary"] = 0 };
            CsPartyData loadedPrimary = new CsPartyData("primary", "primary", true, null);
            CsPartyData loadedSupporting = new CsPartyData("supporting", "primary", false, null);

            Assert.True(SpawnBehaviour.TryIncrementPrimarySpawnCount(spawnCounts, loadedPrimary));
            Assert.False(SpawnBehaviour.TryIncrementPrimarySpawnCount(spawnCounts, loadedSupporting));
            Assert.AreEqual(1, spawnCounts["primary"]);
            Assert.True(SpawnBehaviour.TryDecrementPrimarySpawnCount(spawnCounts, loadedPrimary));
            Assert.AreEqual(0, spawnCounts["primary"]);
        }

        [Test]
        public void DynamicPartyIdentity_UsesStableDistinctSaveIds()
        {
            Assert.AreEqual(1, SaveId<CsPartyData>("_partyTemplateId"));
            Assert.AreEqual(2, SaveId<CsPartyData>("_owningSpawnTemplateId"));
            Assert.AreEqual(3, SaveId<CsPartyData>("_isPrimarySpawn"));
            Assert.AreEqual(4, SaveId<CsPartyData>("_latestClosestSettlement"));
            Assert.AreEqual(5, SaveId<CsPartyData>("_supportedParty"));
            Assert.AreEqual(8, SaveId<CsPartyData>("_partyRole"));
        }

        [Test]
        public void TypeDefiner_RegistersSettlementOffensivePhaseEnumWithUniqueId()
        {
            var context = new DefinitionContext();
            var definer = new CustomSpawnsTypeDefiner();
            typeof(SaveableTypeDefiner).GetMethod("Initialize",
                    BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(definer, new object[] { context });
            typeof(CustomSpawnsTypeDefiner).GetMethod("DefineEnumTypes",
                    BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(definer, null);

            object? definition = typeof(DefinitionContext).GetMethod("GetEnumDefinition",
                    BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(context, new object[] { typeof(SettlementOffensivePhase) });

            Assert.That(definition, Is.InstanceOf<TypeDefinitionBase>());
            TypeDefinitionBase typeDefinition = (TypeDefinitionBase)definition!;
            Assert.That(typeDefinition.Type, Is.EqualTo(typeof(SettlementOffensivePhase)));
            Assert.That(((TypeSaveId)typeDefinition.SaveId).Id, Is.EqualTo(7_955_640));
        }

        [Test]
        public void LegacyDiscovery_SharedPrimaryAndSupportingTemplateIsNotAssumedPrimary()
        {
            CsPartyData partyData = DynamicSpawnData.CreateDiscoveredPartyData("reused", null,
                isPrimaryTemplate: true, isSupportingTemplate: true, latestClosestSettlement: null);
            var spawnCounts = new Dictionary<string, int> { ["reused"] = 3 };

            Assert.False(partyData.IsPrimarySpawn);
            Assert.False(SpawnBehaviour.TryDecrementPrimarySpawnCount(spawnCounts, partyData));
            Assert.AreEqual(3, spawnCounts["reused"]);
        }

        [Test]
        public void ParentLookup_ReturnsTheUniqueParentAndRejectsAmbiguity()
        {
            object uniqueParent = new object();
            Assert.AreSame(uniqueParent, SpawnDao.FindUniqueParent(new[] { uniqueParent }, out bool uniqueIsAmbiguous));
            Assert.False(uniqueIsAmbiguous);

            Assert.IsNull(SpawnDao.FindUniqueParent(new[] { new object(), new object() }, out bool ambiguous));
            Assert.True(ambiguous);
        }

        [Test]
        public void ResolveSpawnClan_UsesTheCurrentSettlementOwnerOnlyWhenConfigured()
        {
            object configuredClan = new object();
            object formerSettlementOwner = new object();
            object currentRebelClan = new object();

            Assert.AreSame(currentRebelClan, SpawnBehaviour.ResolveSpawnClan(true, configuredClan, currentRebelClan));
            Assert.AreSame(configuredClan, SpawnBehaviour.ResolveSpawnClan(false, configuredClan, formerSettlementOwner));
        }

        [Test]
        public void LegacyRepair_WithNoRecoverableState_SkipsClanLifecycle()
        {
            CustomSpawnsPartyComponent.LegacyPartyState state = CustomSpawnsPartyComponent.RepairLegacyState(
                null, null, null, null, null, null, null);

            Assert.IsNull(state.PartyOwner);
            Assert.IsNull(state.Name);
            Assert.IsNull(state.HomeSettlement);
            Assert.False(CustomSpawnsPartyComponent.ShouldRegisterWithClan(null));
            Assert.False(CustomSpawnsPartyComponent.ShouldFinalizeWithClan(false));
        }

        private static short SaveId(string fieldName)
        {
            return SaveId<CustomSpawnsPartyComponent>(fieldName);
        }

        private static short SaveId<T>(string fieldName)
        {
            FieldInfo field = typeof(T).GetField(fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic)!;
            return field.GetCustomAttribute<SaveableFieldAttribute>()!.LocalSaveId;
        }
    }
}
