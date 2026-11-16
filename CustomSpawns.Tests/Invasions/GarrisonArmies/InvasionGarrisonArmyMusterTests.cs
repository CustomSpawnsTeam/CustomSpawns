using CustomSpawns.Invasions.GarrisonArmies;
using NUnit.Framework;

namespace CustomSpawns.Tests.Invasions.GarrisonArmies
{
    public class InvasionGarrisonArmyMusterTests
    {
        private static readonly InvasionGarrisonArmyRules Rules = new(90f, 0.5f, 1000);
        private InvasionGarrisonArmyMuster _invasionGarrisonArmyMuster = new(new InvasionGarrisonArmySizing());

        [SetUp]
        public void SetUp()
        {
            _invasionGarrisonArmyMuster = new InvasionGarrisonArmyMuster(new InvasionGarrisonArmySizing());
        }

        [Test]
        public void TheFirstArmyIsDueAtOnce()
        {
            Assert.That(_invasionGarrisonArmyMuster.IsDue(Rules, null, false), Is.True);
        }

        [TestCase(89.9, false)]
        [TestCase(90.0, true)]
        [TestCase(120.0, true)]
        public void ALaterArmyWaitsForTheInterval(double daysSinceLastRaise, bool expected)
        {
            Assert.That(_invasionGarrisonArmyMuster.IsDue(Rules, daysSinceLastRaise, false), Is.EqualTo(expected));
        }

        [Test]
        public void NoSecondArmyWhileOneIsOut()
        {
            Assert.That(_invasionGarrisonArmyMuster.IsDue(Rules, 500.0, true), Is.False);
        }

        [Test]
        public void ARaisedArmyTakesTheMenTheSizingAllows()
        {
            InvasionGarrisonArmyMusterDecision decision =
                _invasionGarrisonArmyMuster.Decide(Rules, new InvasionGarrisonArmyFacts(false, 3000, true));

            Assert.Multiple(() =>
            {
                Assert.That(decision.Men, Is.EqualTo(1500));
                Assert.That(decision.SkipReason, Is.Null);
            });
        }

        [Test]
        public void ABesiegedCapitalKeepsItsGarrison()
        {
            InvasionGarrisonArmyMusterDecision decision =
                _invasionGarrisonArmyMuster.Decide(Rules, new InvasionGarrisonArmyFacts(true, 10000, true));

            Assert.Multiple(() =>
            {
                Assert.That(decision.Men, Is.EqualTo(0));
                Assert.That(decision.SkipReason, Is.EqualTo(InvasionGarrisonArmyMuster.CapitalBesieged));
            });
        }

        [Test]
        public void AGarrisonAtTheMinimumIsSkippedWithItsReason()
        {
            InvasionGarrisonArmyMusterDecision decision =
                _invasionGarrisonArmyMuster.Decide(Rules, new InvasionGarrisonArmyFacts(false, 1000, true));

            Assert.That(decision.SkipReason, Is.EqualTo(InvasionGarrisonArmyMuster.GarrisonAtMinimum));
        }

        [Test]
        public void WithoutATargetNoArmyIsRaised()
        {
            InvasionGarrisonArmyMusterDecision decision =
                _invasionGarrisonArmyMuster.Decide(Rules, new InvasionGarrisonArmyFacts(false, 3000, false));

            Assert.That(decision.SkipReason, Is.EqualTo(InvasionGarrisonArmyMuster.NoTarget));
        }
    }
}
