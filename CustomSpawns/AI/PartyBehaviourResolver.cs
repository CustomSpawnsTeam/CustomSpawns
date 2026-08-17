using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using CustomSpawns.Data.Dao;
using CustomSpawns.Data.Dto;
using CustomSpawns.Spawn;
using CustomSpawns.Utils;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace CustomSpawns.AI
{
    public sealed class PartyBehaviourResolver
    {
        private readonly ISpawnDao _spawnDao;
        private readonly IPartyBehaviourDao _partyBehaviourDao;
        private readonly MessageBoxService _messageBoxService;
        private readonly IReadOnlyDictionary<Type, IPriorityPartyBehaviour> _providers;
        private readonly IFallbackPartyBehaviour _fallback;
        private readonly IReadOnlyList<Action<IDataStore>> _syncActions;
        private readonly ISet<string> _reportedUnknownTemplateIds = new HashSet<string>();

        public PartyBehaviourResolver(ISpawnDao spawnDao, IPartyBehaviourDao partyBehaviourDao,
            IEnumerable<IPriorityPartyBehaviour> providers, IFallbackPartyBehaviour fallback,
            MessageBoxService messageBoxService)
        {
            _spawnDao = spawnDao ?? throw new ArgumentNullException(nameof(spawnDao));
            _partyBehaviourDao = partyBehaviourDao ?? throw new ArgumentNullException(nameof(partyBehaviourDao));
            _messageBoxService = messageBoxService ?? throw new ArgumentNullException(nameof(messageBoxService));
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

            foreach (PartyBehaviourDto partyBehaviour in partyBehaviourDao.FindAll())
            {
                foreach (PriorityPartyBehaviourData configuration in partyBehaviour.PriorityPartyBehaviours ??
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
                : _spawnDao.FindByPartyTemplateId(partyData.OwningSpawnTemplateId);
            if (spawn is not null)
            {
                foreach (PriorityPartyBehaviourData configuration in FindBehaviours(spawn))
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

        private IReadOnlyList<PriorityPartyBehaviourData> FindBehaviours(SpawnDto spawn)
        {
            string? templateId = spawn.PriorityPartyBehaviourTemplateId;
            if (templateId is null)
            {
                return spawn.PatrolAroundClosestLestInterruptedAndSwitch is null
                    ? Array.Empty<PriorityPartyBehaviourData>()
                    : new PriorityPartyBehaviourData[] { spawn.PatrolAroundClosestLestInterruptedAndSwitch };
            }

            PartyBehaviourDto? partyBehaviour = _partyBehaviourDao.FindById(templateId);
            if (partyBehaviour is null)
            {
                if (_reportedUnknownTemplateIds.Add(templateId))
                {
                    _messageBoxService.ShowMessage("Custom Spawns cannot find the priority party behaviour template '" +
                                                   templateId + "' referenced by a spawn.");
                }

                return Array.Empty<PriorityPartyBehaviourData>();
            }

            return partyBehaviour.PriorityPartyBehaviours ?? Array.Empty<PriorityPartyBehaviourData>();
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
                return RuntimeHelpers.GetHashCode(obj);
            }
        }
    }
}
