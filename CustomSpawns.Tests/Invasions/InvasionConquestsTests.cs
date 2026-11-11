using System.Linq;
using CustomSpawns.Invasions;
using NUnit.Framework;

namespace CustomSpawns.Tests.Invasions
{
    public class InvasionConquestsTests
    {
        private InvasionConquests _invasionConquests = new();

        [SetUp]
        public void SetUp()
        {
            _invasionConquests = new InvasionConquests();
        }

        [Test]
        public void TheFirstKingdomAFiefIsTakenFromBecomesTheTarget()
        {
            _invasionConquests.Begin("humar", "empire_w", 10);
            _invasionConquests.Begin("humar", "vlandia", 20);

            Assert.That(_invasionConquests.Find("humar")!.TargetKingdomId, Is.EqualTo("empire_w"));
        }

        [Test]
        public void AFiefTakenOutsideAnyKingdomNamesNoTarget()
        {
            Assert.That(_invasionConquests.Begin("humar", null, 10), Is.Null);
            Assert.That(_invasionConquests.Find("humar"), Is.Null);
        }

        [Test]
        public void BeforeAnyConquestTheDiplomacyConfigurationDecides()
        {
            Assert.That(_invasionConquests.FindStance("humar", "vlandia", true), Is.EqualTo(InvasionWarStance.Policy));
            Assert.That(_invasionConquests.FindStance(null, "vlandia", true), Is.EqualTo(InvasionWarStance.Policy));
        }

        [Test]
        public void ARetargetedInvaderIsAtWarWithItsNewTargetAndLeavesTheOthersToChoose()
        {
            InvasionConquest conquest = _invasionConquests.Begin("humar", "empire_s", 10)!;

            conquest.Retarget("empire_w", 400);

            Assert.Multiple(() =>
            {
                Assert.That(conquest.TargetKingdomId, Is.EqualTo("empire_w"));
                Assert.That(conquest.StartedDay, Is.EqualTo(400));
                Assert.That(conquest.LastFrontDay, Is.EqualTo(400));
                Assert.That(_invasionConquests.FindStance("humar", "empire_w", true), Is.EqualTo(InvasionWarStance.War));
                Assert.That(_invasionConquests.FindStance("humar", "empire_s", true), Is.EqualTo(InvasionWarStance.Open));
                Assert.That(_invasionConquests.FindStance("humar", "looters", false), Is.EqualTo(InvasionWarStance.Policy));
            });
        }

        [Test]
        public void DuringTheConquestTheTargetIsAtWarAndOtherKingdomsChooseForThemselves()
        {
            _invasionConquests.Begin("humar", "empire_w", 10);

            Assert.Multiple(() =>
            {
                Assert.That(_invasionConquests.FindStance("humar", "empire_w", true),
                    Is.EqualTo(InvasionWarStance.War));
                Assert.That(_invasionConquests.FindStance("humar", "vlandia", true),
                    Is.EqualTo(InvasionWarStance.Open));
                Assert.That(_invasionConquests.FindStance("humar", "looters", false),
                    Is.EqualTo(InvasionWarStance.Policy));
            });
        }

        [Test]
        public void AForgottenConquestFreesTheInvasionsWars()
        {
            _invasionConquests.Begin("humar", "empire_w", 10);
            _invasionConquests.Forget("humar");

            Assert.That(_invasionConquests.FindStance("humar", "vlandia", true), Is.EqualTo(InvasionWarStance.Policy));
        }

        [Test]
        public void ARestoredConquestKeepsItsTarget()
        {
            _invasionConquests.Restore(new InvasionConquest("humar", "sturgia", 27, 40));

            InvasionConquest conquest = _invasionConquests.Find("humar")!;

            Assert.Multiple(() =>
            {
                Assert.That(conquest.TargetKingdomId, Is.EqualTo("sturgia"));
                Assert.That(conquest.StartedDay, Is.EqualTo(27));
                Assert.That(conquest.LastFrontDay, Is.EqualTo(40));
            });
        }

        [Test]
        public void AFollowerConquersItsOwnTargetAndTheLeaderRemembersItsLastFollower()
        {
            InvasionConquest leader = _invasionConquests.Begin("humar", "empire_s", 10)!;
            leader.Retarget("vlandia", 300);

            InvasionConquest follower = _invasionConquests.BeginFollowing("humar_2", leader, "aserai", 301)!;
            _invasionConquests.BeginFollowing("humar_3", leader, "sturgia", 302);

            Assert.Multiple(() =>
            {
                Assert.That(follower.TargetKingdomId, Is.EqualTo("aserai"));
                Assert.That(follower.LeaderClanId, Is.EqualTo("humar"));
                Assert.That(leader.TargetKingdomId, Is.EqualTo("vlandia"));
                Assert.That(leader.FollowerClanId, Is.EqualTo("humar_3"));
                Assert.That(_invasionConquests.FindLeader(follower), Is.SameAs(leader));
                Assert.That(_invasionConquests.BeginFollowing("humar_2", leader, "vlandia", 303), Is.Null);
                Assert.That(_invasionConquests.BeginFollowing("humar", leader, "vlandia", 303), Is.Null);
                Assert.That(_invasionConquests.BeginFollowing("humar_4", leader, null, 303), Is.Null);
            });
        }

