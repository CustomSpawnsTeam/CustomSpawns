using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using CustomSpawns.AI;
using CustomSpawns.Data.Adapter;
using CustomSpawns.Data.Dao;
using CustomSpawns.Data.Dto;
using CustomSpawns.Spawn;
using Moq;
using NUnit.Framework;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using SpawnModel = CustomSpawns.Data.Model.Spawn;

namespace CustomSpawns.Tests.AI
{
    public class PartyBehaviourResolverTests
    {
        [Test]
        public void Resolve_UsesEachSpawnDtosOwnConfiguredOrder()
        {
            var patrol = new ProviderStub<PatrolAroundClosestLestInterruptedAndSwitchData>(
                PartyBehaviourResult.ApplicableWithoutBehavior);
            var raid = new ProviderStub<RaidClosestVillageData>(PartyBehaviourResult.From(default));
            var fallback = new FallbackStub();
            var spawns = new Dictionary<string, SpawnDto>
            {
                ["patrol-first"] = Spawn(PatrolConfiguration(), RaidConfiguration()),
                ["raid-first"] = Spawn(RaidConfiguration(), PatrolConfiguration())
            };
            Mock<ISpawnDao> spawnDao = new();
            spawnDao.Setup(x => x.FindAll()).Returns(spawns.Values.ToList());
            var resolver = new PartyBehaviourResolver(id => spawns[id], spawnDao.Object,
                new IPriorityPartyBehaviour[] { patrol, raid }, fallback);

            PartyBehaviourResult patrolFirst = resolver.Resolve(null!, PartyData("patrol-first"));
            PartyBehaviourResult raidFirst = resolver.Resolve(null!, PartyData("raid-first"));

            Assert.That(patrolFirst.AIBehavior, Is.Null);
            Assert.That(raidFirst.AIBehavior.HasValue, Is.True);
            Assert.That(patrol.CallCount, Is.EqualTo(1));
            Assert.That(raid.CallCount, Is.EqualTo(1));
            Assert.That(fallback.CallCount, Is.Zero);
        }

        [Test]
        public void Resolve_NotApplicableFallsThroughToNextConfiguredProvider()
        {
            var patrol = new ProviderStub<PatrolAroundClosestLestInterruptedAndSwitchData>(
                PartyBehaviourResult.NotApplicable);
            var raid = new ProviderStub<RaidClosestVillageData>(PartyBehaviourResult.From(default));
            var fallback = new FallbackStub();
            var spawn = Spawn(PatrolConfiguration(), RaidConfiguration());
            Mock<ISpawnDao> spawnDao = new();
            spawnDao.Setup(x => x.FindAll()).Returns(new List<SpawnDto> { spawn });
            var resolver = Resolver(spawn, spawnDao.Object, fallback, patrol, raid);

            PartyBehaviourResult result = resolver.Resolve(null!, PartyData());

            Assert.That(result.AIBehavior.HasValue, Is.True);
            Assert.That(patrol.CallCount, Is.EqualTo(1));
            Assert.That(raid.CallCount, Is.EqualTo(1));
            Assert.That(fallback.CallCount, Is.Zero);
        }

