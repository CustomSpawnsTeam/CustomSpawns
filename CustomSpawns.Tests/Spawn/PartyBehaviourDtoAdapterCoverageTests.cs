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
            var adapter = new PartyBehaviourDtoAdapter(PartyBehaviourRegistrations.ConfigAdapters);

            Assert.That(configurationTypes, Is.Not.Empty);
            Assert.That(configurationTypes.Where(type => !adapter.HasAdapter(type)),
                Is.Empty, "Every concrete PriorityPartyBehaviourConfig must be registered in " +
                          "PartyBehaviourRegistrations, which is keyed on the exact configuration type.");
        }
    }
}
