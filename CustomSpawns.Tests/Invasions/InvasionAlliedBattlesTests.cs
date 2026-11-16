using CustomSpawns.Invasions;
using NUnit.Framework;

namespace CustomSpawns.Tests.Invasions
{
    public class InvasionAlliedBattlesTests
    {
        private InvasionAlliedBattles _invasionAlliedBattles = new();

        [SetUp]
        public void SetUp()
        {
            _invasionAlliedBattles = new InvasionAlliedBattles();
        }

        [Test]
        public void ALordTooWeakAloneJoinsABattleItsAlliesAreWinning()
        {
            Assert.That(_invasionAlliedBattles.FindJoinScore(200f, 1500f, 1000f), Is.GreaterThan(1f));
        }

        [Test]
        public void ALordStaysOutOfABattleItAndItsAlliesWouldLose()
        {
            Assert.That(_invasionAlliedBattles.FindJoinScore(200f, 700f, 1000f), Is.EqualTo(0f));
        }

        [Test]
        public void ABattleIsWantedMoreTheMoreTheAlliesOutmatchTheEnemy()
        {
            Assert.That(_invasionAlliedBattles.FindJoinScore(200f, 3000f, 1000f),
                Is.GreaterThan(_invasionAlliedBattles.FindJoinScore(200f, 1500f, 1000f)));
        }

        [Test]
        public void TheScoreOfACrushingAdvantageIsCapped()
        {
            Assert.That(_invasionAlliedBattles.FindJoinScore(200f, 100000f, 10f),
                Is.EqualTo(_invasionAlliedBattles.FindJoinScore(200f, 50000f, 10f)));
        }

        [Test]
        public void ABattleWithNoEnemyLeftIsNotJoined()
        {
            Assert.That(_invasionAlliedBattles.FindJoinScore(200f, 1500f, 0f), Is.EqualTo(0f));
        }

        [Test]
        public void JoiningReplacesAWeakerInitiative()
        {
            Assert.That(_invasionAlliedBattles.OverridesInitiative(1.5f, 1.2f, false), Is.True);
        }

        [Test]
        public void JoiningLeavesAStrongerInitiative()
        {
            Assert.That(_invasionAlliedBattles.OverridesInitiative(1.5f, 5f, false), Is.False);
        }

        [Test]
        public void JoiningReplacesFleeingFromTheEnemyTheAlliesAreBeating()
        {
            Assert.That(_invasionAlliedBattles.OverridesInitiative(1.5f, 5f, true), Is.True);
        }

        [Test]
        public void ABattleBelowVanillasThresholdIsNeverJoined()
        {
            Assert.That(_invasionAlliedBattles.OverridesInitiative(0.9f, 0f, true), Is.False);
        }
    }
}
