using CustomSpawns.Invasions.Coalitions;
using NUnit.Framework;

namespace CustomSpawns.Tests.Invasions.Coalitions
{
    public class InvasionCoalitionWarScoresTests
    {
        private InvasionCoalitionWarScores _invasionCoalitionWarScores = new();

        [SetUp]
        public void SetUp()
        {
            _invasionCoalitionWarScores = new InvasionCoalitionWarScores();
        }

        [Test]
        public void AWinnableSiegeOfTheLineIsWantedAboveEverySiegeOfAnotherKingdom()
        {
            Assert.That(_invasionCoalitionWarScores.FindLineSiegeScore(0.1f, 2f), Is.EqualTo(2.2f).Within(0.001f));
        }

        [Test]
        public void AmongTheLinesFiefsVanillasWishIsDoubled()
        {
            Assert.That(_invasionCoalitionWarScores.FindLineSiegeScore(3f, 2f), Is.EqualTo(6f).Within(0.001f));
        }

        [Test]
        public void ASiegeOfTheLineThatCannotBeWonStaysUnwanted()
        {
            Assert.That(_invasionCoalitionWarScores.FindLineSiegeScore(0f, 2f), Is.EqualTo(0f));
        }

        [Test]
        public void JoiningACoalitionCampIsWantedAboveEverySiegeTheLordCouldOpen()
        {
            Assert.That(_invasionCoalitionWarScores.FindCampScore(1f, 4f), Is.EqualTo(4.4f).Within(0.001f));
        }

        [Test]
        public void ACoalitionCampTheLordCannotWinWithStaysUnwanted()
        {
            Assert.That(_invasionCoalitionWarScores.FindCampScore(0f, 4f), Is.EqualTo(0f));
        }

        [Test]
        public void ACampWithNoOtherSiegeWeighedIsWantedAsTheFarthestSiegeVanillaWeighs()
        {
            Assert.That(_invasionCoalitionWarScores.FindCampScore(5f, 0f), Is.EqualTo(1f).Within(0.001f));
        }

        [Test]
        public void ACutOffCapitalIsWantedAboveEveryOtherSiege()
        {
            Assert.That(_invasionCoalitionWarScores.FindCapitalScore(1f, true, 4.4f),
                Is.EqualTo(6.6f).Within(0.001f));
        }

        [Test]
        public void ACutOffCapitalVanillaWeighsKeepsDoubleItsWishWhenThatIsHigher()
        {
            Assert.That(_invasionCoalitionWarScores.FindCapitalScore(5f, true, 1f), Is.EqualTo(10f).Within(0.001f));
        }

        [Test]
        public void ACutOffCapitalVanillaDoesNotWeighIsWeighedAsTheFarthestSiege()
        {
            Assert.That(_invasionCoalitionWarScores.FindCapitalScore(5f, false, 0f), Is.EqualTo(1f).Within(0.001f));
        }

        [Test]
        public void ACutOffCapitalThatCannotBeWonStaysUnwanted()
        {
            Assert.That(_invasionCoalitionWarScores.FindCapitalScore(0f, false, 4f), Is.EqualTo(0f));
        }

        [Test]
        public void AFieldPartyTheHuntersOutmatchIsWantedAboveTheRestWhenAtHand()
        {
            Assert.That(_invasionCoalitionWarScores.FindHuntScore(0.2f, 2f, 1f, 1200f, 1000f),
                Is.EqualTo(3f).Within(0.001f));
        }

        [Test]
        public void AFartherFieldPartyIsWantedLess()
        {
            Assert.That(_invasionCoalitionWarScores.FindHuntScore(0.2f, 2f, 0.5f, 1200f, 1000f),
                Is.EqualTo(1.5f).Within(0.001f));
        }

        [Test]
        public void AnOutmatchedFieldPartyKeepsDoubleVanillasWishWhenThatIsHigher()
        {
            Assert.That(_invasionCoalitionWarScores.FindHuntScore(2f, 1f, 0.5f, 1200f, 1000f),
                Is.EqualTo(4f).Within(0.001f));
        }

        [Test]
        public void AFieldPartyTheHuntersDoNotOutmatchKeepsVanillasWish()
        {
            Assert.That(_invasionCoalitionWarScores.FindHuntScore(0.2f, 2f, 1f, 1100f, 1000f),
                Is.EqualTo(0.2f).Within(0.001f));
        }

        [Test]
        public void AFieldPartyOutOfSightKeepsVanillasWish()
        {
            Assert.That(_invasionCoalitionWarScores.FindHuntScore(0f, 2f, 0f, 5000f, 1000f), Is.EqualTo(0f));
        }

        [Test]
        public void ACapitalIsBesiegedWithTheStrengthOfTheWholeCoalitionInReach()
        {
            Assert.That(_invasionCoalitionWarScores.FindCoalitionSiegeStrength(500f, new[] { 1200f, 800f }),
                Is.EqualTo(2500f).Within(0.001f));
        }

        [Test]
        public void ALordWithNoForceOfTheCoalitionInReachBesiegesACapitalAlone()
        {
            Assert.That(_invasionCoalitionWarScores.FindCoalitionSiegeStrength(500f, new float[0]),
                Is.EqualTo(500f).Within(0.001f));
        }

        [TestCase(60f, 30f, true)]
        [TestCase(61f, 30f, false)]
        public void ACampIsInReachWithinTwoTownDistances(float distance, float townDistance, bool expected)
        {
            Assert.That(_invasionCoalitionWarScores.IsCampInReach(distance, townDistance), Is.EqualTo(expected));
        }

        [TestCase(120f, 30f, true)]
        [TestCase(121f, 30f, false)]
        public void ACapitalIsInReachWithinFourTownDistances(float distance, float townDistance, bool expected)
        {
            Assert.That(_invasionCoalitionWarScores.IsCapitalInReach(distance, townDistance), Is.EqualTo(expected));
        }
    }
}
