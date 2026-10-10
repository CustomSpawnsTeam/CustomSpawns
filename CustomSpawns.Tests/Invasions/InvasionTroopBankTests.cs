using CustomSpawns.Invasions;
using NUnit.Framework;

namespace CustomSpawns.Tests.Invasions
{
    public class InvasionTroopBankTests
    {
        [Test]
        public void ALordDrawsOnlyWhatStandsAboveTheMinimumCapitalGarrison()
        {
            Assert.AreEqual(200, InvasionTroopBank.Drawable(500, 600, 400));
        }

        [Test]
        public void ALordNeverDrawsMoreThanHeAskedFor()
        {
            Assert.AreEqual(120, InvasionTroopBank.Drawable(120, 800, 400));
        }

        [Test]
        public void AGarrisonAtItsFloorGivesNothing()
        {
            Assert.AreEqual(0, InvasionTroopBank.Drawable(200, 400, 400));
        }

        [Test]
        public void AGarrisonBelowItsFloorGivesNothing()
        {
            Assert.AreEqual(0, InvasionTroopBank.Drawable(200, 150, 400));
        }
    }
}
