using System.Collections.Generic;
using CustomSpawns.Invasions;
using CustomSpawns.Invasions.Adapters;
using NUnit.Framework;

namespace CustomSpawns.Tests.Invasions
{
    public class InvasionConquestSaveStoreTests
    {
        [Test]
        public void TheWarsARealmLeadsSurviveASave()
        {
            var invasionConquests = new InvasionConquests();
            var invasionConquestSaveStore = new InvasionConquestSaveStore(invasionConquests);
            invasionConquests.Begin("humar", "vlandia", 10);
            invasionConquests.BeginWar("humar", "khuzait");
            invasionConquests.BeginWar("humar", "aserai");

            Dictionary<string, string> saved = invasionConquestSaveStore.Export();
            invasionConquestSaveStore.Restore(saved);

            Assert.That(invasionConquests.Find("humar")!.WarsLed, Is.EquivalentTo(new[] { "khuzait", "aserai" }));
        }

        [Test]
        public void ASaveOlderThanLedWarsLeadsNone()
        {
            var invasionConquests = new InvasionConquests();
            var invasionConquestSaveStore = new InvasionConquestSaveStore(invasionConquests);

            invasionConquestSaveStore.Restore(new Dictionary<string, string> { ["humar"] = "vlandia|10|10||" });

            Assert.That(invasionConquests.Find("humar")!.WarsLed, Is.Empty);
        }
    }
}
