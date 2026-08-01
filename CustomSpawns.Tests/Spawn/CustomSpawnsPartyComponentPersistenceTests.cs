using System.Collections.Generic;
using System.Reflection;
using CustomSpawns.Data.Dao;
using CustomSpawns.Spawn;
using CustomSpawns.Spawn.PartyComponents;
using NUnit.Framework;
using TaleWorlds.SaveSystem;

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
        }

        [Test]
        public void TakeFromRegistry_OnlyReturnsThePartyDataOnce()
        {
            CsPartyData partyData = new CsPartyData("primary", null);
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
            var spawnCounts = new Dictionary<string, int> { ["primary"] = 2 };
            CsPartyData primary = new CsPartyData("primary", null);
            CsPartyData accompanying = new CsPartyData("supporting", null);

            Assert.True(SpawnBehaviour.TryDecrementPrimarySpawnCount(spawnCounts, primary));
            Assert.False(SpawnBehaviour.TryDecrementPrimarySpawnCount(spawnCounts, accompanying));
            Assert.AreEqual(1, spawnCounts["primary"]);
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
            FieldInfo field = typeof(CustomSpawnsPartyComponent).GetField(fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic)!;
            return field.GetCustomAttribute<SaveableFieldAttribute>()!.LocalSaveId;
        }
    }
}
