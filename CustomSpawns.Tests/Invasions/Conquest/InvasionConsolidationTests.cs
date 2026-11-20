using System.Linq;
using CustomSpawns.Invasions.Conquest;
using NUnit.Framework;

namespace CustomSpawns.Tests.Invasions.Conquest
{
    public class InvasionConsolidationTests
    {
        private readonly InvasionConsolidation _invasionConsolidation = new();

        [Test]
        public void AnInvasionWhoseFiefsCanHoldMayExpand()
        {
            InvasionFiefGarrison[] fiefs =
            {
                new("capital", 100, true, false),
                new("castle", 250, false, false)
            };

            Assert.That(_invasionConsolidation.AllowsExpansion(fiefs, 250), Is.True);
        }

        [Test]
        public void AThinlyHeldFiefHoldsExpansionBack()
        {
            InvasionFiefGarrison[] fiefs =
            {
                new("capital", 900, true, false),
                new("castle", 249, false, false)
            };

            Assert.That(_invasionConsolidation.AllowsExpansion(fiefs, 250), Is.False);
        }

        [Test]
        public void AnInvasionThatCannotExpandHoldsItsSiegesOfOtherKingdoms()
        {
            Assert.That(_invasionConsolidation.HoldsSiege(false, false, false), Is.True);
        }

        [Test]
        public void TheKingdomBeingConqueredIsBesiegedEvenWhileTheInvasionCannotExpand()
        {
            Assert.That(_invasionConsolidation.HoldsSiege(false, false, true), Is.False);
        }

        [Test]
        public void TheCapitalIsRetakenEvenWhileTheInvasionCannotExpand()
        {
            Assert.That(_invasionConsolidation.HoldsSiege(false, true, false), Is.False);
        }

        [Test]
        public void AnInvasionThatCanExpandHoldsNoSiege()
        {
            Assert.That(_invasionConsolidation.HoldsSiege(true, false, false), Is.False);
        }

        [Test]
        public void WithoutAMinimumExpansionIsNeverHeldBack()
        {
            Assert.That(_invasionConsolidation.AllowsExpansion(new[] { new InvasionFiefGarrison("castle", 0, false, false) },
                0), Is.True);
        }

        [Test]
        public void TheThinnestFiefIsToppedUpFirstFromAboveTheCapitalsFloor()
        {
            InvasionFiefGarrison[] fiefs =
            {
                new("capital", 700, true, false),
                new("town", 200, false, false),
                new("castle", 50, false, false)
            };

            var transfers = _invasionConsolidation.PlanTopUps(700, 400, fiefs, 250);

            Assert.That(transfers.Select(transfer => (transfer.SettlementId, transfer.Men)),
                Is.EqualTo(new[] { ("castle", 200), ("town", 50) }));
        }

        [Test]
        public void TheCapitalIsNeverDrawnBelowItsFloor()
        {
            InvasionFiefGarrison[] fiefs =
            {
                new("castle", 50, false, false),
                new("town", 100, false, false)
            };

            var transfers = _invasionConsolidation.PlanTopUps(500, 400, fiefs, 250);

            Assert.Multiple(() =>
            {
                Assert.That(transfers.Sum(transfer => transfer.Men), Is.EqualTo(100));
                Assert.That(transfers.Single().SettlementId, Is.EqualTo("castle"));
            });
        }

        [Test]
        public void ABesiegedFiefWaitsForItsSiegeToEnd()
        {
            var transfers = _invasionConsolidation.PlanTopUps(900, 400,
                new[] { new InvasionFiefGarrison("castle", 0, false, true) }, 250);

            Assert.That(transfers, Is.Empty);
        }

        [Test]
        public void ACapitalAtItsFloorSendsNothing()
        {
            var transfers = _invasionConsolidation.PlanTopUps(300, 400,
                new[] { new InvasionFiefGarrison("castle", 0, false, false) }, 250);

            Assert.That(transfers, Is.Empty);
        }
    }
}
