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
            const string invasion = "<Invasion>" +
                                    "<Reinforcements><Rule><Id>infantry</Id>" +
                                    "<PartyTemplateId>PartyTemplate.reinforcements</PartyTemplateId>" +
                                    "</Rule></Reinforcements></Invasion>";
            Spawns spawns = Read("<RegularBanditDailySpawnData party_template=\"PartyTemplate.invasion\">" + invasion +
                                 "</RegularBanditDailySpawnData>", out List<System.Exception> errors);

            InvasionData data = InvasionDtoAdapter.Adapt(spawns.AllSpawns.Single().Invasion!);

            Assert.Multiple(() =>
            {
                Assert.That(errors, Is.Empty);
                Assert.That(data.RequiresFirstCoastalConquest, Is.False);
                Assert.That(data.ReinforcementRules.Single().Id, Is.EqualTo("infantry"));
                Assert.That(data.ReinforcementRules.Single().PartyTemplateId, Is.EqualTo("PartyTemplate.reinforcements"));
                Assert.That(data.ReinforcementRules.Single().IntervalInDays, Is.EqualTo(7f));
            });
        }

        [Test]
        public void Adapt_ReadsTheGarrisonFloorAndTheLordsEachWaveBrings()
        {
            const string invasion = "<Invasion><MinimumCapitalGarrison>400</MinimumCapitalGarrison>" +
                                    "<LordPartySizeBonus>50</LordPartySizeBonus>" +
                                    "<FollowsInvasion> PartyTemplate.leader </FollowsInvasion><Reinforcements><Rule><Id>infantry</Id>" +
                                    "<PartyTemplateId>PartyTemplate.reinforcements</PartyTemplateId>" +
                                    "<LordsPerWave>3</LordsPerWave></Rule></Reinforcements></Invasion>";
            Spawns spawns = Read("<RegularBanditDailySpawnData party_template=\"PartyTemplate.invasion\">" + invasion +
                                 "</RegularBanditDailySpawnData>", out List<System.Exception> errors);

            InvasionData data = InvasionDtoAdapter.Adapt(spawns.AllSpawns.Single().Invasion!);

            Assert.Multiple(() =>
            {
                Assert.That(errors, Is.Empty);
                Assert.That(data.MinimumCapitalGarrison, Is.EqualTo(400));
                Assert.That(data.LordPartySizeBonus, Is.EqualTo(50));
                Assert.That(AdaptValidConfig().LordPartySizeBonus, Is.EqualTo(0));
                Assert.That(data.FollowsInvasion, Is.EqualTo("PartyTemplate.leader"));
                Assert.That(AdaptValidConfig().FollowsInvasion, Is.Null);
                Assert.That(data.ReinforcementRules.Single().LordsPerWave, Is.EqualTo(3));
                // An invasion that mentions neither keeps no floor and brings troops only.
                Assert.That(AdaptValidConfig().MinimumCapitalGarrison, Is.EqualTo(0));
                Assert.That(AdaptValidConfig().ReinforcementRules.Single().LordsPerWave, Is.EqualTo(0));
            });
        }

        /**
         * A rule may be told to send a given number of waves and then stop, which is what a one-off
         * reinforcement is. Waves are looked at once a day, so a shorter interval than a day is refused
         * rather than quietly behaving like one.
         */
        [Test]
        public void Adapt_ReadsAWaveCountAndRefusesAnIntervalShorterThanTheDailyCheck()
        {
            const string invasion = "<Invasion><Reinforcements><Rule><Id>landing</Id>" +
                                    "<PartyTemplateId>PartyTemplate.reinforcements</PartyTemplateId>" +
                                    "<IntervalInDays>1</IntervalInDays><MaxWaves>1</MaxWaves>" +
                                    "</Rule></Reinforcements></Invasion>";
            Spawns spawns = Read("<RegularBanditDailySpawnData party_template=\"PartyTemplate.invasion\">" + invasion +
                                 "</RegularBanditDailySpawnData>", out List<System.Exception> errors);

            InvasionData data = InvasionDtoAdapter.Adapt(spawns.AllSpawns.Single().Invasion!);
            InvasionConfig tooOften = ValidConfig();
            tooOften.Reinforcements.Rules.Single().IntervalInDays = 0.5f;

            Assert.Multiple(() =>
            {
                Assert.That(errors, Is.Empty);
                Assert.That(data.ReinforcementRules.Single().MaxWaves, Is.EqualTo(1));
                // A rule that mentions no count keeps sending for as long as the invasion lives.
                Assert.That(AdaptValidConfig().ReinforcementRules.Single().MaxWaves, Is.EqualTo(0));
                Assert.That(Assert.Throws<ArgumentException>(() => InvasionDtoAdapter.Adapt(tooOften))!.Message,
                    Does.Contain("at least one day"));
            });
        }

        [TestCase(-1, 0, "MinimumCapitalGarrison")]
        [TestCase(0, -1, "LordsPerWave")]
        [TestCase(0, 0, "LordPartySizeBonus", -1)]
        public void Adapt_RejectsNegativeGarrisonAndLordCounts(int minimumCapitalGarrison, int lordsPerWave,
            string expected, int lordPartySizeBonus = 0)
        {
            InvasionConfig config = ValidConfig();
            config.MinimumCapitalGarrison = minimumCapitalGarrison;
            config.LordPartySizeBonus = lordPartySizeBonus;
            config.Reinforcements.Rules.Single().LordsPerWave = lordsPerWave;

            ArgumentException error = Assert.Throws<ArgumentException>(() => InvasionDtoAdapter.Adapt(config))!;

            Assert.That(error.Message, Does.Contain(expected));
        }

        [Test]
        public void Adapt_SeveralRulesKeepTheirConfigurationOrder()
        {
            const string invasion = "<Invasion><Reinforcements>" +
                                    "<Rule><Id>infantry</Id><PartyTemplateId>PartyTemplate.infantry</PartyTemplateId>" +
                                    "<IntervalInDays>5</IntervalInDays></Rule>" +
                                    "<Rule><Id>cavalry</Id><PartyTemplateId>PartyTemplate.cavalry</PartyTemplateId>" +
                                    "<IntervalInDays>21</IntervalInDays>" +
                                    "</Rule></Reinforcements></Invasion>";
            Spawns spawns = Read("<RegularBanditDailySpawnData party_template=\"PartyTemplate.invasion\">" + invasion +
                                 "</RegularBanditDailySpawnData>", out List<System.Exception> errors);

            InvasionData data = InvasionDtoAdapter.Adapt(spawns.AllSpawns.Single().Invasion!);

            Assert.Multiple(() =>
            {
                Assert.That(errors, Is.Empty);
                Assert.That(data.ReinforcementRules.Select(rule => rule.Id), Is.EqualTo(new[] { "infantry", "cavalry" }));
                Assert.That(data.ReinforcementRules.Select(rule => rule.IntervalInDays), Is.EqualTo(new[] { 5f, 21f }));
            });
        }

        [Test]
        public void Adapt_SupplyLineIsCarriedThroughAndDefaultsToCoastalWhenUnnamed()
        {
            const string invasion = "<Invasion><SupplyLine>Any</SupplyLine><Reinforcements><Rule><Id>infantry</Id>" +
                                    "<PartyTemplateId>PartyTemplate.reinforcements</PartyTemplateId>" +
                                    "</Rule></Reinforcements></Invasion>";
            Spawns spawns = Read("<RegularBanditDailySpawnData party_template=\"PartyTemplate.invasion\">" + invasion +
                                 "</RegularBanditDailySpawnData>", out List<System.Exception> errors);

            InvasionData data = InvasionDtoAdapter.Adapt(spawns.AllSpawns.Single().Invasion!);

            Assert.Multiple(() =>
            {
                Assert.That(errors, Is.Empty);
                Assert.That(data.SupplyLine, Is.EqualTo("Any"));
                Assert.That(InvasionDtoAdapter.Adapt(ValidConfig()).SupplyLine, Is.Null);
            });
        }

        [Test]
        public void Adapt_RejectsAnUnknownSupplyLine()
        {
            InvasionConfig config = ValidConfig();
            config.SupplyLine = "Inland";

            ArgumentException error = Assert.Throws<ArgumentException>(() => InvasionDtoAdapter.Adapt(config))!;

            Assert.That(error.Message, Does.Contain("SupplyLine 'Inland' must be one of Coastal, Any, None"));
        }

