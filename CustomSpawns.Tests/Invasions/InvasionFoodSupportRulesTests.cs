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
        public void OngoingSupport_RequiresOneEligibleCreditedUnbesiegedTown()
        {
            Assert.True(InvasionFoodSupportRules.IsEligibleForOngoingSupport(true, true, true, true, false));
            Assert.False(InvasionFoodSupportRules.IsEligibleForOngoingSupport(false, true, true, true, false));
            Assert.False(InvasionFoodSupportRules.IsEligibleForOngoingSupport(true, false, true, true, false));
            Assert.False(InvasionFoodSupportRules.IsEligibleForOngoingSupport(true, true, false, true, false));
            Assert.False(InvasionFoodSupportRules.IsEligibleForOngoingSupport(true, true, true, false, false));
            Assert.False(InvasionFoodSupportRules.IsEligibleForOngoingSupport(true, true, true, true, true));
        }
    }
}
