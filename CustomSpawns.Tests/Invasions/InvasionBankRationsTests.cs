using CustomSpawns.Invasions;
using NUnit.Framework;

namespace CustomSpawns.Tests.Invasions
{
    public class InvasionBankRationsTests
    {
        private const int MenToEatOneFood = 20;
        private readonly InvasionBankRations _invasionBankRations = new();

        [Test]
        public void ABankedReserveEatsNothingFromItsTown()
        {
            // 3,563 men against a limit of 518: vanilla fed all of them, 178 food a day.
            Assert.That(_invasionBankRations.FoodSpared(true, 3563, 518, MenToEatOneFood),
                Is.EqualTo(152.25f).Within(0.001f));
        }

        [Test]
        public void AGarrisonWithinItsLimitEatsAsAnyOther()
        {
            Assert.That(_invasionBankRations.FoodSpared(true, 400, 518, MenToEatOneFood), Is.Zero);
        }

        [Test]
        public void AKingdomsCrowdedGarrisonEatsAsInVanilla()
        {
            Assert.That(_invasionBankRations.FoodSpared(false, 3563, 518, MenToEatOneFood), Is.Zero);
        }
    }
}
