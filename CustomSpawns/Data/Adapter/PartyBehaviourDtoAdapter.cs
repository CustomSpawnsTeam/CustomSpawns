using System;
using System.Collections.Generic;
using System.Linq;
using CustomSpawns.Data.Dto;
using CustomSpawns.Data.Model;

namespace CustomSpawns.Data.Adapter
{
    public class PartyBehaviourDtoAdapter
    {
        private static readonly IReadOnlyDictionary<Type, IPriorityPartyBehaviourConfigAdapter> Adapters =
            new IPriorityPartyBehaviourConfigAdapter[]
            {
                new BesiegeClosestSettlementConfigAdapter(),
                new RaidClosestVillageConfigAdapter(),
                new PatrolAroundClosestLestInterruptedAndSwitchConfigAdapter()
            }.ToDictionary(adapter => adapter.ConfigurationType);

        public PartyBehaviourDto Adapt(PriorityPartyBehaviourTemplate template)
        {
            return new PartyBehaviourDto
            {
                Id = template.Id!,
                PriorityPartyBehaviours = AdaptPriorityPartyBehaviours(template.PriorityPartyBehaviours)
            };
        }

        internal static IReadOnlyList<PriorityPartyBehaviourData> AdaptPriorityPartyBehaviours(
            List<PriorityPartyBehaviourConfig>? configurations)
        {
            configurations ??= new List<PriorityPartyBehaviourConfig>();
            if (configurations.GroupBy(configuration => configuration.GetType()).Any(group => group.Count() > 1))
            {
                throw new ArgumentException("PriorityPartyBehaviours cannot contain duplicate behaviour types.");
            }

            return configurations.Select(AdaptPriorityPartyBehaviour).ToList();
        }

        private static PriorityPartyBehaviourData AdaptPriorityPartyBehaviour(
            PriorityPartyBehaviourConfig configuration)
        {
            if (!Adapters.TryGetValue(configuration.GetType(), out IPriorityPartyBehaviourConfigAdapter adapter))
            {
                throw new ArgumentException("Unsupported priority party behaviour configuration.");
            }

            return adapter.Adapt(configuration);
        }
    }
}
