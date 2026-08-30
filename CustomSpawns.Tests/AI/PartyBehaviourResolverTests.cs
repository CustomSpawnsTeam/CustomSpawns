using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Xml.Serialization;
using CustomSpawns.AI;
using CustomSpawns.Data.Adapter;
using CustomSpawns.Data.Dao;
using CustomSpawns.Data.Dto;
using CustomSpawns.Data.Model;
using CustomSpawns.Spawn;
using CustomSpawns.Utils;
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
            var spawns = Spawns(("patrol-first", "patrol-first"), ("raid-first", "raid-first"));
            var behaviourDao = BehaviourDao(
                Template("patrol-first", PatrolConfiguration(), RaidConfiguration()),
                Template("raid-first", RaidConfiguration(), PatrolConfiguration()));
            var resolver = Resolver(SpawnDao(spawns), behaviourDao, fallback, patrol, raid);

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
            var spawns = Spawns(("spawn", "template"));
            var behaviourDao = BehaviourDao(Template("template", PatrolConfiguration(), RaidConfiguration()));
            var resolver = Resolver(SpawnDao(spawns), behaviourDao, fallback, patrol, raid);

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
            var spawns = Spawns(("spawn", "template"));
            var behaviourDao = BehaviourDao(Template("template", PatrolConfiguration(), RaidConfiguration()));
            var resolver = Resolver(SpawnDao(spawns), behaviourDao, fallback, patrol, raid);

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
            var spawns = hasOwner
                ? new Dictionary<string, SpawnDto> { ["spawn"] = new() }
                : new Dictionary<string, SpawnDto>();
            var resolver = Resolver(SpawnDao(spawns), BehaviourDao(), fallback);

            resolver.Resolve(null!, hasOwner ? PartyData() : PartyData(null));

            Assert.That(fallback.CallCount, Is.EqualTo(1));
        }

        [Test]
        public void Constructor_ConfiguredTypeWithoutProviderFailsClearly()
        {
            var behaviourDao = BehaviourDao(Template("template", RaidConfiguration()));

            InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
                Resolver(SpawnDao(new Dictionary<string, SpawnDto>()), behaviourDao, new FallbackStub()))!;

            Assert.That(error.Message, Does.Contain(typeof(RaidClosestVillageData).FullName));
        }

        [Test]
        public void Constructor_RaidProviderSatisfiesConfiguredRaidType()
        {
            var behaviourDao = BehaviourDao(Template("template", RaidConfiguration()));

            Assert.DoesNotThrow(() => Resolver(SpawnDao(new Dictionary<string, SpawnDto>()), behaviourDao,
                new FallbackStub(), new RaidClosestVillageBehaviour(
                    new BannerlordRaidClosestVillageAdapter(
                        (DynamicSpawnData)FormatterServices.GetUninitializedObject(typeof(DynamicSpawnData))),
                    (minimum, _) => minimum)));
        }

        [Test]
        public void Constructor_SiegeProviderSatisfiesConfiguredSiegeType()
        {
            var behaviourDao = BehaviourDao(Template("template", SiegeConfiguration()));

            Assert.DoesNotThrow(() => Resolver(SpawnDao(new Dictionary<string, SpawnDto>()), behaviourDao,
                new FallbackStub(), new BesiegeClosestSettlementBehaviour(
                    new BannerlordBesiegeClosestSettlementAdapter(
                        (DynamicSpawnData)FormatterServices.GetUninitializedObject(typeof(DynamicSpawnData))),
                    (minimum, _) => minimum)));
        }

        [Test]
        public void Constructor_PerformsNoSpawnLookup()
        {
            Mock<ISpawnDao> spawnDao = new(MockBehavior.Strict);
            var behaviourDao = BehaviourDao(Template("template", RaidConfiguration()));

            Resolver(spawnDao, behaviourDao, new FallbackStub(), new RaidClosestVillageBehaviour(
                    new BannerlordRaidClosestVillageAdapter(
                        (DynamicSpawnData)FormatterServices.GetUninitializedObject(typeof(DynamicSpawnData))),
                    (minimum, _) => minimum));

            spawnDao.Verify(dao => dao.FindAll(), Times.Never);
            spawnDao.Verify(dao => dao.FindByPartyTemplateId(It.IsAny<string>()), Times.Never);
        }

        [Test]
        public void Resolve_DifferingOffensiveOrdersStillReachNestedPatrolAndHomeFallback()
        {
            var siege = new ProviderStub<BesiegeClosestSettlementData>(PartyBehaviourResult.NotApplicable);
            var raid = new ProviderStub<RaidClosestVillageData>(PartyBehaviourResult.NotApplicable);
            var patrol = new ProviderStub<PatrolAroundClosestLestInterruptedAndSwitchData>(
                PartyBehaviourResult.ApplicableWithoutBehavior);
            var fallback = new FallbackStub();
            var spawns = Spawns(("siege-first", "siege-first"), ("raid-first", "raid-first"));
            spawns["home"] = new SpawnDto();
            var behaviourDao = BehaviourDao(
                Template("siege-first", SiegeConfiguration(), RaidConfiguration(), PatrolConfiguration()),
                Template("raid-first", RaidConfiguration(), SiegeConfiguration(), PatrolConfiguration()));
            var resolver = Resolver(SpawnDao(spawns), behaviourDao, fallback, patrol, raid, siege);

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
            var spawns = Spawns(("siege-owner", "siege-owner"), ("raid-owner", "raid-owner"));
            spawns["home-owner"] = new SpawnDto();
            var behaviourDao = BehaviourDao(
                XmlTemplate("siege-owner", "BesiegeClosestSettlement", "RaidClosestVillage",
                    "PatrolAroundClosestLestInterruptedAndSwitch"),
                XmlTemplate("raid-owner", "RaidClosestVillage", "BesiegeClosestSettlement",
                    "PatrolAroundClosestLestInterruptedAndSwitch"));
            var resolver = Resolver(SpawnDao(spawns), behaviourDao, fallback, patrol, siege, raid);

            resolver.Resolve(null!, PartyData("siege-owner"));
            Assert.That(calls, Is.EqualTo(new[] { "siege", "raid", "patrol" }));

            calls.Clear();
            resolver.Resolve(null!, PartyData("raid-owner"));
            Assert.That(calls, Is.EqualTo(new[] { "raid", "siege", "patrol" }));

            resolver.Resolve(null!, PartyData("home-owner"));
            Assert.That(fallback.CallCount, Is.EqualTo(1));
        }

        [Test]
        public void Registry_RejectsDuplicateConfigurationProviderRegistration()
        {
            var first = new ProviderStub<RaidClosestVillageData>(PartyBehaviourResult.NotApplicable);
            var duplicate = new ProviderStub<RaidClosestVillageData>(PartyBehaviourResult.NotApplicable);

            ArgumentException error = Assert.Throws<ArgumentException>(() =>
                new PriorityPartyBehaviourRegistry(new IPriorityPartyBehaviour[] { first, duplicate }))!;

            Assert.That(error.Message, Does.Contain(typeof(RaidClosestVillageData).FullName));
        }

        [Test]
        public void Registry_ResolvesARegisteredBehaviourByItsConfigurationType()
        {
            var raid = new ProviderStub<RaidClosestVillageData>(PartyBehaviourResult.NotApplicable);
            PriorityPartyBehaviourRegistry registry = new(new IPriorityPartyBehaviour[] { raid });

            Assert.That(registry.TryGet(typeof(RaidClosestVillageData), out IPriorityPartyBehaviour behaviour),
                Is.True);
            Assert.That(behaviour, Is.SameAs(raid));
            Assert.That(registry.TryGet(typeof(BesiegeClosestSettlementData), out _), Is.False);
        }

        [Test]
        public void Lookup_ReportsAnUnknownTemplateIdOnceAndYieldsNoBehaviours()
        {
            var spawns = Spawns(("first", "missing"), ("second", "missing"));
            Mock<MessageBoxService> messageBoxService = new();
            SpawnPartyBehaviourLookup lookup = Lookup(SpawnDao(spawns), BehaviourDao(), messageBoxService.Object);

            Assert.That(lookup.FindBehaviours("first"), Is.Empty);
            Assert.That(lookup.FindBehaviours("second"), Is.Empty);

            messageBoxService.Verify(service => service.ShowMessage(It.Is<string>(message =>
                message.Contains("missing"))), Times.Once);
        }

        [Test]
        public void Lookup_MissingOwnerOrUnknownSpawnYieldsNoBehaviours()
        {
            SpawnPartyBehaviourLookup lookup = Lookup(SpawnDao(new Dictionary<string, SpawnDto>()), BehaviourDao(),
                new Mock<MessageBoxService>().Object);

            Assert.That(lookup.FindBehaviours(null), Is.Empty);
            Assert.That(lookup.FindBehaviours("spawn"), Is.Empty);
        }

        [Test]
        public void Lookup_ListsEveryConfiguredBehaviourType()
        {
            SpawnPartyBehaviourLookup lookup = Lookup(SpawnDao(new Dictionary<string, SpawnDto>()),
                BehaviourDao(Template("template", RaidConfiguration(), PatrolConfiguration())),
                new Mock<MessageBoxService>().Object);

            Assert.That(lookup.FindAllConfiguredBehaviourTypes(), Is.EqualTo(new[]
            {
                typeof(RaidClosestVillageData), typeof(PatrolAroundClosestLestInterruptedAndSwitchData)
            }));
        }

        [Test]
        public void Resolve_NestedAndLegacyPatrolDtosReachTheSameProvider()
        {
            var patrol = new ProviderStub<PatrolAroundClosestLestInterruptedAndSwitchData>(
                PartyBehaviourResult.ApplicableWithoutBehavior);
            var fallback = new FallbackStub();
            var spawns = Spawns(("nested", "nested"));
            spawns["legacy"] = new SpawnDto
            {
                PatrolAroundClosestLestInterruptedAndSwitch = LegacyPatrol(
                    "<PatrolAroundClosestLestInterruptedAndSwitch min_stable_days=\"2\" " +
                    "max_stable_days=\"4\" try_patrol_around=\"Town\">true" +
                    "</PatrolAroundClosestLestInterruptedAndSwitch>")
            };
            var behaviourDao = BehaviourDao(XmlTemplate("nested",
                "<PatrolAroundClosestLestInterruptedAndSwitch>" +
                "<TargetTypes><TargetType>Town</TargetType></TargetTypes>" +
                "<StableTimeInDays><Minimum>2</Minimum><Maximum>4</Maximum></StableTimeInDays>" +
                "</PatrolAroundClosestLestInterruptedAndSwitch>"));
            var resolver = Resolver(SpawnDao(spawns), behaviourDao, fallback, patrol);

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

        [Test]
        public void Resolve_SpawnsSharingATemplateCannotContaminateEachOther()
        {
            var patrol = new ProviderStub<PatrolAroundClosestLestInterruptedAndSwitchData>(
                PartyBehaviourResult.NotApplicable);
            var spawns = Spawns(("first", "shared"), ("second", "shared"));
            var behaviourDao = BehaviourDao(XmlTemplate("shared",
                "<PatrolAroundClosestLestInterruptedAndSwitch>" +
                "<TargetTypes><TargetType>Town</TargetType></TargetTypes>" +
                "</PatrolAroundClosestLestInterruptedAndSwitch>"));
            var resolver = Resolver(SpawnDao(spawns), behaviourDao, new FallbackStub(), patrol);

            resolver.Resolve(null!, PartyData("first"));
            resolver.Resolve(null!, PartyData("second"));

            Assert.That(patrol.SeenConfigurations[0].PreferredSettlementTypes,
                Is.EqualTo(patrol.SeenConfigurations[1].PreferredSettlementTypes));
            IList<SpawnSettlementType> shared =
                (IList<SpawnSettlementType>)patrol.SeenConfigurations[0].PreferredSettlementTypes;
            Assert.That(shared.IsReadOnly, Is.True);
            Assert.Throws<NotSupportedException>(() => shared.Add(SpawnSettlementType.Village));
            Assert.That(patrol.SeenConfigurations[1].PreferredSettlementTypes,
                Is.EqualTo(new[] { SpawnSettlementType.Town }));
        }

        [Test]
        public void Constructor_CopiesPatrolSettlementTypesFromItsCaller()
        {
            List<SpawnSettlementType> source = new() { SpawnSettlementType.Town };
            PatrolAroundClosestLestInterruptedAndSwitchData patrol = new(1f, 2f, source);

            source.Add(SpawnSettlementType.Village);

            Assert.That(patrol.PreferredSettlementTypes, Is.EqualTo(new[] { SpawnSettlementType.Town }));
        }

        [Test]
        public void Resolve_UnknownTemplateIdIsReportedOnceAndFallsBack()
        {
            var fallback = new FallbackStub();
            var spawns = Spawns(("first", "missing"), ("second", "missing"));
            Mock<MessageBoxService> messageBoxService = new();
            var resolver = new PartyBehaviourResolver(
                Lookup(SpawnDao(spawns), BehaviourDao(), messageBoxService.Object),
                new PriorityPartyBehaviourRegistry(Array.Empty<IPriorityPartyBehaviour>()), fallback,
                new SupportingStub());

            resolver.Resolve(null!, PartyData("first"));
            resolver.Resolve(null!, PartyData("second"));

            Assert.That(fallback.CallCount, Is.EqualTo(2));
            messageBoxService.Verify(service => service.ShowMessage(It.Is<string>(message =>
                message.Contains("missing"))), Times.Once);
        }

        [Test]
        public void Resolve_SupportingPartyEscortsInsteadOfUsingItsSpawnsBehaviours()
        {
            var patrol = new ProviderStub<PatrolAroundClosestLestInterruptedAndSwitchData>(
                PartyBehaviourResult.From(default));
            var fallback = new FallbackStub();
            var supporting = new SupportingStub(PartyBehaviourResult.From(default));
            var spawns = Spawns(("spawn", "template"));
            var behaviourDao = BehaviourDao(Template("template", PatrolConfiguration()));
            var resolver = Resolver(SpawnDao(spawns), behaviourDao, fallback, supporting, patrol);

            PartyBehaviourResult result = resolver.Resolve(null!, SupportingPartyData());

            Assert.That(result.AIBehavior.HasValue, Is.True);
            Assert.That(supporting.CallCount, Is.EqualTo(1));
            Assert.That(patrol.CallCount, Is.Zero);
            Assert.That(fallback.CallCount, Is.Zero);
        }

        [Test]
        public void Resolve_SupportingPartyWithoutMainPartyFallsBackWithoutItsSpawnsBehaviours()
        {
            var patrol = new ProviderStub<PatrolAroundClosestLestInterruptedAndSwitchData>(
                PartyBehaviourResult.From(default));
            var fallback = new FallbackStub();
            var supporting = new SupportingStub(PartyBehaviourResult.NotApplicable);
            var spawns = Spawns(("spawn", "template"));
            var behaviourDao = BehaviourDao(Template("template", PatrolConfiguration()));
            var resolver = Resolver(SpawnDao(spawns), behaviourDao, fallback, supporting, patrol);

            resolver.Resolve(null!, SupportingPartyData());

            Assert.That(supporting.CallCount, Is.EqualTo(1));
            Assert.That(patrol.CallCount, Is.Zero);
            Assert.That(fallback.CallCount, Is.EqualTo(1));
        }

        private static PartyBehaviourResolver Resolver(Mock<ISpawnDao> spawnDao,
            Mock<IPartyBehaviourDao> partyBehaviourDao, IFallbackPartyBehaviour fallback,
            params IPriorityPartyBehaviour[] providers)
        {
            return Resolver(spawnDao, partyBehaviourDao, fallback, new SupportingStub(), providers);
        }

        private static PartyBehaviourResolver Resolver(Mock<ISpawnDao> spawnDao,
            Mock<IPartyBehaviourDao> partyBehaviourDao, IFallbackPartyBehaviour fallback,
            ISupportingPartyBehaviour supporting, params IPriorityPartyBehaviour[] providers)
        {
            return new PartyBehaviourResolver(
                Lookup(spawnDao, partyBehaviourDao, new Mock<MessageBoxService>().Object),
                new PriorityPartyBehaviourRegistry(providers), fallback, supporting);
        }

        private static SpawnPartyBehaviourLookup Lookup(Mock<ISpawnDao> spawnDao,
            Mock<IPartyBehaviourDao> partyBehaviourDao, MessageBoxService messageBoxService)
        {
            return new SpawnPartyBehaviourLookup(spawnDao.Object, partyBehaviourDao.Object, messageBoxService);
        }

        private static Dictionary<string, SpawnDto> Spawns(params (string PartyTemplate, string TemplateId)[] spawns)
        {
            return spawns.ToDictionary(spawn => spawn.PartyTemplate,
                spawn => new SpawnDto { PriorityPartyBehaviourTemplateId = spawn.TemplateId });
        }

        private static Mock<ISpawnDao> SpawnDao(IDictionary<string, SpawnDto> spawns)
        {
            Mock<ISpawnDao> spawnDao = new();
            spawnDao.Setup(dao => dao.FindByPartyTemplateId(It.IsAny<string>()))
                .Returns((string id) => spawns.TryGetValue(id, out SpawnDto spawn) ? spawn : null);
            return spawnDao;
        }

        private static Mock<IPartyBehaviourDao> BehaviourDao(params PartyBehaviourDto[] partyBehaviours)
        {
            Mock<IPartyBehaviourDao> partyBehaviourDao = new();
            partyBehaviourDao.Setup(dao => dao.FindAll()).Returns(partyBehaviours.ToList());
            partyBehaviourDao.Setup(dao => dao.FindById(It.IsAny<string>()))
                .Returns((string id) => partyBehaviours.FirstOrDefault(partyBehaviour => partyBehaviour.Id == id));
            return partyBehaviourDao;
        }

        private static PartyBehaviourDto Template(string id, params PriorityPartyBehaviourData[] behaviours)
        {
            return new PartyBehaviourDto { Id = id, PriorityPartyBehaviours = behaviours };
        }

        private static CsPartyData PartyData(string? owner = "spawn")
        {
            return new CsPartyData("party", owner, true, null);
        }

        private static CsPartyData SupportingPartyData(string? owner = "spawn")
        {
            return new CsPartyData("party", owner, false, null);
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

        private static PartyBehaviourDto XmlTemplate(string id, params string[] elements)
        {
            return XmlTemplateFromBody(id, string.Concat(Array.ConvertAll(elements, element =>
                element == "BesiegeClosestSettlement"
                    ? "<BesiegeClosestSettlement><TargetTypes><TargetType>Town</TargetType>" +
                      "<TargetType>Castle</TargetType></TargetTypes></BesiegeClosestSettlement>"
                    : element.StartsWith("<") ? element : "<" + element + "/>")));
        }

        private static PartyBehaviourDto XmlTemplateFromBody(string id, string behaviours)
        {
            string xml = "<PriorityPartyBehaviourTemplate><Id>" + id + "</Id>" +
                         "<PriorityPartyBehaviours>" + behaviours + "</PriorityPartyBehaviours>" +
                         "</PriorityPartyBehaviourTemplate>";
            XmlSerializer serializer = new(typeof(PriorityPartyBehaviourTemplate));
            using StringReader reader = new(xml);
            PriorityPartyBehaviourTemplate template = (PriorityPartyBehaviourTemplate)serializer.Deserialize(reader);
            return new PartyBehaviourDtoAdapter(PartyBehaviourRegistrations.ConfigAdapters).Adapt(template);
        }

        private static PatrolAroundClosestLestInterruptedAndSwitchData LegacyPatrol(string patrolElement)
        {
            string xml = "<RegularBanditDailySpawnData party_template=\"PartyTemplate.test\">" +
                         patrolElement + "</RegularBanditDailySpawnData>";
            XmlSerializer serializer = new(typeof(SpawnModel));
            using StringReader reader = new(xml);
            SpawnModel spawn = (SpawnModel)serializer.Deserialize(reader);
            return SpawnDtoAdapter.AdaptLegacyPatrol(spawn)!;
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
        }

        private sealed class SupportingStub : ISupportingPartyBehaviour
        {
            private readonly PartyBehaviourResult _result;

            public SupportingStub(PartyBehaviourResult result = default)
            {
                _result = result;
            }

            public int CallCount { get; private set; }

            public PartyBehaviourResult GetPriorityBehaviour(MobileParty party, CsPartyData partyData)
            {
                CallCount++;
                return _result;
            }
        }

        private sealed class FallbackStub : IFallbackPartyBehaviour
        {
            public int CallCount { get; private set; }

            public PartyBehaviourResult GetPriorityBehaviour(MobileParty party, CsPartyData partyData)
            {
                CallCount++;
                return PartyBehaviourResult.ApplicableWithoutBehavior;
            }

            public void SyncData(IDataStore dataStore)
            {
            }
        }
    }
}
