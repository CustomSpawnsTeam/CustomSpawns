using CustomSpawns.Invasions.Conquest;
using NUnit.Framework;

namespace CustomSpawns.Tests.Invasions.Conquest
{
    public class InvasionTargetSiegeScoresTests
    {
        private InvasionTargetSiegeScores _invasionTargetSiegeScores = new();

        [SetUp]
        public void SetUp()
        {
            _invasionTargetSiegeScores = new InvasionTargetSiegeScores();
        }

        [Test]
        public void AWinnableSiegeOfTheConqueredKingdomIsWantedAboveEveryOtherSiege()
        {
            var rules = new InvasionConquestRules(0.05f, 30f, 4f);

            Assert.That(_invasionTargetSiegeScores.FindTargetSiegeScore(rules, 0.1f, 2f), Is.EqualTo(2.2f).Within(0.001f));
        }

        [Test]
        public void AmongTheConqueredKingdomsFiefsThePreferenceMultipliesVanillasWish()
        {
            var rules = new InvasionConquestRules(0.05f, 30f, 4f);

            Assert.That(_invasionTargetSiegeScores.FindTargetSiegeScore(rules, 1f, 2f), Is.EqualTo(4f).Within(0.001f));
        }

        [Test]
        public void ASiegeOfTheConqueredKingdomThatCannotBeWonStaysUnwanted()
        {
            var rules = new InvasionConquestRules(0.05f, 30f, 4f);

            Assert.That(_invasionTargetSiegeScores.FindTargetSiegeScore(rules, 0f, 2f), Is.EqualTo(0f));
        }

        [TestCase(0f)]
        [TestCase(1f)]
        [TestCase(0.5f)]
        public void WithoutAPreferenceAboveOneSiegesAreLeftToVanilla(float preference)
        {
            var rules = new InvasionConquestRules(0.05f, 30f, preference);

            Assert.That(_invasionTargetSiegeScores.FindTargetSiegeScore(rules, 0.3f, 2f), Is.EqualTo(0.3f));
        }

        [Test]
        public void AnUnborderedTargetFiefIsWantedAboveEveryOtherSiegeWhenNoTargetFiefIsWithinReach()
        {
            var rules = new InvasionConquestRules(0.05f, 0f, 4f);

            Assert.Multiple(() =>
            {
                Assert.That(_invasionTargetSiegeScores.FindUnborderedTargetSiegeScore(rules, 1f, 0f, 2f, false),
                    Is.EqualTo(2.2f).Within(0.001f));
                Assert.That(_invasionTargetSiegeScores.FindUnborderedTargetSiegeScore(rules, 10f, 0f, 2f, false),
                    Is.EqualTo(4f).Within(0.001f));
            });
        }

        [Test]
        public void AnUnborderedTargetFiefWaitsWhileAnotherTargetFiefIsWorthASiege()
        {
            var rules = new InvasionConquestRules(0.05f, 0f, 4f);

            Assert.That(_invasionTargetSiegeScores.FindUnborderedTargetSiegeScore(rules, 1f, 0.5f, 2f, false), Is.EqualTo(0f));
        }

        [Test]
        public void ALordBesiegingAnUnborderedTargetFiefKeepsBesiegingIt()
        {
            var rules = new InvasionConquestRules(0.05f, 0f, 4f);

            Assert.That(_invasionTargetSiegeScores.FindUnborderedTargetSiegeScore(rules, 1f, 0.5f, 2f, true),
                Is.EqualTo(2.2f).Within(0.001f));
        }

        [Test]
        public void AnUnborderedTargetFiefThatCannotBeTakenStaysUnwanted()
        {
            var rules = new InvasionConquestRules(0.05f, 0f, 4f);

            Assert.That(_invasionTargetSiegeScores.FindUnborderedTargetSiegeScore(rules, 0f, 0f, 2f, false), Is.EqualTo(0f));
        }

        [TestCase(0f)]
        [TestCase(1f)]
        public void WithoutAPreferenceAboveOneUnborderedFiefsAreLeftToVanilla(float preference)
        {
            var rules = new InvasionConquestRules(0.05f, 0f, preference);

            Assert.That(_invasionTargetSiegeScores.FindUnborderedTargetSiegeScore(rules, 1f, 0f, 2f, false), Is.EqualTo(0f));
        }

        [Test]
        public void AnAlliedSiegeOfATargetFiefIsWantedAboveEverySiegeTheLordCouldOpen()
        {
            var rules = new InvasionConquestRules(0.05f, 0f, 4f);

            Assert.Multiple(() =>
            {
                Assert.That(_invasionTargetSiegeScores.FindAlliedTargetSiegeScore(rules, 1f, 3f, 2f),
                    Is.EqualTo(3.3f).Within(0.001f));
                Assert.That(_invasionTargetSiegeScores.FindAlliedTargetSiegeScore(rules, 1f, 2f, 3f),
                    Is.EqualTo(3.3f).Within(0.001f));
                Assert.That(_invasionTargetSiegeScores.FindAlliedTargetSiegeScore(rules, 10f, 0f, 2f),
                    Is.EqualTo(4f).Within(0.001f));
            });
        }

        [Test]
        public void AnAlliedSiegeThatCannotBeWonStaysUnwanted()
        {
            var rules = new InvasionConquestRules(0.05f, 0f, 4f);

            Assert.That(_invasionTargetSiegeScores.FindAlliedTargetSiegeScore(rules, 0f, 3f, 2f), Is.EqualTo(0f));
        }

        [TestCase(0f)]
        [TestCase(1f)]
        public void WithoutAPreferenceAboveOneAlliedSiegesAreLeftToVanilla(float preference)
        {
            var rules = new InvasionConquestRules(0.05f, 0f, preference);

            Assert.That(_invasionTargetSiegeScores.FindAlliedTargetSiegeScore(rules, 1f, 3f, 2f), Is.EqualTo(0f));
        }
    }
}
