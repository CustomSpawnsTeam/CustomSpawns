using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using CustomSpawns.Data.Adapter;
using CustomSpawns.Data.Dto;
using CustomSpawns.Data.Model;
using NUnit.Framework;
using SpawnModel = CustomSpawns.Data.Model.Spawn;

namespace CustomSpawns.Tests.Spawn
{
    public class PriorityPartyBehaviourConfigTests
    {
        [Test]
        public void Template_DeserializesPreferredNestedPriorityBehaviourShapeInOrder()
        {
            const string behaviours = "<BesiegeClosestSettlement>" +
                                      "<TargetTypes><TargetType>Town</TargetType><TargetType>Castle</TargetType></TargetTypes>" +
                                      "<StrengthAssessment><MinimumAttackerToTargetRatio>1.50</MinimumAttackerToTargetRatio>" +
                                      "<MinimumAttackerToRoamingHostilesRatio>1.20</MinimumAttackerToRoamingHostilesRatio>" +
                                      "</StrengthAssessment>" +
                                      "<Reconnaissance><RequiredClearTimeInDays><Minimum>0.5</Minimum><Maximum>1.0</Maximum>" +
                                      "</RequiredClearTimeInDays><SkipWhenTargetStrengthRatio>3.0</SkipWhenTargetStrengthRatio>" +
                                      "</Reconnaissance><OperationWindowInDays><Minimum>5</Minimum><Maximum>10</Maximum>" +
                                      "</OperationWindowInDays><Recovery><GracePeriodInDays>2</GracePeriodInDays>" +
                                      "<MaximumFailedAttempts>2</MaximumFailedAttempts></Recovery><CooldownInDays>2</CooldownInDays>" +
                                      "</BesiegeClosestSettlement>" +
                                      "<RaidClosestVillage><StrengthAssessment><MinimumAttackerToTargetRatio>1.25" +
                                      "</MinimumAttackerToTargetRatio><MinimumAttackerToRoamingHostilesRatio>1.10" +
                                      "</MinimumAttackerToRoamingHostilesRatio></StrengthAssessment>" +
                                      "<Reconnaissance><RequiredClearTimeInDays><Minimum>0.5</Minimum><Maximum>1</Maximum>" +
                                      "</RequiredClearTimeInDays><SkipWhenTargetStrengthRatio>3</SkipWhenTargetStrengthRatio>" +
                                      "</Reconnaissance><OperationWindowInDays><Minimum>3</Minimum><Maximum>6</Maximum>" +
                                      "</OperationWindowInDays><Recovery><GracePeriodInDays>1</GracePeriodInDays>" +
                                      "<MaximumFailedAttempts>2</MaximumFailedAttempts></Recovery><CooldownInDays>2</CooldownInDays>" +
                                      "</RaidClosestVillage>" +
                                      "<PatrolAroundClosestLestInterruptedAndSwitch><TargetTypes><TargetType>Village</TargetType>" +
                                      "<TargetType>Town</TargetType></TargetTypes><StableTimeInDays><Minimum>2</Minimum>" +
                                      "<Maximum>2</Maximum></StableTimeInDays></PatrolAroundClosestLestInterruptedAndSwitch>";

            IReadOnlyList<PriorityPartyBehaviourData> configurations = AdaptXml(behaviours);

            Assert.That(configurations.Select(configuration => configuration.GetType()), Is.EqualTo(new[]
            {
                typeof(BesiegeClosestSettlementData),
                typeof(RaidClosestVillageData),
                typeof(PatrolAroundClosestLestInterruptedAndSwitchData)
            }));
            BesiegeClosestSettlementData siege = (BesiegeClosestSettlementData)configurations[0];
            Assert.That(siege.TargetTypes, Is.EqualTo(new[] { SpawnSettlementType.Town, SpawnSettlementType.Castle }));
            Assert.That(siege.MinimumAttackerToTargetRatio, Is.EqualTo(1.5f));
            Assert.That(siege.MinimumAttackerToRoamingHostilesRatio, Is.EqualTo(1.2f));
            Assert.That(siege.MinimumRequiredClearTimeInDays, Is.EqualTo(0.5f));
            Assert.That(siege.MaximumOperationWindowInDays, Is.EqualTo(10f));
            PatrolAroundClosestLestInterruptedAndSwitchData patrol =
                (PatrolAroundClosestLestInterruptedAndSwitchData)configurations[2];
            Assert.That(patrol.MinStableDays, Is.EqualTo(2f));
            Assert.That(patrol.MaxStableDays, Is.EqualTo(2f));
        }

