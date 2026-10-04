using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using CustomSpawns.Data.Dao;
using CustomSpawns.Data.Dto;
using CustomSpawns.Spawn;
using CustomSpawns.Spawn.PartySize;
using Moq;
using NUnit.Framework;
using TaleWorlds.CampaignSystem.Party;

namespace CustomSpawns.Tests.Spawn
{
    public class PartySizeResizerRecognitionTests
    {
        [Test]
        public void APartySpawnedFromATemplateIsRecognisedByItsName()
        {
            Resizer(new Dictionary<MobileParty, CsPartyData>());

            Assert.True(PartySizeResizerOnFirstCalculation.IsCustomSpawnsParty(DummyParty("invasion_party_1")));
            Assert.True(PartySizeResizerOnFirstCalculation.IsCustomSpawnsParty(DummyParty("escort_party_1")));
            Assert.True(PartySizeResizerOnFirstCalculation.IsCustomSpawnsParty(DummyParty("reinforcement_party_1")));
        }

        [Test]
        public void AnAdoptedPartyIsRecognisedDespiteItsForeignName()
        {
            MobileParty adopted = DummyParty("lord_hero_001_party_1");
            Resizer(new Dictionary<MobileParty, CsPartyData>
            {
                [adopted] = new("invasion", "invasion", false, null, null)
            });

            Assert.True(PartySizeResizerOnFirstCalculation.IsCustomSpawnsParty(adopted));
        }

        [Test]
        public void APartyThisModNeitherSpawnedNorAdoptedIsLeftAlone()
        {
            Resizer(new Dictionary<MobileParty, CsPartyData>());

            Assert.False(PartySizeResizerOnFirstCalculation.IsCustomSpawnsParty(DummyParty("lord_hero_001_party_1")));
        }

        private static void Resizer(Dictionary<MobileParty, CsPartyData> registry)
        {
            var spawnDao = new Mock<ISpawnDao>();
            spawnDao.Setup(dao => dao.FindAllPartyTemplateId()).Returns(new HashSet<string> { "invasion_party" });
            spawnDao.Setup(dao => dao.FindAllSubPartyTemplateId()).Returns(new HashSet<string> { "escort_party" });
            spawnDao.Setup(dao => dao.FindAll()).Returns(new List<SpawnDto>
            {
                new()
                {
                    Invasion = new InvasionData(false, 0, 0, new[]
                    {
                        new InvasionReinforcementRuleData("infantry", "reinforcement_party", 7, 1)
                    })
                }
            });

            // The resizer keeps its collaborators statically: constructing it is how they are installed.
            _ = new PartySizeResizerOnFirstCalculation(spawnDao.Object, DynamicSpawnData(registry));
        }

        private static DynamicSpawnData DynamicSpawnData(Dictionary<MobileParty, CsPartyData> registry)
        {
            var dynamicSpawnData = (DynamicSpawnData)FormatterServices.GetUninitializedObject(typeof(DynamicSpawnData));
            typeof(DynamicSpawnData).GetField("_dynamicSpawnData", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(dynamicSpawnData, registry);
            return dynamicSpawnData;
        }

        private static MobileParty DummyParty(string stringId)
        {
            var party = (MobileParty)FormatterServices.GetUninitializedObject(typeof(MobileParty));
            party.StringId = stringId;
            return party;
        }
    }
}
