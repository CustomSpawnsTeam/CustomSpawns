using CustomSpawns.Invasions;
using NUnit.Framework;

namespace CustomSpawns.Tests.Invasions
{
    public class InvasionTroopBankTests
    {
        private readonly InvasionTroopBank _invasionTroopBank = new();

        [Test]
        public void ALordDrawsOnlyWhatStandsAboveTheMinimumCapitalGarrison()
        {
            Assert.AreEqual(200, _invasionTroopBank.Drawable(500, 600, 400));
        }

        [Test]
        public void ALordNeverDrawsMoreThanHeAskedFor()
        {
            Assert.AreEqual(120, _invasionTroopBank.Drawable(120, 800, 400));
        }

        [Test]
        public void AGarrisonAtItsFloorGivesNothing()
        {
            Assert.AreEqual(0, _invasionTroopBank.Drawable(200, 400, 400));
        }

        [Test]
        public void AGarrisonBelowItsFloorGivesNothing()
        {
            Assert.AreEqual(0, _invasionTroopBank.Drawable(200, 150, 400));
        }

        [Test]
        public void ALordKeepsHisFreeWarbandWhenTheBankCannotMatchIt()
        {
            Assert.IsFalse(_invasionTroopBank.ReplacesFreeWarband(60, 430, 400));
            Assert.IsTrue(_invasionTroopBank.ReplacesFreeWarband(60, 460, 400));
        }

        [Test]
        public void LordsAreCappedByTheBaseAndTheTownsHeld()
        {
            // 6 + 3 * 1 = 9 allowed, 7 alive: two of the wave's three come.
            Assert.AreEqual(2, _invasionTroopBank.LordsToField(3, 6, 3, 1, 7));
        }

        [Test]
        public void AnInvasionAtItsLordCapFieldsNone()
        {
            Assert.AreEqual(0, _invasionTroopBank.LordsToField(3, 6, 3, 0, 20));
        }

        [Test]
        public void AnUncappedInvasionFieldsTheWholeWave()
        {
            Assert.AreEqual(3, _invasionTroopBank.LordsToField(3, 0, 3, 1, 20));
        }
    }
}
