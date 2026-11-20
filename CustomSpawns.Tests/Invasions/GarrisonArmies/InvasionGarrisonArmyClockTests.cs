using System.Collections.Generic;
using CustomSpawns.Invasions.GarrisonArmies;
using NUnit.Framework;

namespace CustomSpawns.Tests.Invasions.GarrisonArmies
{
    public class InvasionGarrisonArmyClockTests
    {
        private InvasionGarrisonArmyClock _invasionGarrisonArmyClock = new();

        [SetUp]
        public void SetUp()
        {
            _invasionGarrisonArmyClock = new InvasionGarrisonArmyClock();
        }

        [Test]
        public void AnInvasionThatNeverRaisedAnArmyHasNoLastRaise()
        {
            Assert.That(_invasionGarrisonArmyClock.FindDaysSinceLastRaise("clan", 100.0), Is.Null);
        }

        [Test]
        public void TheDaysSinceTheLastRaiseAreCounted()
        {
            _invasionGarrisonArmyClock.MarkRaised("clan", 40.0);

            Assert.That(_invasionGarrisonArmyClock.FindDaysSinceLastRaise("clan", 100.0), Is.EqualTo(60.0));
        }

        [Test]
        public void TheSameSkipIsLoggedOnlyOnce()
        {
            Assert.Multiple(() =>
            {
                Assert.That(_invasionGarrisonArmyClock.ShouldLogSkip("clan", "no_lord"), Is.True);
                Assert.That(_invasionGarrisonArmyClock.ShouldLogSkip("clan", "no_lord"), Is.False);
                Assert.That(_invasionGarrisonArmyClock.ShouldLogSkip("clan", "garrison_at_minimum"), Is.True);
                Assert.That(_invasionGarrisonArmyClock.ShouldLogSkip("other", "no_lord"), Is.True);
            });
        }

        [Test]
        public void RaisingAnArmyForgetsTheLastSkip()
        {
            _invasionGarrisonArmyClock.ShouldLogSkip("clan", "no_lord");
            _invasionGarrisonArmyClock.MarkRaised("clan", 10.0);

            Assert.That(_invasionGarrisonArmyClock.ShouldLogSkip("clan", "no_lord"), Is.True);
        }

        [Test]
        public void TheLastRaiseSurvivesASave()
        {
            _invasionGarrisonArmyClock.MarkRaised("clan", 12.345);
            Dictionary<string, string> saved = _invasionGarrisonArmyClock.Export();
            var restored = new InvasionGarrisonArmyClock();

            restored.Restore(saved);

            Assert.That(restored.FindDaysSinceLastRaise("clan", 20.0), Is.EqualTo(20.0 - 12.345).Within(1e-9));
        }

        [Test]
        public void AnUnreadableOrMissingSaveForgetsTheLastRaise()
        {
            _invasionGarrisonArmyClock.MarkRaised("old", 5.0);

            _invasionGarrisonArmyClock.Restore(new Dictionary<string, string> { ["clan"] = "not a day" });

            Assert.Multiple(() =>
            {
                Assert.That(_invasionGarrisonArmyClock.FindDaysSinceLastRaise("clan", 20.0), Is.Null);
                Assert.That(_invasionGarrisonArmyClock.FindDaysSinceLastRaise("old", 20.0), Is.Null);
            });

            _invasionGarrisonArmyClock.Restore(null);

            Assert.That(_invasionGarrisonArmyClock.Export(), Is.Empty);
        }
    }
}
