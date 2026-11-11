using CustomSpawns.Invasions;
using NUnit.Framework;

namespace CustomSpawns.Tests.Invasions
{
    public class InvasionGarrisonArmySizingTests
    {
        private static readonly InvasionGarrisonArmyRules Rules = new(90f, 0.5f, 1000);
        private InvasionGarrisonArmySizing _invasionGarrisonArmySizing = new();

        [SetUp]
        public void SetUp()
        {
            _invasionGarrisonArmySizing = new InvasionGarrisonArmySizing();
        }

        [TestCase(3000, 1500)]
        [TestCase(10000, 5000)]
        [TestCase(1600, 600)]
        [TestCase(1500, 500)]
        public void AnArmyTakesItsShareButNeverTheMinimumGarrison(int garrisonMen, int expected)
        {
            Assert.That(_invasionGarrisonArmySizing.FindMen(Rules, garrisonMen), Is.EqualTo(expected));
        }

        [TestCase(1000)]
        [TestCase(600)]
        [TestCase(0)]
        public void AGarrisonAtOrBelowTheMinimumSendsNoOne(int garrisonMen)
        {
            Assert.That(_invasionGarrisonArmySizing.FindMen(Rules, garrisonMen), Is.EqualTo(0));
        }

        [Test]
        public void AFullShareTakesEveryManAboveTheMinimum()
        {
            var rules = new InvasionGarrisonArmyRules(90f, 1f, 2000);

            Assert.That(_invasionGarrisonArmySizing.FindMen(rules, 3000), Is.EqualTo(1000));
        }

        [Test]
        public void APartyCarriesAMonthOfFood()
        {
            Assert.That(_invasionGarrisonArmySizing.FindFood(500, 20, 7f), Is.EqualTo(750));
        }

        [Test]
        public void ALongerVanillaMinimumIsCarriedInstead()
        {
            Assert.That(_invasionGarrisonArmySizing.FindFood(500, 20, 40f), Is.EqualTo(1000));
        }

        [Test]
        public void FoodIsRoundedUp()
        {
            Assert.That(_invasionGarrisonArmySizing.FindFood(1, 20, 7f), Is.EqualTo(2));
        }

        [Test]
        public void NoMenNeedNoFood()
        {
            Assert.That(_invasionGarrisonArmySizing.FindFood(0, 20, 7f), Is.EqualTo(0));
        }
    }
}