        [Test]
        public void AFollowerKeepsItsTargetWhenItsLeaderTurnsOnAnother()
        {
            InvasionConquest leader = _invasionConquests.Begin("humar", "vlandia", 10)!;
            InvasionConquest follower = _invasionConquests.BeginFollowing("humar_2", leader, "aserai", 11)!;

            leader.Retarget("sturgia", 500);

            Assert.That(follower.TargetKingdomId, Is.EqualTo("aserai"));
        }

        [Test]
        public void TheLeaderOfALineLaunchesEveryFollower()
        {
            InvasionConquest leader = _invasionConquests.Begin("humar", "vlandia", 10)!;
            InvasionConquest follower = _invasionConquests.BeginFollowing("humar_2", leader, "aserai", 11)!;

            Assert.Multiple(() =>
            {
                Assert.That(_invasionConquests.FindLauncher(leader), Is.SameAs(leader));
                Assert.That(_invasionConquests.FindLauncher(follower), Is.SameAs(leader));
            });
        }

        [Test]
        public void AFollowerWhoseLeaderIsGoneLaunchesItsOwn()
        {
            InvasionConquest leader = _invasionConquests.Begin("humar", "vlandia", 10)!;
            InvasionConquest follower = _invasionConquests.BeginFollowing("humar_2", leader, "aserai", 11)!;

            _invasionConquests.Forget("humar");

            Assert.Multiple(() =>
            {
                Assert.That(_invasionConquests.FindLeader(follower), Is.Null);
                Assert.That(_invasionConquests.FindLauncher(follower), Is.SameAs(follower));
                Assert.That(_invasionConquests.AreAllied("humar_2", "humar"), Is.False);
            });
        }

        [Test]
        public void FollowersOfOneLeaderAreAlliedWithEachOtherEvenOnceItIsGone()
        {
            InvasionConquest leader = _invasionConquests.Begin("humar", "vlandia", 10)!;
            InvasionConquest second = _invasionConquests.BeginFollowing("humar_2", leader, "aserai", 11)!;
            _invasionConquests.BeginFollowing("humar_3", leader, "sturgia", 12);
            _invasionConquests.Begin("makko", "battania", 13);

            Assert.Multiple(() =>
            {
                Assert.That(_invasionConquests.AreAllied("humar_2", "humar_3"), Is.True);
                Assert.That(_invasionConquests.FindAllies(second).Select(ally => ally.InvasionClanId),
                    Is.EquivalentTo(new[] { "humar", "humar_3" }));
                Assert.That(_invasionConquests.AreAllied("humar_2", "makko"), Is.False);
            });

            _invasionConquests.Forget("humar");

            Assert.That(_invasionConquests.AreAllied("humar_2", "humar_3"), Is.True);
        }

        [Test]
        public void AlliedInvasionsAreAtPeaceAndWarOnTheirOwnTargets()
        {
            InvasionConquest leader = _invasionConquests.Begin("humar", "vlandia", 10)!;
            _invasionConquests.BeginFollowing("humar_2", leader, "aserai", 11);

            Assert.Multiple(() =>
            {
                Assert.That(_invasionConquests.FindStance("humar", "humar_2_kingdom", true, "humar_2"),
                    Is.EqualTo(InvasionWarStance.Peace));
                Assert.That(_invasionConquests.FindStance("humar_2", "humar_kingdom", true, "humar"),
                    Is.EqualTo(InvasionWarStance.Peace));
                Assert.That(_invasionConquests.FindStance("humar_2", "aserai", true),
                    Is.EqualTo(InvasionWarStance.War));
                Assert.That(_invasionConquests.FindStance("humar_2", "vlandia", true),
                    Is.EqualTo(InvasionWarStance.Open));
                Assert.That(_invasionConquests.FindStance("humar", "pirates", false, "pirates"),
                    Is.EqualTo(InvasionWarStance.Policy));
            });
        }

        [Test]
        public void ARestoredFollowerRemembersItsLeaderAndTheLeaderItsFollower()
        {
            _invasionConquests.Restore(new InvasionConquest("humar", "vlandia", 10, 10, null, "humar_2"));
            _invasionConquests.Restore(new InvasionConquest("humar_2", "vlandia", 11, 11, "humar"));

            Assert.Multiple(() =>
            {
                Assert.That(_invasionConquests.Find("humar")!.FollowerClanId, Is.EqualTo("humar_2"));
                Assert.That(_invasionConquests.AreAllied("humar", "humar_2"), Is.True);
            });
        }
    }
}
