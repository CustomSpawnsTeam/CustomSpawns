using CustomSpawns.Invasions;
using NUnit.Framework;

namespace CustomSpawns.Tests.Invasions
{
    public class InvasionLineRefillTests
    {
        private readonly InvasionLineRefill _invasionLineRefill = new();

        [Test]
        public void TheFirstRealmAloneLandsNothingBeforeAnyFall()
        {
            Assert.That(_invasionLineRefill.ShouldLaunch(0, 1, true), Is.False);
        }

        [Test]
        public void TheFirstFallLandsASecondRealm()
        {
            Assert.That(_invasionLineRefill.ShouldLaunch(1, 1, true), Is.True);
        }

        [Test]
        public void ACopyStillOnItsWayCountsAsTheRealmOwed()
        {
            Assert.That(_invasionLineRefill.ShouldLaunch(1, 2, true), Is.False);
        }

        [Test]
        public void EveryFurtherFallLandsOneMore()
        {
            Assert.That(_invasionLineRefill.ShouldLaunch(3, 2, true), Is.True);
        }

        [Test]
        public void ALineWithNoStandingRealmLandsNothing()
        {
            Assert.That(_invasionLineRefill.ShouldLaunch(2, 1, false), Is.False);
        }

        [TestCase(91530, true)]
        [TestCase(91531, false)]
        [TestCase(91559, false)]
        [TestCase(91560, true)]
        public void TheCountIsCheckedEveryThirtyDays(int day, bool expected)
        {
            Assert.That(_invasionLineRefill.IsCheckDay(day), Is.EqualTo(expected));
        }
    }
}