        [Test]
        public void Resolve_ApplicableWithoutBehaviorBlocksLaterProvidersAndFallback()
        {
            var patrol = new ProviderStub<PatrolAroundClosestLestInterruptedAndSwitchData>(
                PartyBehaviourResult.ApplicableWithoutBehavior);
            var raid = new ProviderStub<RaidClosestVillageData>(PartyBehaviourResult.From(default));
            var fallback = new FallbackStub();
            var spawn = Spawn(PatrolConfiguration(), RaidConfiguration());
            Mock<ISpawnDao> spawnDao = new();
            spawnDao.Setup(x => x.FindAll()).Returns(new List<SpawnDto> { spawn });
            var resolver = Resolver(spawn, spawnDao.Object, fallback, patrol, raid);

            PartyBehaviourResult result = resolver.Resolve(null!, PartyData());

            Assert.That(result.IsApplicable, Is.True);
            Assert.That(result.AIBehavior, Is.Null);
            Assert.That(raid.CallCount, Is.Zero);
            Assert.That(fallback.CallCount, Is.Zero);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Resolve_MissingOwnerOrEmptyConfigurationUsesHomeFallback(bool hasOwner)
        {
            var fallback = new FallbackStub();
            SpawnDto? spawn = hasOwner ? Spawn() : null;

            Mock<ISpawnDao> spawnDao = new();
            spawnDao.Setup(x => x.FindAll()).Returns(spawn is null ? Array.Empty<SpawnDto>() : new[] { spawn });

            var resolver = new PartyBehaviourResolver(_ => spawn, spawnDao.Object,
                Array.Empty<IPriorityPartyBehaviour>(), fallback);

            resolver.Resolve(null!, hasOwner ? PartyData() : PartyData(null));

            Assert.That(fallback.CallCount, Is.EqualTo(1));
        }

        [Test]
        public void Constructor_ConfiguredTypeWithoutProviderFailsClearly()
        {
            SpawnDto spawn = Spawn(RaidConfiguration());
            Mock<ISpawnDao> spawnDao = new();
            spawnDao.Setup(x => x.FindAll()).Returns(new List<SpawnDto> { spawn });

            InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
                new PartyBehaviourResolver(_ => spawn, spawnDao.Object,
                    Array.Empty<IPriorityPartyBehaviour>(), new FallbackStub()))!;

            Assert.That(error.Message, Does.Contain(typeof(RaidClosestVillageData).FullName));
        }

        [Test]
        public void Constructor_RaidProviderSatisfiesConfiguredRaidType()
        {
            SpawnDto spawn = Spawn(RaidConfiguration());
            Mock<ISpawnDao> spawnDao = new();
            spawnDao.Setup(x => x.FindAll()).Returns(new List<SpawnDto> { spawn });

            Assert.DoesNotThrow(() => new PartyBehaviourResolver(_ => spawn, spawnDao.Object,
                new IPriorityPartyBehaviour[] { new RaidClosestVillageBehaviour() }, new FallbackStub()));
        }

        [Test]
        public void Constructor_SiegeProviderSatisfiesConfiguredSiegeType()
        {
            SpawnDto spawn = Spawn(SiegeConfiguration());
            Mock<ISpawnDao> spawnDao = new();
            spawnDao.Setup(x => x.FindAll()).Returns(new List<SpawnDto> { spawn });

            Assert.DoesNotThrow(() => new PartyBehaviourResolver(_ => spawn, spawnDao.Object,
                new IPriorityPartyBehaviour[] { new BesiegeClosestSettlementBehaviour() }, new FallbackStub()));
        }

        [Test]
        public void Resolve_DifferingOffensiveOrdersStillReachNestedPatrolAndHomeFallback()
        {
            var siege = new ProviderStub<BesiegeClosestSettlementData>(PartyBehaviourResult.NotApplicable);
            var raid = new ProviderStub<RaidClosestVillageData>(PartyBehaviourResult.NotApplicable);
            var patrol = new ProviderStub<PatrolAroundClosestLestInterruptedAndSwitchData>(
                PartyBehaviourResult.ApplicableWithoutBehavior);
            var fallback = new FallbackStub();
            var spawns = new Dictionary<string, SpawnDto>
            {
                ["siege-first"] = Spawn(SiegeConfiguration(), RaidConfiguration(), PatrolConfiguration()),
                ["raid-first"] = Spawn(RaidConfiguration(), SiegeConfiguration(), PatrolConfiguration()),
                ["home"] = Spawn()
            };
            Mock<ISpawnDao> spawnDao = new();
            spawnDao.Setup(x => x.FindAll()).Returns(spawns.Values.ToList());
            var resolver = new PartyBehaviourResolver(id => spawns[id], spawnDao.Object,
                new IPriorityPartyBehaviour[] { patrol, raid, siege }, fallback);

            resolver.Resolve(null!, PartyData("siege-first"));
            resolver.Resolve(null!, PartyData("raid-first"));
            resolver.Resolve(null!, PartyData("home"));

            Assert.That(siege.CallCount, Is.EqualTo(2));
            Assert.That(raid.CallCount, Is.EqualTo(2));
            Assert.That(patrol.CallCount, Is.EqualTo(2));
            Assert.That(fallback.CallCount, Is.EqualTo(1));
            Assert.That(siege.SeenConfigurations, Has.All.TypeOf<BesiegeClosestSettlementData>());
        }