        [Test]
        public void AdaptPriorityPartyBehaviours_AppliesOmittedDefaults()
        {
            IReadOnlyList<PriorityPartyBehaviourData> configurations =
                new PartyBehaviourDtoAdapter(PartyBehaviourRegistrations.ConfigAdapters).AdaptPriorityPartyBehaviours(new List<PriorityPartyBehaviourConfig>
                {
                    new BesiegeClosestSettlementConfig(),
                    new RaidClosestVillageConfig(),
                    new PatrolAroundClosestLestInterruptedAndSwitchConfig()
                });

            BesiegeClosestSettlementData siege = (BesiegeClosestSettlementData)configurations[0];
            Assert.That(siege.TargetTypes, Is.EqualTo(new[] { SpawnSettlementType.Town, SpawnSettlementType.Castle }));
            Assert.That(siege.MinimumAttackerToTargetRatio, Is.EqualTo(1.5f));
            Assert.That(siege.MinimumOperationWindowInDays, Is.EqualTo(5f));
            RaidClosestVillageData raid = (RaidClosestVillageData)configurations[1];
            Assert.That(raid.MinimumAttackerToTargetRatio, Is.EqualTo(1.25f));
            Assert.That(raid.MinimumOperationWindowInDays, Is.EqualTo(3f));
            PatrolAroundClosestLestInterruptedAndSwitchData patrol =
                (PatrolAroundClosestLestInterruptedAndSwitchData)configurations[2];
            Assert.That(patrol.PreferredSettlementTypes, Is.EqualTo(new[]
                { SpawnSettlementType.Village, SpawnSettlementType.Town, SpawnSettlementType.Castle }));
            Assert.That(patrol.MinStableDays, Is.Zero);
            Assert.That(patrol.MaxStableDays, Is.EqualTo(10f));
        }

        [Test]
        public void Template_PartialNestedValuesRetainBehaviourSpecificDefaults()
        {
            BesiegeClosestSettlementData siege = (BesiegeClosestSettlementData)AdaptXml(
                "<BesiegeClosestSettlement>" +
                "<StrengthAssessment><MinimumAttackerToTargetRatio>2</MinimumAttackerToTargetRatio></StrengthAssessment>" +
                "<Reconnaissance><RequiredClearTimeInDays><Minimum>0.75</Minimum></RequiredClearTimeInDays></Reconnaissance>" +
                "<OperationWindowInDays><Minimum>7</Minimum></OperationWindowInDays>" +
                "<Recovery><MaximumFailedAttempts>3</MaximumFailedAttempts></Recovery>" +
                "</BesiegeClosestSettlement>")[0];
            RaidClosestVillageData raid = (RaidClosestVillageData)AdaptXml(
                "<RaidClosestVillage>" +
                "<StrengthAssessment><MinimumAttackerToRoamingHostilesRatio>1.4</MinimumAttackerToRoamingHostilesRatio></StrengthAssessment>" +
                "<Reconnaissance><RequiredClearTimeInDays><Maximum>0.9</Maximum></RequiredClearTimeInDays></Reconnaissance>" +
                "<OperationWindowInDays><Maximum>5</Maximum></OperationWindowInDays>" +
                "<Recovery><GracePeriodInDays>1.5</GracePeriodInDays></Recovery>" +
                "</RaidClosestVillage>")[0];
            PatrolAroundClosestLestInterruptedAndSwitchData patrolWithMinimum =
                (PatrolAroundClosestLestInterruptedAndSwitchData)AdaptXml(
                    "<PatrolAroundClosestLestInterruptedAndSwitch>" +
                    "<StableTimeInDays><Minimum>4</Minimum></StableTimeInDays>" +
                    "</PatrolAroundClosestLestInterruptedAndSwitch>")[0];
            PatrolAroundClosestLestInterruptedAndSwitchData patrolWithMaximum =
                (PatrolAroundClosestLestInterruptedAndSwitchData)AdaptXml(
                    "<PatrolAroundClosestLestInterruptedAndSwitch>" +
                    "<StableTimeInDays><Maximum>6</Maximum></StableTimeInDays>" +
                    "</PatrolAroundClosestLestInterruptedAndSwitch>")[0];

            Assert.Multiple(() =>
            {
                Assert.That(siege.MinimumAttackerToTargetRatio, Is.EqualTo(2f));
                Assert.That(siege.MinimumAttackerToRoamingHostilesRatio, Is.EqualTo(1.2f));
                Assert.That(siege.MinimumRequiredClearTimeInDays, Is.EqualTo(0.75f));
                Assert.That(siege.MaximumRequiredClearTimeInDays, Is.EqualTo(1f));
                Assert.That(siege.MinimumOperationWindowInDays, Is.EqualTo(7f));
                Assert.That(siege.MaximumOperationWindowInDays, Is.EqualTo(10f));
                Assert.That(siege.GracePeriodInDays, Is.EqualTo(2f));
                Assert.That(siege.MaximumFailedAttempts, Is.EqualTo(3));
                Assert.That(raid.MinimumAttackerToTargetRatio, Is.EqualTo(1.25f));
                Assert.That(raid.MinimumAttackerToRoamingHostilesRatio, Is.EqualTo(1.4f));
                Assert.That(raid.MinimumRequiredClearTimeInDays, Is.EqualTo(0.5f));
                Assert.That(raid.MaximumRequiredClearTimeInDays, Is.EqualTo(0.9f));
                Assert.That(raid.MinimumOperationWindowInDays, Is.EqualTo(3f));
                Assert.That(raid.MaximumOperationWindowInDays, Is.EqualTo(5f));
                Assert.That(raid.GracePeriodInDays, Is.EqualTo(1.5f));
                Assert.That(raid.MaximumFailedAttempts, Is.EqualTo(2));
                Assert.That(patrolWithMinimum.MinStableDays, Is.EqualTo(4f));
                Assert.That(patrolWithMinimum.MaxStableDays, Is.EqualTo(10f));
                Assert.That(patrolWithMaximum.MinStableDays, Is.Zero);
                Assert.That(patrolWithMaximum.MaxStableDays, Is.EqualTo(6f));
            });
        }

