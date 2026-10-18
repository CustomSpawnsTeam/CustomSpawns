using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace CustomSpawns.AI
{
    public sealed class CompositePartyOrder : IPartyOrder
    {
        private readonly IReadOnlyList<IPartyOrder> _orders;

        public CompositePartyOrder(IEnumerable<IPartyOrder> orders)
        {
            _orders = new List<IPartyOrder>(orders ?? throw new ArgumentNullException(nameof(orders)));
        }

        public AIBehaviorData? Find(MobileParty party)
        {
            foreach (IPartyOrder order in _orders)
            {
                AIBehaviorData? behavior = order.Find(party);
                if (behavior.HasValue)
                {
                    return behavior;
                }
            }

            return null;
        }
    }
}