[Test]
        public void Adapt_RejectsTwoRulesSharingOneId()
        {
            InvasionConfig config = ValidConfig();
            config.Reinforcements.Rules.Add(new InvasionReinforcementRuleConfig
            {
                Id = "infantry", PartyTemplateId = "PartyTemplate.other"
            });

            ArgumentException error = Assert.Throws<ArgumentException>(() => InvasionDtoAdapter.Adapt(config))!;

            Assert.That(error.Message, Does.Contain("'infantry' is used twice"));
        }

        [Test]
        public void Adapt_RejectsARuleWithoutAnId()
        {
            InvasionConfig config = ValidConfig();
            config.Reinforcements.Rules.Single().Id = null;

            ArgumentException error = Assert.Throws<ArgumentException>(() => InvasionDtoAdapter.Adapt(config))!;

            Assert.That(error.Message, Does.Contain("Id"));
        }

        [Test]
        public void Adapt_ReadsTheFiefGarrisonAndTheConquest()
        {
            const string invasion = "<Invasion><MinimumFiefGarrison>250</MinimumFiefGarrison>" +
                                    "<Conquest><ThreatPerFiefHeld>0.05</ThreatPerFiefHeld>" +
                                    "<OpenFrontIntervalInDays>30</OpenFrontIntervalInDays>" +
                                    "<TargetSiegePreference>4</TargetSiegePreference>" +
                                    "<ReconsiderTargetIntervalInDays>30</ReconsiderTargetIntervalInDays></Conquest>" +
                                    "<Reinforcements><Rule><Id>infantry</Id>" +
                                    "<PartyTemplateId>PartyTemplate.reinforcements</PartyTemplateId>" +
                                    "</Rule></Reinforcements></Invasion>";
            Spawns spawns = Read("<RegularBanditDailySpawnData party_template=\"PartyTemplate.invasion\">" + invasion +
                                 "</RegularBanditDailySpawnData>", out List<System.Exception> errors);

            InvasionData data = InvasionDtoAdapter.Adapt(spawns.AllSpawns.Single().Invasion!);

            Assert.Multiple(() =>
            {
                Assert.That(errors, Is.Empty);
                Assert.That(data.MinimumFiefGarrison, Is.EqualTo(250));
                Assert.That(data.Conquest!.ThreatPerFiefHeld, Is.EqualTo(0.05f));
                Assert.That(data.Conquest.OpenFrontIntervalInDays, Is.EqualTo(30f));
                Assert.That(data.Conquest.TargetSiegePreference, Is.EqualTo(4f));
                Assert.That(data.Conquest.ReconsiderTargetIntervalInDays, Is.EqualTo(30f));
            });
        }

        [Test]
        public void Adapt_RejectsANegativeReconsiderInterval()
        {
            InvasionConfig config = ValidConfig();
            config.Conquest = new InvasionConquestConfig { ReconsiderTargetIntervalInDays = -1f };

            ArgumentException error = Assert.Throws<ArgumentException>(() => InvasionDtoAdapter.Adapt(config))!;

            Assert.That(error.Message, Does.Contain("ReconsiderTargetIntervalInDays"));
        }

        [Test]
        public void Adapt_ReadsTheGarrisonArmy()
        {
            const string invasion = "<Invasion><Conquest><GarrisonArmy><IntervalInDays>90</IntervalInDays>" +
                                    "<GarrisonShare>0.5</GarrisonShare>" +
                                    "</GarrisonArmy></Conquest>" +
                                    "<Reinforcements><Rule><Id>infantry</Id>" +
                                    "<PartyTemplateId>PartyTemplate.reinforcements</PartyTemplateId>" +
                                    "</Rule></Reinforcements></Invasion>";
            Spawns spawns = Read("<RegularBanditDailySpawnData party_template=\"PartyTemplate.invasion\">" + invasion +
                                 "</RegularBanditDailySpawnData>", out List<System.Exception> errors);

            InvasionGarrisonArmyData? garrisonArmy =
                InvasionDtoAdapter.Adapt(spawns.AllSpawns.Single().Invasion!).Conquest!.GarrisonArmy;

            Assert.Multiple(() =>
            {
                Assert.That(errors, Is.Empty);
                Assert.That(garrisonArmy!.IntervalInDays, Is.EqualTo(90f));
                Assert.That(garrisonArmy.GarrisonShare, Is.EqualTo(0.5f));
            });
        }

        [Test]
        public void Adapt_WithoutAGarrisonArmyRaisesNone()
        {
            InvasionConfig config = ValidConfig();
            config.Conquest = new InvasionConquestConfig();

            Assert.That(InvasionDtoAdapter.Adapt(config).Conquest!.GarrisonArmy, Is.Null);
        }

        [TestCase(0.5f, 0.5f, "IntervalInDays")]
        [TestCase(90f, -0.1f, "GarrisonShare")]
        [TestCase(90f, 1.5f, "GarrisonShare")]
        public void Adapt_RejectsABadGarrisonArmy(float intervalInDays, float garrisonShare, string expected)
        {
            InvasionConfig config = ValidConfig();
            config.Conquest = new InvasionConquestConfig
            {
                GarrisonArmy = new InvasionGarrisonArmyConfig
                {
                    IntervalInDays = intervalInDays, GarrisonShare = garrisonShare
                }
            };

            ArgumentException error = Assert.Throws<ArgumentException>(() => InvasionDtoAdapter.Adapt(config))!;

            Assert.That(error.Message, Does.Contain(expected));
        }

        [Test]
        public void Adapt_WithoutAConquestLeavesWarsToDiplomacy()
        {
            Assert.That(InvasionDtoAdapter.Adapt(ValidConfig()).Conquest, Is.Null);
        }

        [TestCase(-1, 0f, 0f, 0f, "MinimumFiefGarrison")]
        [TestCase(0, -1f, 0f, 0f, "ThreatPerFiefHeld")]
        [TestCase(0, float.NaN, 0f, 0f, "ThreatPerFiefHeld")]
        [TestCase(0, 0f, -1f, 0f, "OpenFrontIntervalInDays")]
        [TestCase(0, 0f, 0f, -1f, "TargetSiegePreference")]
        [TestCase(0, 0f, 0f, float.PositiveInfinity, "TargetSiegePreference")]
        public void Adapt_RejectsNegativeConsolidationAndConquest(int minimumFiefGarrison, float threatPerFiefHeld,
            float openFrontIntervalInDays, float targetSiegePreference, string expected)
        {
            InvasionConfig config = ValidConfig();
            config.MinimumFiefGarrison = minimumFiefGarrison;
            config.Conquest = new InvasionConquestConfig
            {
                ThreatPerFiefHeld = threatPerFiefHeld, OpenFrontIntervalInDays = openFrontIntervalInDays,
                TargetSiegePreference = targetSiegePreference
            };

            ArgumentException error = Assert.Throws<ArgumentException>(() => InvasionDtoAdapter.Adapt(config))!;

            Assert.That(error.Message, Does.Contain(expected));
        }

        [Test]
        public void Adapt_RejectsAnInvasionWithoutAnyRule()
        {
            InvasionConfig config = ValidConfig();
            config.Reinforcements.Rules.Clear();

            Assert.Throws<ArgumentException>(() => InvasionDtoAdapter.Adapt(config));
        }

        [Test]
        public void Adapt_ExplicitCoastalConquestRequirementEnablesInitialCoastalTargeting()
        {
            const string invasion = "<Invasion><RequiresFirstCoastalConquest>true</RequiresFirstCoastalConquest>" +
                                    "<Reinforcements><Rule><Id>infantry</Id>" +
                                    "<PartyTemplateId>PartyTemplate.reinforcements</PartyTemplateId>" +
                                    "</Rule></Reinforcements></Invasion>";
            Spawns spawns = Read("<RegularBanditDailySpawnData party_template=\"PartyTemplate.invasion\">" +
                                 invasion + "</RegularBanditDailySpawnData>", out List<System.Exception> errors);

            InvasionData data = InvasionDtoAdapter.Adapt(spawns.AllSpawns.Single().Invasion!);

            Assert.Multiple(() =>
            {
                Assert.That(errors, Is.Empty);
                Assert.That(data.RequiresFirstCoastalConquest, Is.True);
            });
        }

        [Test]
        public void Adapt_RelocateCapitalOnLossIsReadAndOffWhenLeftOut()
        {
            const string rules = "<Reinforcements><Rule><Id>infantry</Id>" +
                                 "<PartyTemplateId>PartyTemplate.reinforcements</PartyTemplateId>" +
                                 "</Rule></Reinforcements>";
            Spawns on = Read("<RegularBanditDailySpawnData party_template=\"PartyTemplate.invasion\"><Invasion>" +
                             "<RelocateCapitalOnLoss>true</RelocateCapitalOnLoss>" + rules +
                             "</Invasion></RegularBanditDailySpawnData>", out List<System.Exception> errors);
            Spawns off = Read("<RegularBanditDailySpawnData party_template=\"PartyTemplate.invasion\"><Invasion>" +
                              rules + "</Invasion></RegularBanditDailySpawnData>", out _);

            Assert.Multiple(() =>
            {
                Assert.That(errors, Is.Empty);
                Assert.That(InvasionDtoAdapter.Adapt(on.AllSpawns.Single().Invasion!).RelocateCapitalOnLoss,
                    Is.True);
                Assert.That(InvasionDtoAdapter.Adapt(off.AllSpawns.Single().Invasion!).RelocateCapitalOnLoss,
                    Is.False);
            });
        }

        [Test]
        public void DeserializeAndResolve_LegacyCoastalTownElementsAreIgnored()
        {
            const string invasion = "<Invasion><Foothold><CoastalTownIds><TownId>town_V7</TownId>" +
                                    "</CoastalTownIds></Foothold><Reinforcements><Rule><Id>infantry</Id>" +
                                    "<PartyTemplateId>PartyTemplate.reinforcements</PartyTemplateId>" +
                                    "</Rule></Reinforcements></Invasion>";
            Spawns spawns = Read("<RegularBanditDailySpawnData party_template=\"PartyTemplate.invasion\">" +
                                 invasion + "</RegularBanditDailySpawnData>", out List<System.Exception> errors);

            InvasionData data = InvasionDtoAdapter.Adapt(spawns.AllSpawns.Single().Invasion!);

            Assert.Multiple(() =>
            {
                Assert.That(errors, Is.Empty);
                Assert.That(data.ReinforcementRules.Single().PartyTemplateId, Is.EqualTo("PartyTemplate.reinforcements"));
                Assert.That(data.ReinforcementRules.Single().IntervalInDays, Is.EqualTo(7f));
            });
        }

        [Test]
        public void DeserializeAndResolve_InvalidInvasionReportsItsSpawnAndKeepsUnrelatedSpawns()
        {
            const string invalid = "<RegularBanditDailySpawnData party_template=\"PartyTemplate.invalid\"><Invasion>" +
                                   "<MinimumCapitalGarrison>-1</MinimumCapitalGarrison>" +
                                   "<Reinforcements><Rule><Id>infantry</Id>" +
                                   "<PartyTemplateId>PartyTemplate.reinforcements</PartyTemplateId></Rule></Reinforcements>" +
                                   "</Invasion></RegularBanditDailySpawnData>";
            const string valid = "<RegularBanditDailySpawnData party_template=\"PartyTemplate.legacy\" />";

            Spawns spawns = Read(invalid + valid, out List<System.Exception> errors);

            Assert.Multiple(() =>
            {
                Assert.That(spawns.AllSpawns.Select(spawn => spawn.PartyTemplate), Is.EqualTo(new[] { "PartyTemplate.legacy" }));
                Assert.That(errors, Has.Count.EqualTo(1));
                Assert.That(errors[0].Message, Does.Contain("PartyTemplate.invalid"));
                Assert.That(errors[0].Message, Does.Contain("MinimumCapitalGarrison"));
            });
        }

        [Test]
        public void Adapt_ReadsTheBankLimitItsPerTownBonusAndTheWaveCap()
        {
            const string invasion = "<Invasion><MaxBankedTroops>1700</MaxBankedTroops>" +
                                    "<BankedTroopsPerTownHeld>300</BankedTroopsPerTownHeld>" +
                                    "<Reinforcements><Rule><Id>infantry</Id>" +
                                    "<PartyTemplateId>PartyTemplate.reinforcements</PartyTemplateId>" +
                                    "<MaxMenPerWave>600</MaxMenPerWave><MenPerTownHeld>300</MenPerTownHeld>" +
                                    "</Rule></Reinforcements></Invasion>";
            Spawns spawns = Read("<RegularBanditDailySpawnData party_template=\"PartyTemplate.invasion\">" + invasion +
                                 "</RegularBanditDailySpawnData>", out List<System.Exception> errors);

            InvasionData data = InvasionDtoAdapter.Adapt(spawns.AllSpawns.Single().Invasion!);

            Assert.Multiple(() =>
            {
                Assert.That(errors, Is.Empty);
                Assert.That(data.MaxBankedTroops, Is.EqualTo(1700));
                Assert.That(data.BankedTroopsPerTownHeld, Is.EqualTo(300));
                Assert.That(data.ReinforcementRules.Single().MaxMenPerWave, Is.EqualTo(600));
                Assert.That(data.ReinforcementRules.Single().MenPerTownHeld, Is.EqualTo(300));
                // An invasion that mentions neither keeps a bottomless bank and banks whole waves.
                Assert.That(AdaptValidConfig().MaxBankedTroops, Is.EqualTo(0));
                Assert.That(AdaptValidConfig().ReinforcementRules.Single().MaxMenPerWave, Is.EqualTo(0));
            });
        }

        [Test]
        public void Adapt_RejectsANegativeBankLimitOrWaveCap()
        {
            InvasionConfig limit = ValidConfig();
            limit.MaxBankedTroops = -1;
            InvasionConfig perTown = ValidConfig();
            perTown.BankedTroopsPerTownHeld = -1;
            InvasionConfig cap = ValidConfig();
            cap.Reinforcements.Rules.Single().MaxMenPerWave = -1;

            Assert.Multiple(() =>
            {
                Assert.That(Assert.Throws<ArgumentException>(() => InvasionDtoAdapter.Adapt(limit))!.Message,
                    Does.Contain("MaxBankedTroops"));
                Assert.That(Assert.Throws<ArgumentException>(() => InvasionDtoAdapter.Adapt(perTown))!.Message,
                    Does.Contain("BankedTroopsPerTownHeld"));
                Assert.That(Assert.Throws<ArgumentException>(() => InvasionDtoAdapter.Adapt(cap))!.Message,
                    Does.Contain("MaxMenPerWave"));
            });
        }

        [Test]
        public void ValidateReferences_RejectsUnknownTemplateAndBanditSpawn()
        {
            InvasionData invasion = AdaptValidConfig();

            ArgumentException template = Assert.Throws<ArgumentException>(() =>
                InvasionRuntimeConfigurationValidator.ValidateReferences(invasion, "PartyTemplate.origin", false,
                    _ => false))!;
            ArgumentException bandit = Assert.Throws<ArgumentException>(() =>
                InvasionRuntimeConfigurationValidator.ValidateReferences(invasion, "PartyTemplate.origin", true,
                    _ => true))!;

            Assert.Multiple(() =>
            {
                Assert.That(template.Message, Does.Contain("unknown reinforcement party template"));
                Assert.That(bandit.Message, Does.Contain("bandit clan"));
            });
        }

        /**
         * An invasion is identified by the clan it invades for, so a spawn that would roll a different clan
         * every time it fires, or take whichever clan owns the settlement it lands at, has no one invasion.
         */
        [Test]
        public void ValidateFixedInvasionClan_RejectsAClanTheSpawnPicksRatherThanTheConfiguration()
        {
            ArgumentException overriden = Assert.Throws<ArgumentException>(() =>
                InvasionRuntimeConfigurationValidator.ValidateFixedInvasionClan("PartyTemplate.origin", 2, false))!;
            ArgumentException inherited = Assert.Throws<ArgumentException>(() =>
                InvasionRuntimeConfigurationValidator.ValidateFixedInvasionClan("PartyTemplate.origin", 0, true))!;

            Assert.Multiple(() =>
            {
                Assert.That(overriden.Message, Does.Contain("OverridenClanSpawns"));
                Assert.That(inherited.Message, Does.Contain("InheritClanFromSettlement"));
                Assert.DoesNotThrow(() => InvasionRuntimeConfigurationValidator.ValidateFixedInvasionClan(
                    "PartyTemplate.origin", 0, false));
            });
        }

        // One faction, one invasion: the first spawn to name a clan keeps it and every later one is refused.
        [Test]
        public void FindInvasionsSharingAClan_RefusesEveryInvasionAfterTheFirstToNameAClan()
        {
            IReadOnlyList<string> refused = InvasionRuntimeConfigurationValidator.FindInvasionsSharingAClan(new[]
            {
                new KeyValuePair<string, string>("northern_landing", "hunnar"),
                new KeyValuePair<string, string>("makko_landing", "makko"),
                new KeyValuePair<string, string>("southern_landing", "hunnar"),
                new KeyValuePair<string, string>("third_landing", "hunnar")
            });

            Assert.That(refused, Is.EqualTo(new[] { "southern_landing", "third_landing" }));
        }

        /**
         * Several waves of one invasion are one spawn firing again, not a second spawn, so nothing about
         * this rule stands between an invasion and the reinforcements or re-landings it is configured for.
         */
        [Test]
        public void FindInvasionsSharingAClan_AcceptsOneInvasionPerClan()
        {
            Assert.That(InvasionRuntimeConfigurationValidator.FindInvasionsSharingAClan(new[]
            {
                new KeyValuePair<string, string>("hunnar_landing", "hunnar"),
                new KeyValuePair<string, string>("makko_landing", "makko")
            }), Is.Empty);
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
                "PartyTemplate.origin", false, _ => true));
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
                Reinforcements = new InvasionReinforcementsConfig
                {
                    Rules = new List<InvasionReinforcementRuleConfig>
                    {
                        new() { Id = "infantry", PartyTemplateId = "PartyTemplate.reinforcements" }
                    }
                }
            };
        }
    }
}