        [Test]
        public void Resolve_TwoXmlOrdersControlSupportingPartyPriorityBeforePatrolAndHomeFallback()
        {
            var calls = new List<string>();
            var siege = new ProviderStub<BesiegeClosestSettlementData>(PartyBehaviourResult.NotApplicable,
                calls, "siege");
            var raid = new ProviderStub<RaidClosestVillageData>(PartyBehaviourResult.NotApplicable,
                calls, "raid");
            var patrol = new ProviderStub<PatrolAroundClosestLestInterruptedAndSwitchData>(
                PartyBehaviourResult.ApplicableWithoutBehavior, calls, "patrol");
            var fallback = new FallbackStub();
            var spawns = new Dictionary<string, SpawnDto>
            {
                ["siege-owner"] = Spawn(XmlConfigurations("BesiegeClosestSettlement",
                    "RaidClosestVillage", "PatrolAroundClosestLestInterruptedAndSwitch")),
                ["raid-owner"] = Spawn(XmlConfigurations("RaidClosestVillage",
                    "BesiegeClosestSettlement", "PatrolAroundClosestLestInterruptedAndSwitch")),
                ["home-owner"] = Spawn()
            };
            Mock<ISpawnDao> spawnDao = new();
            spawnDao.Setup(x => x.FindAll()).Returns(spawns.Values.ToList());
            var resolver = new PartyBehaviourResolver(id => spawns[id], spawnDao.Object,
                new IPriorityPartyBehaviour[] { patrol, siege, raid }, fallback);

            resolver.Resolve(null!, PartyData("siege-owner"));
            Assert.That(calls, Is.EqualTo(new[] { "siege", "raid", "patrol" }));

            calls.Clear();
            resolver.Resolve(null!, PartyData("raid-owner"));
            Assert.That(calls, Is.EqualTo(new[] { "raid", "siege", "patrol" }));

            resolver.Resolve(null!, PartyData("home-owner"));
            Assert.That(fallback.CallCount, Is.EqualTo(1));
        }

        [Test]
        public void Constructor_RejectsDuplicateConfigurationProviderRegistration()
        {
            var first = new ProviderStub<RaidClosestVillageData>(PartyBehaviourResult.NotApplicable);
            var duplicate = new ProviderStub<RaidClosestVillageData>(PartyBehaviourResult.NotApplicable);
            Mock<ISpawnDao> spawnDao = new();
            spawnDao.Setup(x => x.FindAll()).Returns(new List<SpawnDto>());

            ArgumentException error = Assert.Throws<ArgumentException>(() => new PartyBehaviourResolver(
                _ => Spawn(), spawnDao.Object,
                new IPriorityPartyBehaviour[] { first, duplicate }, new FallbackStub()))!;

            Assert.That(error.Message, Does.Contain(typeof(RaidClosestVillageData).FullName));
        }

