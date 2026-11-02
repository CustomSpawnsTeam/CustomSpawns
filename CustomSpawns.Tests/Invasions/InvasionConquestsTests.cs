using CustomSpawns.Invasions;
using NUnit.Framework;

namespace CustomSpawns.Tests.Invasions
{
    public class InvasionConquestsTests
    {
        private readonly InvasionConquestRules _rules = new(0.05f, 30f);
        private InvasionConquests _invasionConquests = new();

        [SetUp]
        public void SetUp()
        {
            _invasionConquests = new InvasionConquests();
        }

        [Test]
        public void TheFirstKingdomAFiefIsTakenFromBecomesTheTarget()
        {
            _invasionConquests.Begin("humar", "empire_w", 10);
            _invasionConquests.Begin("humar", "vlandia", 20);

            Assert.That(_invasionConquests.Find("humar")!.TargetKingdomId, Is.EqualTo("empire_w"));
        }

        [Test]
        public void AFiefTakenOutsideAnyKingdomNamesNoTarget()
        {
            Assert.That(_invasionConquests.Begin("humar", null, 10), Is.Null);
            Assert.That(_invasionConquests.Find("humar"), Is.Null);
        }

        [Test]
        public void BeforeAnyConquestTheDiplomacyConfigurationDecides()
        {
            Assert.That(_invasionConquests.FindStance("humar", "vlandia", true), Is.EqualTo(InvasionWarStance.Policy));
            Assert.That(_invasionConquests.FindStance(null, "vlandia", true), Is.EqualTo(InvasionWarStance.Policy));
        }

        [Test]
        public void ASettledInvaderLeavesEveryKingdomToChooseAndMinorFactionsToTheConfiguration()
        {
            _invasionConquests.Begin("humar", "empire_w", 10)!.Settle();

            Assert.Multiple(() =>
            {
                Assert.True(_invasionConquests.IsSettled("humar"));
                Assert.That(_invasionConquests.FindStance("humar", "empire_w", true), Is.EqualTo(InvasionWarStance.Open));
                Assert.That(_invasionConquests.FindStance("humar", "vlandia", true), Is.EqualTo(InvasionWarStance.Open));
                Assert.That(_invasionConquests.FindStance("humar", "looters", false), Is.EqualTo(InvasionWarStance.Policy));
            });
        }

        [Test]
        public void AnInvaderStillConqueringIsNotSettled()
        {
            _invasionConquests.Begin("humar", "empire_w", 10);

            Assert.False(_invasionConquests.IsSettled("humar"));
            Assert.False(_invasionConquests.IsSettled(null));
        }

        [Test]
        public void DuringTheConquestTheTargetIsAtWarAndOtherKingdomsChooseForThemselves()
        {
            _invasionConquests.Begin("humar", "empire_w", 10);

            Assert.Multiple(() =>
            {
                Assert.That(_invasionConquests.FindStance("humar", "empire_w", true),
                    Is.EqualTo(InvasionWarStance.War));
                Assert.That(_invasionConquests.FindStance("humar", "vlandia", true),
                    Is.EqualTo(InvasionWarStance.Open));
                Assert.That(_invasionConquests.FindStance("humar", "looters", false),
                    Is.EqualTo(InvasionWarStance.Policy));
            });
        }

        [Test]
        public void AWinnableSiegeOfTheConqueredKingdomIsWantedAboveEveryOtherSiege()
        {
            var rules = new InvasionConquestRules(0.05f, 30f, 4f);

            Assert.That(_invasionConquests.FindTargetSiegeScore(rules, 0.1f, 2f), Is.EqualTo(2.2f).Within(0.001f));
        }

        [Test]
        public void AmongTheConqueredKingdomsFiefsThePreferenceMultipliesVanillasWish()
        {
            var rules = new InvasionConquestRules(0.05f, 30f, 4f);

            Assert.That(_invasionConquests.FindTargetSiegeScore(rules, 1f, 2f), Is.EqualTo(4f).Within(0.001f));
        }

        [Test]
        public void ASiegeOfTheConqueredKingdomThatCannotBeWonStaysUnwanted()
        {
            var rules = new InvasionConquestRules(0.05f, 30f, 4f);

            Assert.That(_invasionConquests.FindTargetSiegeScore(rules, 0f, 2f), Is.EqualTo(0f));
        }

        [TestCase(0f)]
        [TestCase(1f)]
        [TestCase(0.5f)]
        public void WithoutAPreferenceAboveOneSiegesAreLeftToVanilla(float preference)
        {
            var rules = new InvasionConquestRules(0.05f, 30f, preference);

            Assert.That(_invasionConquests.FindTargetSiegeScore(rules, 0.3f, 2f), Is.EqualTo(0.3f));
        }

        [Test]
        public void AnUnborderedTargetFiefIsWantedAboveEveryOtherSiegeWhenNoTargetFiefIsWithinReach()
        {
            var rules = new InvasionConquestRules(0.05f, 0f, 4f);

            Assert.Multiple(() =>
            {
                Assert.That(_invasionConquests.FindUnborderedTargetSiegeScore(rules, 1f, 0f, 2f, false),
                    Is.EqualTo(2.2f).Within(0.001f));
                Assert.That(_invasionConquests.FindUnborderedTargetSiegeScore(rules, 10f, 0f, 2f, false),
                    Is.EqualTo(4f).Within(0.001f));
            });
        }

