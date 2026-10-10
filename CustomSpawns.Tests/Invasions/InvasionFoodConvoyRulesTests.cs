using CustomSpawns.Invasions;
using NUnit.Framework;

namespace CustomSpawns.Tests.Invasions
{
    public class InvasionFoodConvoyRulesTests
    {
        [TestCase(0, 7d, false)]
        [TestCase(100, 0d, false)]
        [TestCase(100, 7d, true)]
        public void AConvoyRunsOnlyWhenBothFoodAndACadenceAreConfigured(int foodPerConvoy, double intervalInDays,
            bool expected)
        {
            Assert.That(InvasionFoodConvoyRules.IsConfigured(foodPerConvoy, intervalInDays), Is.EqualTo(expected));
        }

        [Test]
        public void TheFirstConvoyIsAnIntervalAfterTheCapitalIsTaken()
        {
            InvasionFoodConvoyDecision decision = InvasionFoodConvoyRules.Decide(null, 10d, 7d, true);

            Assert.IsFalse(decision.Unloads);
            Assert.That(decision.NextConvoyDays, Is.EqualTo(17d));
        }

        [Test]
        public void ADueConvoyUnloadsAndSetsTheNextOneAnIntervalOut()
        {
            InvasionFoodConvoyDecision decision = InvasionFoodConvoyRules.Decide(17d, 17d, 7d, true);

            Assert.IsTrue(decision.Unloads);
            Assert.That(decision.NextConvoyDays, Is.EqualTo(24d));
        }

        [Test]
        public void AConvoyThatIsNotDueYetLeavesTheClockAlone()
        {
            InvasionFoodConvoyDecision decision = InvasionFoodConvoyRules.Decide(17d, 16d, 7d, true);

            Assert.IsFalse(decision.Unloads);
            Assert.That(decision.NextConvoyDays, Is.EqualTo(17d));
        }

        // The convoy waits for the way in rather than being forfeited to a siege.
        [Test]
        public void AnUnreachableCapitalHoldsTheClockWhereItIs()
        {
            InvasionFoodConvoyDecision overdue = InvasionFoodConvoyRules.Decide(17d, 30d, 7d, false);

            Assert.IsFalse(overdue.Unloads);
            Assert.That(overdue.NextConvoyDays, Is.EqualTo(17d));
            Assert.IsNull(InvasionFoodConvoyRules.Decide(null, 30d, 7d, false).NextConvoyDays);
        }

        [Test]
        public void AConvoyOverdueByDaysUnloadsOnceAndRestartsFromNow()
        {
            InvasionFoodConvoyDecision decision = InvasionFoodConvoyRules.Decide(17d, 40d, 7d, true);

            Assert.IsTrue(decision.Unloads);
            Assert.That(decision.NextConvoyDays, Is.EqualTo(47d));
        }
    }
}
