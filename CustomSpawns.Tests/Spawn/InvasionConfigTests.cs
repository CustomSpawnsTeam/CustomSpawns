using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CustomSpawns.Data.Adapter;
using CustomSpawns.Data.Dto;
using CustomSpawns.Data.Model;
using CustomSpawns.Data.Reader.Impl;
using NUnit.Framework;

namespace CustomSpawns.Tests.Spawn
{
    public class InvasionConfigTests
    {
        [Test]
        public void DeserializeAndResolve_LegacySpawnWithoutInvasionRetainsNullInvasion()
        {
            Spawns spawns = Read("<RegularBanditDailySpawnData party_template=\"PartyTemplate.legacy\" />", out List<System.Exception> errors);

            Assert.Multiple(() =>
            {
                Assert.That(errors, Is.Empty);
                Assert.That(spawns.AllSpawns, Has.Count.EqualTo(1));
                Assert.That(spawns.AllSpawns[0].Invasion, Is.Null);
            });
        }

        [Test]
        public void Adapt_ValidInvasionPreservesConfigurationAndDefaultSevenDayInterval()
        {
            const string invasion = "<Invasion><Supplies><FoodStockFloor>30</FoodStockFloor><DailyFoodBonus>5</DailyFoodBonus>" +
                                    "</Supplies><OccupationGarrison><MinimumTroopCount>10</MinimumTroopCount>" +
                                    "<MaximumTroopCount>25</MaximumTroopCount><MilitiaStrengthRatio>1.5</MilitiaStrengthRatio>" +
                                    "<MaximumDonorLossFraction>0.25</MaximumDonorLossFraction></OccupationGarrison>" +
                                    "<Reinforcements><PartyTemplateId>PartyTemplate.reinforcements</PartyTemplateId>" +
                                    "<ActivePartyCap>2</ActivePartyCap></Reinforcements></Invasion>";
            Spawns spawns = Read("<RegularBanditDailySpawnData party_template=\"PartyTemplate.invasion\">" + invasion +
                                 "</RegularBanditDailySpawnData>", out List<System.Exception> errors);

            InvasionData data = InvasionDtoAdapter.Adapt(spawns.AllSpawns.Single().Invasion!);

            Assert.Multiple(() =>
            {
                Assert.That(errors, Is.Empty);
                Assert.That(data.FoodStockFloor, Is.EqualTo(30f));
                Assert.That(data.DailyFoodBonus, Is.EqualTo(5f));
                Assert.That(data.MinimumGarrisonTroopCount, Is.EqualTo(10));
                Assert.That(data.MaximumGarrisonTroopCount, Is.EqualTo(25));
                Assert.That(data.MilitiaStrengthRatio, Is.EqualTo(1.5f));
                Assert.That(data.MaximumDonorLossFraction, Is.EqualTo(0.25f));
                Assert.That(data.ReinforcementPartyTemplateId, Is.EqualTo("PartyTemplate.reinforcements"));
                Assert.That(data.ReinforcementIntervalInDays, Is.EqualTo(7f));
                Assert.That(data.ReinforcementActivePartyCap, Is.EqualTo(2));
            });
        }

        [Test]
        public void DeserializeAndResolve_LegacyCoastalTownElementsAreIgnored()
        {
            const string invasion = "<Invasion><Foothold><CoastalTownIds><TownId>town_V7</TownId>" +
                                    "</CoastalTownIds></Foothold><Reinforcements>" +
                                    "<PartyTemplateId>PartyTemplate.reinforcements</PartyTemplateId>" +
                                    "</Reinforcements></Invasion>";
            Spawns spawns = Read("<RegularBanditDailySpawnData party_template=\"PartyTemplate.invasion\">" +
                                 invasion + "</RegularBanditDailySpawnData>", out List<System.Exception> errors);

            InvasionData data = InvasionDtoAdapter.Adapt(spawns.AllSpawns.Single().Invasion!);

            Assert.Multiple(() =>
            {
                Assert.That(errors, Is.Empty);
                Assert.That(data.ReinforcementPartyTemplateId, Is.EqualTo("PartyTemplate.reinforcements"));
                Assert.That(data.ReinforcementIntervalInDays, Is.EqualTo(7f));
            });
        }

