using System;
using System.Collections.Generic;
using System.Linq;
using CustomSpawns.AI.Adapters;
using CustomSpawns.AI.Behaviours;
using CustomSpawns.AI.Offensives;
using CustomSpawns.Data.Adapter;
using CustomSpawns.Spawn;
using TaleWorlds.Core;

namespace CustomSpawns
{
    /**
     * One priority party behaviour: the adapter that reads its configuration and the behaviour that acts on
     * the data that adapter produces.
     */
    internal sealed class PartyBehaviourRegistration
    {
        private readonly Func<DynamicSpawnData, ISiegeTargetPolicy, IPriorityPartyBehaviour>
            _createBehaviour;

        internal PartyBehaviourRegistration(IPriorityPartyBehaviourConfigAdapter configAdapter,
            Func<DynamicSpawnData, ISiegeTargetPolicy, IPriorityPartyBehaviour> createBehaviour)
        {
            ConfigAdapter = configAdapter ?? throw new ArgumentNullException(nameof(configAdapter));
            _createBehaviour = createBehaviour ?? throw new ArgumentNullException(nameof(createBehaviour));
        }

        internal IPriorityPartyBehaviourConfigAdapter ConfigAdapter { get; }

        internal IPriorityPartyBehaviour CreateBehaviour(DynamicSpawnData dynamicSpawnData,
            ISiegeTargetPolicy siegeTargetPolicy)
        {
            return _createBehaviour(dynamicSpawnData, siegeTargetPolicy);
        }
    }

    /**
     * The single place a priority party behaviour is registered. Pairing the configuration adapter with the
     * behaviour that consumes its output is what keeps the two from drifting apart: a behaviour whose
     * configuration cannot be read, or a configuration nothing acts on, cannot be registered here.
     * The behaviour is created lazily because it needs campaign state the composition root builds later.
     */
    internal static class PartyBehaviourRegistrations
    {
        internal static readonly IReadOnlyList<PartyBehaviourRegistration> All = new[]
        {
            new PartyBehaviourRegistration(
                new PatrolAroundClosestLestInterruptedAndSwitchConfigAdapter(),
                (_, _) => new PatrolAroundClosestLestInterruptedAndSwitchBehaviour()),
            new PartyBehaviourRegistration(
                new RaidClosestVillageConfigAdapter(),
                (dynamicSpawnData, _) => new RaidClosestVillageBehaviour(
                    new BannerlordRaidClosestVillageAdapter(dynamicSpawnData), MBRandom.RandomFloatRanged)),
            new PartyBehaviourRegistration(
                new BesiegeClosestSettlementConfigAdapter(),
                (dynamicSpawnData, siegeTargetPolicy) => new BesiegeClosestSettlementBehaviour(
                    new BannerlordBesiegeClosestSettlementAdapter(dynamicSpawnData, siegeTargetPolicy),
                    MBRandom.RandomFloatRanged))
        };

        internal static IEnumerable<IPriorityPartyBehaviourConfigAdapter> ConfigAdapters =>
            All.Select(registration => registration.ConfigAdapter);

        internal static IPriorityPartyBehaviour[] CreateBehaviours(DynamicSpawnData dynamicSpawnData,
            ISiegeTargetPolicy siegeTargetPolicy)
        {
            return All.Select(registration => registration.CreateBehaviour(dynamicSpawnData,
                siegeTargetPolicy)).ToArray();
        }
    }
}
