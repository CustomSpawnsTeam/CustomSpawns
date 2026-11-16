using CustomSpawns.Invasions.Conquest;
using NUnit.Framework;

namespace CustomSpawns.Tests.Invasions.Conquest
{
    // Every realm of a line converges on the kingdom its newest realm conquers, and a fall launches one follower.
    public class InvasionConquestLineFrontTests
    {
        private InvasionConquests _invasionConquests = new();

        [SetUp]
        public void SetUp()
        {
            _invasionConquests = new InvasionConquests();
        }

        [Test]
        public void TheLeaderJoinsTheNewestFollowersTarget()
        {
            InvasionConquest leader = _invasionConquests.Begin("humar", "empire_w", 10)!;
            _invasionConquests.BeginFollowing("humar_2", leader, "aserai", 300);

            Assert.That(_invasionConquests.FindFrontToJoin(leader), Is.EqualTo("aserai"));
        }

        [Test]
        public void TheNewestFollowerKeepsItsOwnTarget()
        {
            InvasionConquest leader = _invasionConquests.Begin("humar", "empire_w", 10)!;
            InvasionConquest follower = _invasionConquests.BeginFollowing("humar_2", leader, "aserai", 300)!;

            Assert.That(_invasionConquests.FindFrontToJoin(follower), Is.Null);
        }

        [Test]
        public void AnOlderFollowerAlsoJoinsTheNewest()
        {
            InvasionConquest leader = _invasionConquests.Begin("humar", "empire_w", 10)!;
            InvasionConquest older = _invasionConquests.BeginFollowing("humar_2", leader, "aserai", 300)!;
            InvasionConquest newest = _invasionConquests.BeginFollowing("humar_3", leader, "sturgia", 400)!;

            Assert.Multiple(() =>
            {
                Assert.That(_invasionConquests.FindFrontToJoin(leader), Is.EqualTo("sturgia"));
                Assert.That(_invasionConquests.FindFrontToJoin(older), Is.EqualTo("sturgia"));
                Assert.That(_invasionConquests.FindFrontToJoin(newest), Is.Null);
            });
        }

        [Test]
        public void NoFrontIsJoinedWithoutAFollower()
        {
            InvasionConquest leader = _invasionConquests.Begin("humar", "empire_w", 10)!;
            InvasionConquest gone = _invasionConquests.Begin("makko", "vlandia", 10)!;
            _invasionConquests.BeginFollowing("makko_2", gone, "aserai", 300);
            _invasionConquests.Forget("makko_2");

            Assert.Multiple(() =>
            {
                Assert.That(_invasionConquests.FindFrontToJoin(leader), Is.Null);
                Assert.That(_invasionConquests.FindFrontToJoin(gone), Is.Null);
            });
        }
    }
}
