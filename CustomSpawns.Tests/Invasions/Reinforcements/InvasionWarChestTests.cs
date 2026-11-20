using CustomSpawns.Invasions.Reinforcements;
using NUnit.Framework;

namespace CustomSpawns.Tests.Invasions.Reinforcements
{
    public class InvasionWarChestTests
    {
        private const int Lower = 30000;
        private const int Upper = 90000;

        private readonly InvasionWarChest _invasionWarChest = new();

        [Test]
        public void APoorClansWageLimitNeedsNoReserve()
        {
            Assert.AreEqual(0, _invasionWarChest.WageLimitReserve(200, Lower, Upper));
        }

        [Test]
        public void TheLowerBandGrowsWithTheSquareOfThePurse()
        {
            Assert.AreEqual(21214, _invasionWarChest.WageLimitReserve(400, Lower, Upper));
            Assert.AreEqual(30000, _invasionWarChest.WageLimitReserve(600, Lower, Upper));
        }

        [Test]
        public void TheUpperBandGrowsLinearly()
        {
            Assert.AreEqual(60000, _invasionWarChest.WageLimitReserve(900, Lower, Upper));
            Assert.AreEqual(90000, _invasionWarChest.WageLimitReserve(1200, Lower, Upper));
        }

        [Test]
        public void AboveTheUpperBandTheClanPaysWhateverItsPartiesCost()
        {
            Assert.AreEqual(90001, _invasionWarChest.WageLimitReserve(5000, Lower, Upper));
        }

        [Test]
        public void TheAllowanceIsTheReserveAndTheWagesLessThePurse()
        {
            Assert.AreEqual(30000 + 700 * 7 - 10000,
                _invasionWarChest.Allowance(600, 700, 7f, 10000, Lower, Upper));
        }

        [Test]
        public void ARichClanIsPaidNothing()
        {
            Assert.AreEqual(0, _invasionWarChest.Allowance(600, 700, 7f, 100000, Lower, Upper));
        }

        [Test]
        public void FoodIsSizedForTheWholeWarband()
        {
            Assert.AreEqual(120 / 20 * 16 - 30, _invasionWarChest.FoodShortfall(120, 20, 16f, 30));
            Assert.AreEqual(0, _invasionWarChest.FoodShortfall(120, 20, 16f, 500));
        }
    }
}
