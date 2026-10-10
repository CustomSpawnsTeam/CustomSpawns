using CustomSpawns.Invasions;
using NUnit.Framework;

namespace CustomSpawns.Tests.Invasions
{
    public class InvasionFoodSupportRulesTests
    {
        [TestCase(10f, 100f, 30f, 30f)]
        [TestCase(35f, 100f, 30f, 35f)]
        [TestCase(150f, 100f, 30f, 150f)]
        [TestCase(10f, 20f, 30f, 20f)]
        public void ApplyFoodStockFloor_NeverLowersStocksAndRespectsActualCapacity(float existing, float capacity,
            float floor, float expected)
        {
            Assert.That(InvasionFoodSupportRules.ApplyFoodStockFloor(existing, capacity, floor), Is.EqualTo(expected));
        }

        [Test]
        public void OngoingSupport_RequiresOneEligibleHeldUnbesiegedTown()
        {
            Assert.True(InvasionFoodSupportRules.IsEligibleForOngoingSupport(true, true, true, true, false));
            Assert.False(InvasionFoodSupportRules.IsEligibleForOngoingSupport(false, true, true, true, false));
            Assert.False(InvasionFoodSupportRules.IsEligibleForOngoingSupport(true, false, true, true, false));
            Assert.False(InvasionFoodSupportRules.IsEligibleForOngoingSupport(true, true, false, true, false));
            Assert.False(InvasionFoodSupportRules.IsEligibleForOngoingSupport(true, true, true, false, false));
            Assert.False(InvasionFoodSupportRules.IsEligibleForOngoingSupport(true, true, true, true, true));
        }

        // Cover for a 400-man floor at one food per twenty men is 20, plus whatever the town is allowed.
        [TestCase(-13f, 400, 20, 2f, 13f)]
        [TestCase(-50f, 400, 20, 2f, 22f)]
        [TestCase(4f, 400, 20, 2f, 0f)]
        [TestCase(0f, 400, 20, 2f, 0f)]
        [TestCase(-13f, 0, 20, 0f, 0f)]
        [TestCase(-13f, 400, 0, 2f, 2f)]
        public void DailySupply_CoversTheShortfallUpToTheMinimumGarrisonAndAllowance(float dailyChange,
            int minimumGarrison, int menPerFood, float townAllowance, float expected)
        {
            Assert.That(InvasionFoodSupportRules.DailySupply(dailyChange, minimumGarrison, menPerFood, townAllowance),
                Is.EqualTo(expected));
        }
    }
}