        [Test]
        public void SyncData_DelegatesExactlyOnceToEveryProviderGenerically()
        {
            var patrol = new ProviderStub<PatrolAroundClosestLestInterruptedAndSwitchData>(
                PartyBehaviourResult.NotApplicable);
            var raid = new ProviderStub<RaidClosestVillageData>(PartyBehaviourResult.NotApplicable);
            var fallback = new FallbackStub();
            Mock<ISpawnDao> spawnDao = new();
            spawnDao.Setup(x => x.FindAll()).Returns(new List<SpawnDto> { Spawn() });
            var resolver = Resolver(Spawn(), spawnDao.Object, fallback, patrol, raid);

            resolver.SyncData(null!);

            Assert.That(patrol.SyncCount, Is.EqualTo(1));
            Assert.That(raid.SyncCount, Is.EqualTo(1));
            Assert.That(fallback.SyncCount, Is.EqualTo(1));
        }

        [Test]
        public void Resolve_NestedAndLegacyPatrolDtosReachTheSameProvider()
        {
            var patrol = new ProviderStub<PatrolAroundClosestLestInterruptedAndSwitchData>(
                PartyBehaviourResult.ApplicableWithoutBehavior);
            var fallback = new FallbackStub();
            var spawns = new Dictionary<string, SpawnDto>
            {
                ["nested"] = Spawn(XmlConfigurations(
                    "<PriorityPartyBehaviours>" +
                    "<PatrolAroundClosestLestInterruptedAndSwitch>" +
                    "<TargetTypes><TargetType>Town</TargetType></TargetTypes>" +
                    "<StableTimeInDays><Minimum>2</Minimum><Maximum>4</Maximum></StableTimeInDays>" +
                    "</PatrolAroundClosestLestInterruptedAndSwitch>" +
                    "</PriorityPartyBehaviours>")),
                ["legacy"] = Spawn(XmlConfigurations(
                    "<PatrolAroundClosestLestInterruptedAndSwitch min_stable_days=\"2\" " +
                    "max_stable_days=\"4\" try_patrol_around=\"Town\">true" +
                    "</PatrolAroundClosestLestInterruptedAndSwitch>"))
            };
            Mock<ISpawnDao> spawnDao = new();
            spawnDao.Setup(x => x.FindAll()).Returns(spawns.Values.ToList());
            var resolver = new PartyBehaviourResolver(id => spawns[id], spawnDao.Object,
                new IPriorityPartyBehaviour[] { patrol }, fallback);

            resolver.Resolve(null!, PartyData("nested"));
            resolver.Resolve(null!, PartyData("legacy"));

            Assert.That(patrol.CallCount, Is.EqualTo(2));
            Assert.That(patrol.SeenConfigurations, Has.All.TypeOf<PatrolAroundClosestLestInterruptedAndSwitchData>());
            Assert.That(patrol.SeenConfigurations[0].MinStableDays, Is.EqualTo(2f));
            Assert.That(patrol.SeenConfigurations[1].MinStableDays, Is.EqualTo(2f));
            Assert.That(patrol.SeenConfigurations[0].PreferredSettlementTypes,
                Is.EqualTo(new[] { SpawnSettlementType.Town }));
            Assert.That(patrol.SeenConfigurations[1].PreferredSettlementTypes,
                Is.EqualTo(new[] { SpawnSettlementType.Town }));
        }

        private static PartyBehaviourResolver Resolver(SpawnDto spawn, ISpawnDao spawnDao, FallbackStub fallback,
            params IPriorityPartyBehaviour[] providers)
        {
            return new PartyBehaviourResolver(_ => spawn, spawnDao, providers, fallback);
        }

        private static SpawnDto Spawn(params PriorityPartyBehaviourData[] configurations)
        {
            return new SpawnDto { PriorityPartyBehaviours = configurations };
        }

        private static CsPartyData PartyData(string? owner = "spawn")
        {
            return new CsPartyData("party", owner, true, null);
        }

        private static PatrolAroundClosestLestInterruptedAndSwitchData PatrolConfiguration()
        {
            return new PatrolAroundClosestLestInterruptedAndSwitchData(1f, 2f,
                new List<SpawnSettlementType>());
        }

