using System;
using System.Collections.Generic;
using System.Linq;
using CustomSpawns.Data.Dto;
using CustomSpawns.Data.Model;

namespace CustomSpawns.Data.Adapter
{
    public class PartyBehaviourDtoAdapter
    {
        /**
         * Keyed on the exact configuration type and looked up with configuration.GetType(), which only
         * resolves because every concrete PriorityPartyBehaviourConfig is sealed. A configuration whose
         * type is not registered is rejected as unsupported, derived or not.
         */
        private readonly IReadOnlyDictionary<Type, IPriorityPartyBehaviourConfigAdapter> _adapters;

        internal PartyBehaviourDtoAdapter(IEnumerable<IPriorityPartyBehaviourConfigAdapter> adapters)
        {
            _adapters = (adapters ?? throw new ArgumentNullException(nameof(adapters)))
                .ToDictionary(adapter => adapter.ConfigurationType);
        }

        internal bool HasAdapter(Type configurationType)
        {
            return _adapters.ContainsKey(configurationType);
        }

        public PartyBehaviourDto Adapt(PriorityPartyBehaviourTemplate template)
        {
            return new PartyBehaviourDto
            {
                Id = template.Id!,
                PriorityPartyBehaviours = AdaptPriorityPartyBehaviours(template.PriorityPartyBehaviours)
            };
        }

        internal IReadOnlyList<PriorityPartyBehaviourData> AdaptPriorityPartyBehaviours(
            List<PriorityPartyBehaviourConfig>? configurations)
        {
            configurations ??= new List<PriorityPartyBehaviourConfig>();
            if (configurations.GroupBy(configuration => configuration.GetType()).Any(group => group.Count() > 1))
            {
                throw new ArgumentException("PriorityPartyBehaviours cannot contain duplicate behaviour types.");
            }

            return configurations.Select(AdaptPriorityPartyBehaviour).ToList();
        }

        private PriorityPartyBehaviourData AdaptPriorityPartyBehaviour(PriorityPartyBehaviourConfig configuration)
        {
            if (!_adapters.TryGetValue(configuration.GetType(), out IPriorityPartyBehaviourConfigAdapter adapter))
            {
                throw new ArgumentException("Unsupported priority party behaviour configuration.");
            }

            return adapter.Adapt(configuration);
        }
    }
}
