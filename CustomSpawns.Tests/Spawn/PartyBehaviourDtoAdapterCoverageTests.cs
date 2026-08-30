using System;
using System.Linq;
using CustomSpawns.Data.Adapter;
using CustomSpawns.Data.Model;
using NUnit.Framework;

namespace CustomSpawns.Tests.Spawn
{
    public class PartyBehaviourDtoAdapterCoverageTests
    {
        [Test]
        public void EveryConcretePriorityPartyBehaviourConfigHasARegisteredAdapter()
        {
            Type[] configurationTypes = typeof(PriorityPartyBehaviourConfig).Assembly.GetTypes()
                .Where(type => !type.IsAbstract && typeof(PriorityPartyBehaviourConfig).IsAssignableFrom(type))
                .ToArray();

            Assert.That(configurationTypes, Is.Not.Empty);
            Assert.That(configurationTypes.Where(type => !PartyBehaviourDtoAdapter.HasAdapter(type)),
                Is.Empty, "Every concrete PriorityPartyBehaviourConfig must register an adapter in " +
                          "PartyBehaviourDtoAdapter.Adapters, which is keyed on the exact configuration type.");
        }
    }
}
