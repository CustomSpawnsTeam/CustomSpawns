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
        public void Spawn_DeserializesPreferredNestedPriorityBehaviourShapeInOrder()
        {
            const string xml = "<RegularBanditDailySpawnData party_template=\"PartyTemplate.test\">" +
                               "<PriorityPartyBehaviours>" +
                               "<BesiegeClosestSettlement>" +
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
                               "<Maximum>2</Maximum></StableTimeInDays></PatrolAroundClosestLestInterruptedAndSwitch>" +
                               "</PriorityPartyBehaviours></RegularBanditDailySpawnData>";

            SpawnModel spawn = Deserialize(xml);
            IReadOnlyList<PriorityPartyBehaviourData> configurations = SpawnDtoAdapter.AdaptPriorityPartyBehaviours(spawn);

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
            SpawnModel spawn = new()
            {
                PriorityPartyBehaviours = new List<PriorityPartyBehaviourConfig>
                {
                    new BesiegeClosestSettlementConfig(),
                    new RaidClosestVillageConfig(),
                    new PatrolAroundClosestLestInterruptedAndSwitchConfig()
                }
            };

            IReadOnlyList<PriorityPartyBehaviourData> configurations = SpawnDtoAdapter.AdaptPriorityPartyBehaviours(spawn);

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
        public void AdaptPriorityPartyBehaviours_PrefersPresentListOverLegacyPatrol()
        {
            SpawnModel spawn = new()
            {
                PatrolAroundConfig = new PatrolAroundConfig { IsEnabled = true },
                PriorityPartyBehaviours = new List<PriorityPartyBehaviourConfig>()
            };

            Assert.That(SpawnDtoAdapter.AdaptPriorityPartyBehaviours(spawn), Is.Empty);
        }

        [Test]
        public void AdaptPriorityPartyBehaviours_AdaptsEnabledLegacyPatrolWhenListIsAbsent()
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

            IReadOnlyList<PriorityPartyBehaviourData> configurations = SpawnDtoAdapter.AdaptPriorityPartyBehaviours(spawn);

            Assert.That(configurations, Has.Count.EqualTo(1));
            PatrolAroundClosestLestInterruptedAndSwitchData patrol =
                (PatrolAroundClosestLestInterruptedAndSwitchData)configurations[0];
            Assert.That(patrol.PreferredSettlementTypes, Is.EqualTo(
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
            Assert.Throws<ArgumentException>(() => SpawnDtoAdapter.AdaptPriorityPartyBehaviours(new SpawnModel
            {
                PriorityPartyBehaviours = new List<PriorityPartyBehaviourConfig>
                {
                    new RaidClosestVillageConfig(),
                    new RaidClosestVillageConfig()
                }
            }));
        }

        [Test]
        public void AdaptPriorityPartyBehaviours_RejectsEmptyOrUnsupportedSiegeTargetList()
        {
            Assert.Throws<ArgumentException>(() => Adapt(new BesiegeClosestSettlementConfig
            {
                TargetTypes = new List<SpawnSettlementType>()
            }));
            Assert.Throws<ArgumentException>(() => Adapt(new BesiegeClosestSettlementConfig
            {
                TargetTypes = new List<SpawnSettlementType> { SpawnSettlementType.Village }
            }));
        }

        private static IReadOnlyList<PriorityPartyBehaviourData> Adapt(PriorityPartyBehaviourConfig configuration)
        {
            return SpawnDtoAdapter.AdaptPriorityPartyBehaviours(new SpawnModel
            {
                PriorityPartyBehaviours = new List<PriorityPartyBehaviourConfig> { configuration }
            });
        }

        private static SpawnModel Deserialize(string xml)
        {
            XmlSerializer serializer = new(typeof(SpawnModel));
            using StringReader reader = new(xml);
            return (SpawnModel)serializer.Deserialize(reader);
        }
    }
}
