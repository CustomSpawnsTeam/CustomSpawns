using System;
using System.Collections.Generic;
using CustomSpawns.AI.Adapters;
using CustomSpawns.AI.Offensives;
using CustomSpawns.Data.Adapter;
using CustomSpawns.Data.Dao;
using CustomSpawns.Data.Dto;
using CustomSpawns.Invasions.Adapters;
using CustomSpawns.Invasions.CapitalObjectives;
using CustomSpawns.Utils;
using Moq;
using NUnit.Framework;

namespace CustomSpawns.Tests.Invasions
{
    public class BannerlordInvasionAssaultConfigurationProviderTests
    {
        [Test]
        public void Find_MapsEffectiveRatiosTargetKindsAndOriginalDoctrine()
        {
            IInvasionAssaultConfigurationProvider provider = Provider(
                Spawn(requiresCoastal: true, supplyLine: "Any"),
                new PriorityPartyBehaviourData[]
                {
                    Siege(new[] { SpawnSettlementType.Town, SpawnSettlementType.Castle }, 1.7f, 1.3f)
                });

            InvasionAssaultRequirements? requirements = provider.Find("invader");

            Assert.Multiple(() =>
            {
                Assert.That(requirements, Is.Not.Null);
                Assert.That(requirements!.TargetKinds,
                    Is.EqualTo(new[] { SiegeTargetKind.Town, SiegeTargetKind.Castle }));
                Assert.That(requirements.MinimumTargetRatio, Is.EqualTo(1.7f));
                Assert.That(requirements.MinimumRoamingRatio, Is.EqualTo(1.3f));
                Assert.That(requirements.RequiresOriginalCoastalTarget, Is.True);
                Assert.That(requirements.SupplyLine, Is.EqualTo(InvasionSupplyLineKind.Any));
            });
        }

        [Test]
        public void Find_UsesAlreadyAdaptedDefaultRatiosAndDefaultCoastalSupplyLine()
        {
            IInvasionAssaultConfigurationProvider provider = Provider(
                Spawn(requiresCoastal: false, supplyLine: null),
                new PriorityPartyBehaviourData[]
                {
                    Siege(new[] { SpawnSettlementType.Town }, 1.5f, 1.2f)
                });

            InvasionAssaultRequirements? requirements = provider.Find("invader");

            Assert.Multiple(() =>
            {
                Assert.That(requirements!.MinimumTargetRatio, Is.EqualTo(1.5f));
                Assert.That(requirements.MinimumRoamingRatio, Is.EqualTo(1.2f));
                Assert.That(requirements.SupplyLine, Is.EqualTo(InvasionSupplyLineKind.Coastal));
            });
        }

        [Test]
        public void Find_UsesFirstTownCapableBehaviourAndWarnsOnlyOnce()
        {
            var warnings = new List<string>();
            IInvasionAssaultConfigurationProvider provider = Provider(
                Spawn(requiresCoastal: false, supplyLine: "None"),
                new PriorityPartyBehaviourData[]
                {
                    Siege(new[] { SpawnSettlementType.Castle }, 9f, 9f),
                    Siege(new[] { SpawnSettlementType.Town }, 1.6f, 1.1f),
                    Siege(new[] { SpawnSettlementType.Town, SpawnSettlementType.Castle }, 2f, 2f)
                }, warnings.Add);

            InvasionAssaultRequirements? first = provider.Find("invader");
            InvasionAssaultRequirements? second = provider.Find("invader");

            Assert.Multiple(() =>
            {
                Assert.That(first!.MinimumTargetRatio, Is.EqualTo(1.6f));
                Assert.That(second!.MinimumTargetRatio, Is.EqualTo(1.6f));
                Assert.That(warnings, Has.Count.EqualTo(1));
            });
        }

        [Test]
        public void Find_ReturnsNoRequirementsWhenEffectiveTownSiegeConfigurationIsMissing()
        {
            IInvasionAssaultConfigurationProvider provider = Provider(
                Spawn(requiresCoastal: false, supplyLine: "Coastal"),
                new PriorityPartyBehaviourData[]
                {
                    Siege(new[] { SpawnSettlementType.Castle }, 1.5f, 1.2f)
                });

            Assert.That(provider.Find("invader"), Is.Null);
        }

        [Test]
        public void Find_ReturnsNoRequirementsWhenRuntimeSupplyLineIsUnknown()
        {
            IInvasionAssaultConfigurationProvider provider = Provider(
                Spawn(requiresCoastal: false, supplyLine: "changed-at-runtime"),
                new PriorityPartyBehaviourData[]
                {
                    Siege(new[] { SpawnSettlementType.Town }, 1.5f, 1.2f)
                });

            Assert.That(provider.Find("invader"), Is.Null);
        }

        private static IInvasionAssaultConfigurationProvider Provider(SpawnDto spawn,
            IReadOnlyList<PriorityPartyBehaviourData> behaviours, Action<string>? warn = null)
        {
            var spawnDao = new Mock<ISpawnDao>();
            spawnDao.Setup(dao => dao.FindByPartyTemplateId("invader")).Returns(spawn);
            var partyBehaviourDao = new Mock<IPartyBehaviourDao>();
            partyBehaviourDao.Setup(dao => dao.FindById("siege-template"))
                .Returns(new PartyBehaviourDto
                {
                    Id = "siege-template",
                    PriorityPartyBehaviours = behaviours
                });
            var lookup = new SpawnPartyBehaviourLookup(spawnDao.Object, partyBehaviourDao.Object,
                new Mock<MessageBoxService>().Object);

            return new BannerlordInvasionAssaultConfigurationProvider(spawnDao.Object, lookup,
                warn ?? (_ => { }));
        }

        private static SpawnDto Spawn(bool requiresCoastal, string? supplyLine)
        {
            return new SpawnDto
            {
                PriorityPartyBehaviourTemplateId = "siege-template",
                Invasion = new InvasionData(requiresCoastal,
                    Array.Empty<InvasionReinforcementRuleData>(), supplyLine: supplyLine)
            };
        }

        private static BesiegeClosestSettlementData Siege(
            IReadOnlyList<SpawnSettlementType> targetTypes, float targetRatio, float roamingRatio)
        {
            return new BesiegeClosestSettlementData(targetTypes, targetRatio, roamingRatio,
                0f, 0f, 0f, 0f, 0f, 0f);
        }
    }
}
