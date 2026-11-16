using System.Collections.Generic;
using CustomSpawns.Invasions.Coalitions;
using NUnit.Framework;

namespace CustomSpawns.Tests.Invasions.Coalitions
{
    public class InvasionStandingCoalitionsTests
    {
        [Test]
        public void NoCoalitionStandsBeforeTheLineHoldsTheShare()
        {
            InvasionStandingCoalitions coalitions = new();

            Assert.That(coalitions.StandsAgainst("humar", false), Is.False);
        }

        [Test]
        public void ACoalitionStandsOnceTheLineHoldsTheShareAndAfterItLosesIt()
        {
            InvasionStandingCoalitions coalitions = new();

            coalitions.StandsAgainst("humar", true);

            Assert.That(coalitions.StandsAgainst("humar", false), Is.True);
        }

        [Test]
        public void ACoalitionBreaksUpOnceItsLineIsDestroyed()
        {
            InvasionStandingCoalitions coalitions = new();
            coalitions.StandsAgainst("humar", true);
            coalitions.StandsAgainst("sea_raiders", true);

            coalitions.DisbandAllBut(new[] { "sea_raiders" });

            Assert.That(coalitions.StandsAgainst("humar", false), Is.False);
            Assert.That(coalitions.StandsAgainst("sea_raiders", false), Is.True);
        }

        [Test]
        public void StandingCoalitionsAreSaved()
        {
            InvasionStandingCoalitions coalitions = new();
            coalitions.StandsAgainst("humar", true);
            InvasionStandingCoalitions restored = new();

            restored.Restore(coalitions.Export());

            Assert.That(restored.StandsAgainst("humar", false), Is.True);
        }

        [Test]
        public void AMissingSaveHasNoCoalition()
        {
            InvasionStandingCoalitions coalitions = new();

            coalitions.Restore(null);

            Assert.That(coalitions.Export(), Is.EqualTo(new List<string>()));
        }
    }
}
