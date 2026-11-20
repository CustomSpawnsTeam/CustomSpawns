using CustomSpawns.Invasions.Conquest;
using NUnit.Framework;

namespace CustomSpawns.Tests.Invasions.Conquest
{
    public class InvasionConquestFrontsTests
    {
        private readonly InvasionConquestRules _rules = new(0.05f, 30f);
        private InvasionConquests _invasionConquests = new();
        private InvasionConquestFronts _invasionConquestFronts = new();

        [SetUp]
        public void SetUp()
        {
            _invasionConquests = new InvasionConquests();
            _invasionConquestFronts = new InvasionConquestFronts();
        }

        [Test]
        public void AFootholdIsLessThanHalfTheThreatOfARealm()
        {
            float foothold = _invasionConquestFronts.FindThreat(_rules, 2, 200000f);
            float realm = _invasionConquestFronts.FindThreat(_rules, 6, 600000f);

            Assert.That(realm, Is.EqualTo(9 * foothold).Within(0.01f));
        }

        [TestCase(0, 100000f)]
        [TestCase(3, 0f)]
        public void AnInvaderWithNothingIsNoThreat(int fiefsHeld, float invaderWorth)
        {
            Assert.That(_invasionConquestFronts.FindThreat(_rules, fiefsHeld, invaderWorth), Is.Zero);
        }

        [Test]
        public void AnUnsetThreatLeavesTheNeighboursToVanilla()
        {
            Assert.That(_invasionConquestFronts.FindThreat(new InvasionConquestRules(0f, 30f), 8, 800000f), Is.Zero);
        }

        [Test]
        public void AKingdomAtPeaceElsewhereHeedsTheWholeThreat()
        {
            Assert.That(_invasionConquestFronts.FindStretchedThreat(300000f, 5000f, 0f), Is.EqualTo(300000f));
        }

        [Test]
        public void AKingdomFightingAsStrongAnEnemyHeedsHalfTheThreat()
        {
            Assert.That(_invasionConquestFronts.FindStretchedThreat(300000f, 5000f, 5000f),
                Is.EqualTo(150000f).Within(0.01f));
        }

        [Test]
        public void AKingdomWithNoStrengthHeedsNoThreat()
        {
            Assert.That(_invasionConquestFronts.FindStretchedThreat(300000f, 0f, 5000f), Is.Zero);
        }

        [TestCase(34, true)]
        [TestCase(33, false)]
        public void ALineHoldingAQuarterOfTheLandFormsACoalition(int lineFiefs, bool formed)
        {
            Assert.That(_invasionConquestFronts.IsCoalitionFormed(lineFiefs, 133), Is.EqualTo(formed));
        }

        [TestCase(6, false, true)]
        [TestCase(7, false, false)]
        [TestCase(0, true, false)]
        public void ALineHoldingNoCapitalAndLessThanATwentiethIsSpent(int lineFiefs, bool holdsACapital, bool spent)
        {
            Assert.That(_invasionConquestFronts.IsCoalitionSpent(lineFiefs, 133, holdsACapital), Is.EqualTo(spent));
        }

        [Test]
        public void ALineHoldingNoFiefInAnEmptyLandFormsNoCoalition()
        {
            Assert.That(_invasionConquestFronts.IsCoalitionFormed(0, 0), Is.False);
        }

        [Test]
        public void TheNearestFiefOfAKingdomAtPeaceOpensAFront()
        {
            InvasionConquest conquest = _invasionConquests.Begin("humar", "vlandia", 10)!;

            Assert.That(_invasionConquestFronts.FindFrontToOpen(conquest, _rules, new[]
            {
                new InvasionPrey("vlandia", 40f, true), new InvasionPrey("empire_w", 12f, false)
            }, 40), Is.EqualTo("empire_w"));
        }

        [Test]
        public void TheNearestFiefOfAnEnemyIsTakenWithoutAnyNewWar()
        {
            InvasionConquest conquest = _invasionConquests.Begin("humar", "vlandia", 10)!;

            Assert.That(_invasionConquestFronts.FindFrontToOpen(conquest, _rules, new[]
            {
                new InvasionPrey("vlandia", 8f, true), new InvasionPrey("empire_w", 12f, false)
            }, 40), Is.Null);
        }

        [Test]
        public void ANewFrontWaitsForTheLastOneToSettle()
        {
            InvasionConquest conquest = _invasionConquests.Begin("humar", "vlandia", 10)!;
            conquest.OpenFront(30);

            Assert.That(_invasionConquestFronts.FindFrontToOpen(conquest, _rules,
                new[] { new InvasionPrey("empire_w", 12f, false) }, 59), Is.Null);
            Assert.That(_invasionConquestFronts.FindFrontToOpen(conquest, _rules,
                new[] { new InvasionPrey("empire_w", 12f, false) }, 60), Is.EqualTo("empire_w"));
        }

        [Test]
        public void AnUnsetIntervalNeverOpensAFront()
        {
            InvasionConquest conquest = _invasionConquests.Begin("humar", "vlandia", 10)!;

            Assert.That(_invasionConquestFronts.FindFrontToOpen(conquest, new InvasionConquestRules(0.05f, 0f),
                new[] { new InvasionPrey("empire_w", 12f, false) }, 500), Is.Null);
        }

        [Test]
        public void NothingLeftToTakeOpensNoFront()
        {
            InvasionConquest conquest = _invasionConquests.Begin("humar", "vlandia", 10)!;

            Assert.That(_invasionConquestFronts.FindFrontToOpen(conquest, _rules, new InvasionPrey[0], 500), Is.Null);
        }
    }
}
