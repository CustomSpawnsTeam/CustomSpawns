using System;
using System.Collections.Generic;
using CustomSpawns.Data.Dao;
using CustomSpawns.Data.Dto;
using CustomSpawns.Spawn;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace CustomSpawns.AI
{
    public sealed class PartyBehaviourResolver
    {
        private readonly Func<string, SpawnDto?> _findSpawn;
        private readonly IReadOnlyDictionary<Type, IPriorityPartyBehaviour> _providers;
        private readonly IFallbackPartyBehaviour _fallback;
        private readonly IReadOnlyList<Action<IDataStore>> _syncActions;

        public PartyBehaviourResolver(SpawnDao spawnDao,
            IEnumerable<IPriorityPartyBehaviour> providers, IFallbackPartyBehaviour fallback)
            : this(spawnDao.FindByPartyTemplateId, spawnDao.FindAll(), providers, fallback)
        {
        }

        internal PartyBehaviourResolver(Func<string, SpawnDto?> findSpawn,
            IEnumerable<SpawnDto> spawnCatalog, IEnumerable<IPriorityPartyBehaviour> providers,
            IFallbackPartyBehaviour fallback)
        {
            _findSpawn = findSpawn ?? throw new ArgumentNullException(nameof(findSpawn));
            _fallback = fallback ?? throw new ArgumentNullException(nameof(fallback));

            var providerRegistry = new Dictionary<Type, IPriorityPartyBehaviour>();
            var syncActions = new List<Action<IDataStore>>();
            var distinctProviders = new HashSet<object>(ReferenceEqualityComparer.Instance);
            foreach (IPriorityPartyBehaviour provider in providers ??
                     throw new ArgumentNullException(nameof(providers)))
            {
                if (provider is null)
                {
                    throw new ArgumentException("Priority party behaviour providers cannot contain null.",
                        nameof(providers));
                }

                Type configurationType = provider.ConfigurationType;
                if (!typeof(PriorityPartyBehaviourData).IsAssignableFrom(configurationType))
                {
                    throw new ArgumentException($"{provider.GetType().Name} registered unsupported configuration type " +
                                                $"{configurationType.FullName}.", nameof(providers));
                }

                if (providerRegistry.ContainsKey(configurationType))
                {
                    throw new ArgumentException($"A provider is already registered for {configurationType.FullName}.",
                        nameof(providers));
                }

                providerRegistry.Add(configurationType, provider);
                if (distinctProviders.Add(provider))
                {
                    syncActions.Add(provider.SyncData);
                }
            }

            if (distinctProviders.Add(fallback))
            {
                syncActions.Add(fallback.SyncData);
            }

            _providers = providerRegistry;
            _syncActions = syncActions;

            foreach (SpawnDto spawn in spawnCatalog ?? throw new ArgumentNullException(nameof(spawnCatalog)))
            {
                foreach (PriorityPartyBehaviourData configuration in spawn.PriorityPartyBehaviours ??
                         Array.Empty<PriorityPartyBehaviourData>())
                {
                    if (!providerRegistry.ContainsKey(configuration.GetType()))
                    {
                        throw CreateMissingProviderException(configuration.GetType());
                    }
                }
            }
        }

        public PartyBehaviourResult Resolve(MobileParty party, CsPartyData partyData)
        {
            SpawnDto? spawn = partyData.OwningSpawnTemplateId is null
                ? null
                : _findSpawn(partyData.OwningSpawnTemplateId);
            if (spawn?.PriorityPartyBehaviours is not null)
            {
                foreach (PriorityPartyBehaviourData configuration in spawn.PriorityPartyBehaviours)
                {
                    if (!_providers.TryGetValue(configuration.GetType(), out IPriorityPartyBehaviour? provider))
                    {
                        throw CreateMissingProviderException(configuration.GetType());
                    }

                    PartyBehaviourResult result = provider.GetPriorityBehaviour(party, partyData, configuration);
                    if (result.IsApplicable)
                    {
                        return result;
                    }
                }
            }

            return _fallback.GetPriorityBehaviour(party, partyData);
        }

        public void SyncData(IDataStore dataStore)
        {
            foreach (Action<IDataStore> syncAction in _syncActions)
            {
                syncAction(dataStore);
            }
        }

        private static InvalidOperationException CreateMissingProviderException(Type configurationType)
        {
            return new InvalidOperationException($"No priority party behaviour provider is registered for " +
                                                 $"{configurationType.FullName}.");
        }

        private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
        {
            public static ReferenceEqualityComparer Instance { get; } = new();

            public new bool Equals(object? x, object? y)
            {
                return ReferenceEquals(x, y);
            }

            public int GetHashCode(object obj)
            {
                return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
            }
        }
    }
}
