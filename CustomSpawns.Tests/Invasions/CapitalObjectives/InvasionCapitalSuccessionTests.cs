using CustomSpawns.AI.Offensives;
using CustomSpawns.Invasions.CapitalObjectives;
using NUnit.Framework;

namespace CustomSpawns.Tests.Invasions.CapitalObjectives
{
    public class InvasionCapitalSuccessionTests
    {
        private readonly InvasionCapitalSuccession _invasionCapitalSuccession = new();

        [Test]
        public void ACoastalInvasionMovesItsCapitalToTheNearestCoastalTown()
        {
            string? successor = _invasionCapitalSuccession.FindSuccessor(Requirements(true, true),
                new[] { Fief("inland", 1f, false), Fief("far-port", 9f, true), Fief("port", 5f, true) });

            Assert.That(successor, Is.EqualTo("port"));
        }

        [Test]
        public void AnInvasionFreeToLandAnywhereMovesItsCapitalToTheNearestTown()
        {
            string? successor = _invasionCapitalSuccession.FindSuccessor(Requirements(true, false),
                new[] { Fief("port", 5f, true), Fief("inland", 1f, false) });

            Assert.That(successor, Is.EqualTo("inland"));
        }

        [Test]
        public void AFiefOfAKindTheInvasionDoesNotBesiegeNeverBecomesItsCapital()
        {
            var castle = new CapitalSuccessorFacts("castle", SiegeTargetKind.Castle, false, 1f);

            Assert.That(_invasionCapitalSuccession.FindSuccessor(Requirements(true, false), new[] { castle }),
                Is.Null);
        }

        [Test]
        public void NoFitFiefLeavesTheCapitalToBeRetaken()
        {
            Assert.That(_invasionCapitalSuccession.FindSuccessor(Requirements(true, true),
                new[] { Fief("inland", 1f, false) }), Is.Null);
        }

        [Test]
        public void AnInvasionNotConfiguredToMoveItsCapitalNeverDoes()
        {
            Assert.That(_invasionCapitalSuccession.FindSuccessor(Requirements(false, false),
                new[] { Fief("port", 1f, true) }), Is.Null);
        }

        private static InvasionAssaultRequirements Requirements(bool relocates, bool coastalFirst) =>
            new(new[] { SiegeTargetKind.Town }, 1.5f, 1.2f, coastalFirst, InvasionSupplyLineKind.Coastal,
                relocates);

        private static CapitalSuccessorFacts Fief(string settlementId, float distance, bool coastal) =>
            new(settlementId, SiegeTargetKind.Town, coastal, distance);
    }
}