        private static RaidClosestVillageData RaidConfiguration()
        {
            return new RaidClosestVillageData(1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1, 1f);
        }

        private static BesiegeClosestSettlementData SiegeConfiguration()
        {
            return new BesiegeClosestSettlementData(
                new[] { SpawnSettlementType.Town, SpawnSettlementType.Castle },
                1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1, 1f);
        }

        private static PriorityPartyBehaviourData[] XmlConfigurations(params string[] elements)
        {
            string xml = "<RegularBanditDailySpawnData party_template=\"PartyTemplate.test\">" +
                         "<PriorityPartyBehaviours>" +
                         string.Concat(Array.ConvertAll(elements, element =>
                             element == "BesiegeClosestSettlement"
                                 ? "<BesiegeClosestSettlement><TargetTypes><TargetType>Town</TargetType>" +
                                   "<TargetType>Castle</TargetType></TargetTypes></BesiegeClosestSettlement>"
                                 : "<" + element + "/>")) +
                         "</PriorityPartyBehaviours></RegularBanditDailySpawnData>";
            XmlSerializer serializer = new(typeof(SpawnModel));
            using StringReader reader = new(xml);
            SpawnModel spawn = (SpawnModel)serializer.Deserialize(reader);
            var adapted = SpawnDtoAdapter.AdaptPriorityPartyBehaviours(spawn);
            var result = new PriorityPartyBehaviourData[adapted.Count];
            for (int i = 0; i < adapted.Count; i++)
            {
                result[i] = adapted[i];
            }
            return result;
        }

        private static PriorityPartyBehaviourData[] XmlConfigurations(string spawnBody)
        {
            string xml = "<RegularBanditDailySpawnData party_template=\"PartyTemplate.test\">" +
                         spawnBody + "</RegularBanditDailySpawnData>";
            XmlSerializer serializer = new(typeof(SpawnModel));
            using StringReader reader = new(xml);
            SpawnModel spawn = (SpawnModel)serializer.Deserialize(reader);
            IReadOnlyList<PriorityPartyBehaviourData> adapted =
                SpawnDtoAdapter.AdaptPriorityPartyBehaviours(spawn);
            var result = new PriorityPartyBehaviourData[adapted.Count];
            for (int i = 0; i < adapted.Count; i++)
            {
                result[i] = adapted[i];
            }
            return result;
        }

        private sealed class ProviderStub<TConfiguration> : PriorityPartyBehaviour<TConfiguration>
            where TConfiguration : PriorityPartyBehaviourData
        {
            private readonly PartyBehaviourResult _result;
            private readonly List<string>? _calls;
            private readonly string? _name;

            public ProviderStub(PartyBehaviourResult result, List<string>? calls = null,
                string? name = null)
            {
                _result = result;
                _calls = calls;
                _name = name;
            }

            public int CallCount { get; private set; }
            public int SyncCount { get; private set; }
            public List<TConfiguration> SeenConfigurations { get; } = new();

            public override PartyBehaviourResult GetPriorityBehaviour(MobileParty party, CsPartyData partyData,
                TConfiguration configuration)
            {
                CallCount++;
                if (_name is not null)
                {
                    _calls!.Add(_name);
                }
                SeenConfigurations.Add(configuration);
                return _result;
            }

            public override void SyncData(IDataStore dataStore)
            {
                SyncCount++;
            }
        }

        private sealed class FallbackStub : IFallbackPartyBehaviour
        {
            public int CallCount { get; private set; }
            public int SyncCount { get; private set; }

            public PartyBehaviourResult GetPriorityBehaviour(MobileParty party, CsPartyData partyData)
            {
                CallCount++;
                return PartyBehaviourResult.ApplicableWithoutBehavior;
            }

            public void SyncData(IDataStore dataStore)
            {
                SyncCount++;
            }
        }
    }
}
