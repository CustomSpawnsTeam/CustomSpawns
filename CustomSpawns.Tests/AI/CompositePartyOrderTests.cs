using CustomSpawns.AI;
using NUnit.Framework;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace CustomSpawns.Tests.AI
{
    public class CompositePartyOrderTests
    {
        [Test]
        public void Find_ReturnsTheFirstApplicableOrder()
        {
            StubPartyOrder first = new(null);
            StubPartyOrder second = new(default(AIBehaviorData));
            StubPartyOrder third = new(default(AIBehaviorData));
            var orders = new CompositePartyOrder(new IPartyOrder[] { first, second, third });

            AIBehaviorData? result = orders.Find(null!);

            Assert.That(result.HasValue, Is.True);
            Assert.That(first.CallCount, Is.EqualTo(1));
            Assert.That(second.CallCount, Is.EqualTo(1));
            Assert.That(third.CallCount, Is.Zero);
        }

        [Test]
        public void Find_ReturnsNothingWhenNoOrderApplies()
        {
            var orders = new CompositePartyOrder(new IPartyOrder[]
            {
                new StubPartyOrder(null), new StubPartyOrder(null)
            });

            Assert.That(orders.Find(null!), Is.Null);
        }

        private sealed class StubPartyOrder : IPartyOrder
        {
            private readonly AIBehaviorData? _behavior;

            public StubPartyOrder(AIBehaviorData? behavior)
            {
                _behavior = behavior;
            }

            public int CallCount { get; private set; }

            public AIBehaviorData? Find(MobileParty party)
            {
                CallCount++;
                return _behavior;
            }
        }
    }
}
