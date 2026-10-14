using System;
using System.Collections.Generic;
using System.Linq;
using CustomSpawns.AI.Adapters;
using CustomSpawns.AI.Behaviours;
using CustomSpawns.AI.Model;
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
    public sealed class PartyBehaviourRegistration
    {
        private readonly Func<DynamicSpawnData, ISiegeTargetPolicy,
            PartyStateStore<SettlementOffensiveBehaviourState>, IPriorityPartyBehaviour> _createBehaviour;

        public PartyBehaviourRegistration(IPriorityPartyBehaviourConfigAdapter configAdapter,
            Func<DynamicSpawnData, ISiegeTargetPolicy,
                PartyStateStore<SettlementOffensiveBehaviourState>, IPriorityPartyBehaviour> createBehaviour)
        {
            ConfigAdapter = configAdapter ?? throw new ArgumentNullException(nameof(configAdapter));
            _createBehaviour = createBehaviour ?? throw new ArgumentNullException(nameof(createBehaviour));
        }

        public IPriorityPartyBehaviourConfigAdapter ConfigAdapter { get; }

        public IPriorityPartyBehaviour CreateBehaviour(DynamicSpawnData dynamicSpawnData,
            ISiegeTargetPolicy siegeTargetPolicy,
            PartyStateStore<SettlementOffensiveBehaviourState> siegeStates)
        {
            return _createBehaviour(dynamicSpawnData, siegeTargetPolicy, siegeStates);
        }
    }

    /**
     * The single place a priority party behaviour is registered. Pairing the configuration adapter with the
     * behaviour that consumes its output is what keeps the two from drifting apart: a behaviour whose
     * configuration cannot be read, or a configuration nothing acts on, cannot be registered here.
     * The behaviour is created lazily because it needs campaign state the composition root builds later.
     */
    public static class PartyBehaviourRegistrations
    {
        public static readonly IReadOnlyList<PartyBehaviourRegistration> All = new[]
        {
            new PartyBehaviourRegistration(
                new PatrolAroundClosestLestInterruptedAndSwitchConfigAdapter(),
                (_, _, _) => new PatrolAroundClosestLestInterruptedAndSwitchBehaviour()),
            new PartyBehaviourRegistration(
                new RaidClosestVillageConfigAdapter(),
                (dynamicSpawnData, _, _) => new RaidClosestVillageBehaviour(
                    new BannerlordRaidClosestVillageAdapter(dynamicSpawnData), MBRandom.RandomFloatRanged)),
            new PartyBehaviourRegistration(
                new BesiegeClosestSettlementConfigAdapter(),
                (dynamicSpawnData, siegeTargetPolicy, siegeStates) => new BesiegeClosestSettlementBehaviour(
                    new BannerlordBesiegeClosestSettlementAdapter(dynamicSpawnData, siegeTargetPolicy),
                    MBRandom.RandomFloatRanged, siegeStates))
        };

        public static IEnumerable<IPriorityPartyBehaviourConfigAdapter> ConfigAdapters =>
            All.Select(registration => registration.ConfigAdapter);

        public static IPriorityPartyBehaviour[] CreateBehaviours(DynamicSpawnData dynamicSpawnData,
            ISiegeTargetPolicy siegeTargetPolicy,
            PartyStateStore<SettlementOffensiveBehaviourState> siegeStates)
        {
            return All.Select(registration => registration.CreateBehaviour(dynamicSpawnData,
                siegeTargetPolicy, siegeStates)).ToArray();
        }
    }
}