        [Test]
        public void DeserializeAndResolve_InvalidInvasionReportsItsSpawnAndKeepsUnrelatedSpawns()
        {
            const string invalid = "<RegularBanditDailySpawnData party_template=\"PartyTemplate.invalid\"><Invasion>" +
                                   "<OccupationGarrison><MinimumTroopCount>3</MinimumTroopCount>" +
                                   "<MaximumTroopCount>2</MaximumTroopCount></OccupationGarrison>" +
                                   "<Reinforcements><PartyTemplateId>PartyTemplate.reinforcements</PartyTemplateId></Reinforcements>" +
                                   "</Invasion></RegularBanditDailySpawnData>";
            const string valid = "<RegularBanditDailySpawnData party_template=\"PartyTemplate.legacy\" />";

            Spawns spawns = Read(invalid + valid, out List<System.Exception> errors);

            Assert.Multiple(() =>
            {
                Assert.That(spawns.AllSpawns.Select(spawn => spawn.PartyTemplate), Is.EqualTo(new[] { "PartyTemplate.legacy" }));
                Assert.That(errors, Has.Count.EqualTo(1));
                Assert.That(errors[0].Message, Does.Contain("PartyTemplate.invalid"));
                Assert.That(errors[0].Message, Does.Contain("MinimumTroopCount"));
            });
        }

        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void Adapt_RejectsInvalidFoodFloor(float foodStockFloor)
        {
            InvasionConfig config = ValidConfig();
            config.Supplies.FoodStockFloor = foodStockFloor;

            ArgumentException error = Assert.Throws<ArgumentException>(() => InvasionDtoAdapter.Adapt(config))!;

            Assert.That(error.Message, Does.Contain("FoodStockFloor"));
        }

        [Test]
        public void ValidateReferences_RejectsUnknownTemplateAndBanditSpawn()
        {
            InvasionData invasion = AdaptValidConfig();

            ArgumentException template = Assert.Throws<ArgumentException>(() =>
                InvasionRuntimeConfigurationValidator.ValidateReferences(invasion, "PartyTemplate.origin", false,
                    false))!;
            ArgumentException bandit = Assert.Throws<ArgumentException>(() =>
                InvasionRuntimeConfigurationValidator.ValidateReferences(invasion, "PartyTemplate.origin", true,
                    true))!;

            Assert.Multiple(() =>
            {
                Assert.That(template.Message, Does.Contain("unknown reinforcement party template"));
                Assert.That(bandit.Message, Does.Contain("bandit clan"));
            });
        }

        [Test]
        public void ValidateEffectiveSiegeTemplate_RejectsCastleOnlyWithoutMutatingSharedBehaviour()
        {
            BesiegeClosestSettlementData castleOnly = new(new[] { SpawnSettlementType.Castle }, 1f, 1f,
                0f, 0f, 1f, 0f, 0f, 0f);

            ArgumentException error = Assert.Throws<ArgumentException>(() =>
                InvasionRuntimeConfigurationValidator.ValidateEffectiveSiegeTemplate("PartyTemplate.origin", "siege",
                    true, new PriorityPartyBehaviourData[] { castleOnly }))!;

            Assert.Multiple(() =>
            {
                Assert.That(error.Message, Does.Contain("targeting Town"));
                Assert.That(castleOnly.TargetTypes, Is.EqualTo(new[] { SpawnSettlementType.Castle }));
            });
        }

        [Test]
        public void ValidateRuntimeConfiguration_AcceptsReinforcementTemplateAndTownTargetingSiegeTemplate()
        {
            InvasionData invasion = AdaptValidConfig();
            BesiegeClosestSettlementData townTargeting = new(new[] { SpawnSettlementType.Town }, 1f, 1f,
                0f, 0f, 1f, 0f, 0f, 0f);

            Assert.DoesNotThrow(() => InvasionRuntimeConfigurationValidator.ValidateReferences(invasion,
                "PartyTemplate.origin", false, true));
            Assert.DoesNotThrow(() => InvasionRuntimeConfigurationValidator.ValidateEffectiveSiegeTemplate(
                "PartyTemplate.origin", "siege", true, new PriorityPartyBehaviourData[] { townTargeting }));
        }

        private static Spawns Read(string spawnXml, out List<System.Exception> errors)
        {
            errors = new List<System.Exception>();
            using MemoryStream stream = new(Encoding.UTF8.GetBytes("<root>" + spawnXml + "</root>"));
            return SpawnDataReader.DeserializeAndResolve(stream, errors.Add);
        }

        private static InvasionData AdaptValidConfig()
        {
            return InvasionDtoAdapter.Adapt(ValidConfig());
        }

        private static InvasionConfig ValidConfig()
        {
            return new InvasionConfig
            {
                Reinforcements = new InvasionReinforcementsConfig { PartyTemplateId = "PartyTemplate.reinforcements" }
            };
        }
    }
}