        [Test]
        public void AdaptLegacyPatrol_IsSuppressedByATemplateReference()
        {
            SpawnModel spawn = DeserializeSpawn(
                "<RegularBanditDailySpawnData party_template=\"PartyTemplate.test\">" +
                "<PatrolAroundClosestLestInterruptedAndSwitch>true" +
                "</PatrolAroundClosestLestInterruptedAndSwitch>" +
                "<PriorityPartyBehaviourTemplateId>raider</PriorityPartyBehaviourTemplateId>" +
                "</RegularBanditDailySpawnData>");

            Assert.That(SpawnDtoAdapter.AdaptLegacyPatrol(spawn), Is.Null);
        }

        [Test]
        public void AdaptLegacyPatrol_AdaptsEnabledLegacyPatrolWhenNoTemplateIsReferenced()
        {
            SpawnModel spawn = new()
            {
                PatrolAroundConfig = new PatrolAroundConfig
                {
                    IsEnabled = true,
                    MinStableDays = 2f,
                    MaxStableDays = 4f,
                    PatrolLocationType = "Town|Castle"
                }
            };

            PatrolAroundClosestLestInterruptedAndSwitchData? patrol = SpawnDtoAdapter.AdaptLegacyPatrol(spawn);

            Assert.That(patrol, Is.Not.Null);
            Assert.That(patrol!.PreferredSettlementTypes, Is.EqualTo(
                new[] { SpawnSettlementType.Town, SpawnSettlementType.Castle }));
        }

        [TestCase(-1f, 1f)]
        [TestCase(2f, 1f)]
        public void AdaptPriorityPartyBehaviours_RejectsInvalidRanges(float minimum, float maximum)
        {
            RaidClosestVillageConfig raid = new();
            raid.OperationWindowInDays = new FloatRangeConfig(minimum, maximum);

            Assert.Throws<ArgumentException>(() => Adapt(raid));
        }

        [Test]
        public void AdaptPriorityPartyBehaviours_RejectsNonPositiveStrengthRatio()
        {
            RaidClosestVillageConfig raid = new();
            raid.StrengthAssessment.MinimumAttackerToTargetRatio = 0f;

            Assert.Throws<ArgumentException>(() => Adapt(raid));
        }

        [Test]
        public void AdaptPriorityPartyBehaviours_RejectsDuplicateBehaviourTypes()
        {
            Assert.Throws<ArgumentException>(() =>
                new PartyBehaviourDtoAdapter(PartyBehaviourRegistrations.ConfigAdapters).AdaptPriorityPartyBehaviours(new List<PriorityPartyBehaviourConfig>
                {
                    new RaidClosestVillageConfig(),
                    new RaidClosestVillageConfig()
                }));
        }

