using System;
using System.Collections.Generic;
using CustomSpawns.AI;
using CustomSpawns.Data.Adapter;
using CustomSpawns.Data.Dto;
using CustomSpawns.Spawn;
using NUnit.Framework;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

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
            var resolver = new PartyBehaviourResolver(id => spawns[id],
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
            var resolver = Resolver(Spawn(PatrolConfiguration(), RaidConfiguration()), fallback, patrol, raid);

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
            var resolver = Resolver(Spawn(PatrolConfiguration(), RaidConfiguration()), fallback, patrol, raid);

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
            var resolver = new PartyBehaviourResolver(_ => spawn,
                Array.Empty<IPriorityPartyBehaviour>(), fallback);

            resolver.Resolve(null!, hasOwner ? PartyData() : PartyData(null));

            Assert.That(fallback.CallCount, Is.EqualTo(1));
        }

        [Test]
        public void Resolve_ConfiguredTypeWithoutProviderFailsClearly()
        {
            var resolver = Resolver(Spawn(RaidConfiguration()), new FallbackStub());

            InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
                resolver.Resolve(null!, PartyData()))!;

            Assert.That(error.Message, Does.Contain(typeof(RaidClosestVillageData).FullName));
        }

        [Test]
        public void Constructor_RejectsDuplicateConfigurationProviderRegistration()
        {
            var first = new ProviderStub<RaidClosestVillageData>(PartyBehaviourResult.NotApplicable);
            var duplicate = new ProviderStub<RaidClosestVillageData>(PartyBehaviourResult.NotApplicable);

            ArgumentException error = Assert.Throws<ArgumentException>(() => new PartyBehaviourResolver(
                _ => Spawn(), new IPriorityPartyBehaviour[] { first, duplicate }, new FallbackStub()))!;

            Assert.That(error.Message, Does.Contain(typeof(RaidClosestVillageData).FullName));
        }

        [Test]
        public void SyncData_DelegatesExactlyOnceToEveryProviderGenerically()
        {
            var patrol = new ProviderStub<PatrolAroundClosestLestInterruptedAndSwitchData>(
                PartyBehaviourResult.NotApplicable);
            var raid = new ProviderStub<RaidClosestVillageData>(PartyBehaviourResult.NotApplicable);
            var fallback = new FallbackStub();
            var resolver = Resolver(Spawn(), fallback, patrol, raid);

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
                ["nested"] = Spawn(PatrolConfiguration()),
                ["legacy"] = Spawn(PatrolConfiguration())
            };
            var resolver = new PartyBehaviourResolver(id => spawns[id],
                new IPriorityPartyBehaviour[] { patrol }, fallback);

            resolver.Resolve(null!, PartyData("nested"));
            resolver.Resolve(null!, PartyData("legacy"));

            Assert.That(patrol.CallCount, Is.EqualTo(2));
            Assert.That(patrol.SeenConfigurations, Has.All.TypeOf<PatrolAroundClosestLestInterruptedAndSwitchData>());
        }

        private static PartyBehaviourResolver Resolver(SpawnDto spawn, FallbackStub fallback,
            params IPriorityPartyBehaviour[] providers)
        {
            return new PartyBehaviourResolver(_ => spawn, providers, fallback);
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

        private sealed class ProviderStub<TConfiguration> : PriorityPartyBehaviour<TConfiguration>
            where TConfiguration : PriorityPartyBehaviourData
        {
            private readonly PartyBehaviourResult _result;

            public ProviderStub(PartyBehaviourResult result)
            {
                _result = result;
            }

            public int CallCount { get; private set; }
            public int SyncCount { get; private set; }
            public List<TConfiguration> SeenConfigurations { get; } = new();

            public override PartyBehaviourResult GetPriorityBehaviour(MobileParty party, CsPartyData partyData,
                TConfiguration configuration)
            {
                CallCount++;
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
