using System;
using CustomSpawns.Data.Dto;
using CustomSpawns.Spawn;
using TaleWorlds.CampaignSystem.Party;

namespace CustomSpawns.AI
{
    public sealed class PartyBehaviourResolver
    {
        private readonly SpawnPartyBehaviourLookup _lookup;
        private readonly PriorityPartyBehaviourRegistry _registry;
        private readonly IFallbackPartyBehaviour _fallback;
        private readonly ISupportingPartyBehaviour _supportingPartyBehaviour;

        public PartyBehaviourResolver(SpawnPartyBehaviourLookup lookup, PriorityPartyBehaviourRegistry registry,
            IFallbackPartyBehaviour fallback, ISupportingPartyBehaviour supportingPartyBehaviour)
        {
            _lookup = lookup ?? throw new ArgumentNullException(nameof(lookup));
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _fallback = fallback ?? throw new ArgumentNullException(nameof(fallback));
            _supportingPartyBehaviour = supportingPartyBehaviour ??
                                        throw new ArgumentNullException(nameof(supportingPartyBehaviour));

            foreach (Type configurationType in lookup.FindAllConfiguredBehaviourTypes())
            {
                if (!registry.TryGet(configurationType, out _))
                {
                    throw CreateMissingProviderException(configurationType);
                }
            }
        }

        public PartyBehaviourResult Resolve(MobileParty party, CsPartyData partyData)
        {
            if (!partyData.IsPrimarySpawn)
            {
                PartyBehaviourResult supportingResult = _supportingPartyBehaviour.GetPriorityBehaviour(party, partyData);
                return supportingResult.IsApplicable ? supportingResult : _fallback.GetPriorityBehaviour(party, partyData);
            }

            foreach (PriorityPartyBehaviourData configuration in _lookup.FindBehaviours(partyData.OwningSpawnTemplateId))
            {
                if (!_registry.TryGet(configuration.GetType(), out IPriorityPartyBehaviour provider))
                {
                    throw CreateMissingProviderException(configuration.GetType());
                }

                PartyBehaviourResult result = provider.GetPriorityBehaviour(party, partyData, configuration);
                if (result.IsApplicable)
                {
                    return result;
                }
            }

            return _fallback.GetPriorityBehaviour(party, partyData);
        }

        private static InvalidOperationException CreateMissingProviderException(Type configurationType)
        {
            return new InvalidOperationException($"No priority party behaviour provider is registered for " +
                                                 $"{configurationType.FullName}.");
        }
    }
}