        [Test]
        public void AnUnborderedTargetFiefWaitsWhileAnotherTargetFiefIsWorthASiege()
        {
            var rules = new InvasionConquestRules(0.05f, 0f, 4f);

            Assert.That(_invasionConquests.FindUnborderedTargetSiegeScore(rules, 1f, 0.5f, 2f, false), Is.EqualTo(0f));
        }

        [Test]
        public void ALordBesiegingAnUnborderedTargetFiefKeepsBesiegingIt()
        {
            var rules = new InvasionConquestRules(0.05f, 0f, 4f);

            Assert.That(_invasionConquests.FindUnborderedTargetSiegeScore(rules, 1f, 0.5f, 2f, true),
                Is.EqualTo(2.2f).Within(0.001f));
        }

        [Test]
        public void AnUnborderedTargetFiefThatCannotBeTakenStaysUnwanted()
        {
            var rules = new InvasionConquestRules(0.05f, 0f, 4f);

            Assert.That(_invasionConquests.FindUnborderedTargetSiegeScore(rules, 0f, 0f, 2f, false), Is.EqualTo(0f));
        }

        [TestCase(0f)]
        [TestCase(1f)]
        public void WithoutAPreferenceAboveOneUnborderedFiefsAreLeftToVanilla(float preference)
        {
            var rules = new InvasionConquestRules(0.05f, 0f, preference);

            Assert.That(_invasionConquests.FindUnborderedTargetSiegeScore(rules, 1f, 0f, 2f, false), Is.EqualTo(0f));
        }

        [Test]
        public void AFootholdIsLessThanHalfTheThreatOfARealm()
        {
            float foothold = _invasionConquests.FindThreat(_rules, 2, 200000f);
            float realm = _invasionConquests.FindThreat(_rules, 6, 600000f);

            Assert.That(realm, Is.EqualTo(9 * foothold).Within(0.01f));
        }

        [TestCase(0, 100000f)]
        [TestCase(3, 0f)]
        public void AnInvaderWithNothingIsNoThreat(int fiefsHeld, float invaderWorth)
        {
            Assert.That(_invasionConquests.FindThreat(_rules, fiefsHeld, invaderWorth), Is.Zero);
        }

        [Test]
        public void AnUnsetThreatLeavesTheNeighboursToVanilla()
        {
            Assert.That(_invasionConquests.FindThreat(new InvasionConquestRules(0f, 30f), 8, 800000f), Is.Zero);
        }

        [Test]
        public void TheNearestFiefOfAKingdomAtPeaceOpensAFront()
        {
            InvasionConquest conquest = _invasionConquests.Begin("humar", "vlandia", 10)!;

            Assert.That(_invasionConquests.FindFrontToOpen(conquest, _rules, new[]
            {
                new InvasionPrey("vlandia", 40f, true), new InvasionPrey("empire_w", 12f, false)
            }, 40), Is.EqualTo("empire_w"));
        }

        [Test]
        public void TheNearestFiefOfAnEnemyIsTakenWithoutAnyNewWar()
        {
            InvasionConquest conquest = _invasionConquests.Begin("humar", "vlandia", 10)!;

            Assert.That(_invasionConquests.FindFrontToOpen(conquest, _rules, new[]
            {
                new InvasionPrey("vlandia", 8f, true), new InvasionPrey("empire_w", 12f, false)
            }, 40), Is.Null);
        }

        [Test]
        public void ANewFrontWaitsForTheLastOneToSettle()
        {
            InvasionConquest conquest = _invasionConquests.Begin("humar", "vlandia", 10)!;
            conquest.OpenFront(30);

            Assert.That(_invasionConquests.FindFrontToOpen(conquest, _rules,
                new[] { new InvasionPrey("empire_w", 12f, false) }, 59), Is.Null);
            Assert.That(_invasionConquests.FindFrontToOpen(conquest, _rules,
                new[] { new InvasionPrey("empire_w", 12f, false) }, 60), Is.EqualTo("empire_w"));
        }

        [Test]
        public void AnUnsetIntervalNeverOpensAFront()
        {
            InvasionConquest conquest = _invasionConquests.Begin("humar", "vlandia", 10)!;

            Assert.That(_invasionConquests.FindFrontToOpen(conquest, new InvasionConquestRules(0.05f, 0f),
                new[] { new InvasionPrey("empire_w", 12f, false) }, 500), Is.Null);
        }

        [Test]
        public void NothingLeftToTakeOpensNoFront()
        {
            InvasionConquest conquest = _invasionConquests.Begin("humar", "vlandia", 10)!;

            Assert.That(_invasionConquests.FindFrontToOpen(conquest, _rules, new InvasionPrey[0], 500), Is.Null);
        }

        [Test]
        public void AForgottenConquestFreesTheInvasionsWars()
        {
            _invasionConquests.Begin("humar", "empire_w", 10);
            _invasionConquests.Forget("humar");

            Assert.That(_invasionConquests.FindStance("humar", "vlandia", true), Is.EqualTo(InvasionWarStance.Policy));
        }

        [Test]
        public void ARestoredConquestKeepsItsTarget()
        {
            _invasionConquests.Restore(new InvasionConquest("humar", "sturgia", 27, 40));

            InvasionConquest conquest = _invasionConquests.Find("humar")!;

            Assert.Multiple(() =>
            {
                Assert.That(conquest.TargetKingdomId, Is.EqualTo("sturgia"));
                Assert.That(conquest.StartedDay, Is.EqualTo(27));
                Assert.That(conquest.LastFrontDay, Is.EqualTo(40));
            });
        }
    }
}