        [Test]
        public void AdaptPriorityPartyBehaviours_RejectsEmptyOrUnsupportedSiegeTargetList()
        {
            Assert.Throws<ArgumentException>(() => AdaptXml(
                "<BesiegeClosestSettlement><TargetTypes/></BesiegeClosestSettlement>"));
            Assert.Throws<ArgumentException>(() => Adapt(new BesiegeClosestSettlementConfig
            {
                TargetTypes = new List<SpawnSettlementType> { SpawnSettlementType.Village }
            }));
        }

        [TestCase("range", "NaN")]
        [TestCase("range", "INF")]
        [TestCase("range", "-INF")]
        [TestCase("ratio", "NaN")]
        [TestCase("ratio", "INF")]
        [TestCase("ratio", "-INF")]
        [TestCase("cooldown", "NaN")]
        [TestCase("cooldown", "INF")]
        [TestCase("cooldown", "-INF")]
        [TestCase("grace", "NaN")]
        [TestCase("grace", "INF")]
        [TestCase("grace", "-INF")]
        public void Template_RejectsNonFiniteNestedNumbers(string valueKind, string literal)
        {
            string nestedValue = valueKind switch
            {
                "range" => "<OperationWindowInDays><Maximum>" + literal + "</Maximum></OperationWindowInDays>",
                "ratio" => "<StrengthAssessment><MinimumAttackerToTargetRatio>" + literal +
                           "</MinimumAttackerToTargetRatio></StrengthAssessment>",
                "cooldown" => "<CooldownInDays>" + literal + "</CooldownInDays>",
                _ => "<Recovery><GracePeriodInDays>" + literal + "</GracePeriodInDays></Recovery>"
            };

            ArgumentException error = Assert.Throws<ArgumentException>(() =>
                AdaptXml("<RaidClosestVillage>" + nestedValue + "</RaidClosestVillage>"))!;

            Assert.That(error.Message, Does.Contain("finite"));
        }

        [Test]
        public void AdaptPriorityPartyBehaviours_CopiesSiegeTargetTypes()
        {
            var targetTypes = new List<SpawnSettlementType> { SpawnSettlementType.Town };
            BesiegeClosestSettlementData siege = (BesiegeClosestSettlementData)Adapt(
                new BesiegeClosestSettlementConfig { TargetTypes = targetTypes })[0];

            targetTypes[0] = SpawnSettlementType.Village;

            Assert.That(siege.TargetTypes, Is.EqualTo(new[] { SpawnSettlementType.Town }));
        }

        [Test]
        public void AdaptPriorityPartyBehaviours_CopiesPatrolTargetTypes()
        {
            var targetTypes = new List<SpawnSettlementType> { SpawnSettlementType.Town };
            PatrolAroundClosestLestInterruptedAndSwitchData patrol =
                (PatrolAroundClosestLestInterruptedAndSwitchData)Adapt(
                    new PatrolAroundClosestLestInterruptedAndSwitchConfig { TargetTypes = targetTypes })[0];

            targetTypes[0] = SpawnSettlementType.Village;

            Assert.That(patrol.PreferredSettlementTypes, Is.EqualTo(new[] { SpawnSettlementType.Town }));
        }

        private static IReadOnlyList<PriorityPartyBehaviourData> Adapt(PriorityPartyBehaviourConfig configuration)
        {
            return new PartyBehaviourDtoAdapter(PartyBehaviourRegistrations.ConfigAdapters).AdaptPriorityPartyBehaviours(
                new List<PriorityPartyBehaviourConfig> { configuration });
        }

        private static SpawnModel DeserializeSpawn(string xml)
        {
            XmlSerializer serializer = new(typeof(SpawnModel));
            using StringReader reader = new(xml);
            return (SpawnModel)serializer.Deserialize(reader);
        }

        private static IReadOnlyList<PriorityPartyBehaviourData> AdaptXml(string behaviourXml)
        {
            XmlSerializer serializer = new(typeof(PriorityPartyBehaviourTemplate));
            using StringReader reader = new("<PriorityPartyBehaviourTemplate><Id>test</Id>" +
                                            "<PriorityPartyBehaviours>" + behaviourXml + "</PriorityPartyBehaviours>" +
                                            "</PriorityPartyBehaviourTemplate>");
            PriorityPartyBehaviourTemplate template = (PriorityPartyBehaviourTemplate)serializer.Deserialize(reader);
            return new PartyBehaviourDtoAdapter(PartyBehaviourRegistrations.ConfigAdapters).Adapt(template).PriorityPartyBehaviours;
        }
    }
}
