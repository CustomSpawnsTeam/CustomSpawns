using CustomSpawns.Invasions;
using NUnit.Framework;

namespace CustomSpawns.Tests.Invasions
{
    public class InvasionCrownRestorationTests
    {
        private Invasion _invasion = null!;

        [SetUp]
        public void SetUp()
        {
            _invasion = new Invasion("template", "founder");
            _invasion.AddClan("expanded-clan");
        }

        [TestCase(null)]
        [TestCase("kingdom")]
        public void AnEligibleFounderCanRegainTheCrownFromItsExpandedClan(string? founderKingdomId)
        {
            Assert.True(_invasion.ShouldRestoreCrown("kingdom", "expanded-clan", false,
                (false, true, founderKingdomId)));
        }

        [TestCase(null)]
        [TestCase("founder")]
        [TestCase("foreign-clan")]
        public void AMissingUnchangedOrUnrelatedRulerDoesNotAuthorizeRestoration(string? rulingClanId)
        {
            Assert.False(_invasion.ShouldRestoreCrown("kingdom", rulingClanId, false,
                (false, true, "kingdom")));
        }

        [Test]
        public void AnEliminatedKingdomDoesNotHaveItsCrownRestored()
        {
            Assert.False(_invasion.ShouldRestoreCrown("kingdom", "expanded-clan", true,
                (false, true, "kingdom")));
        }

        [TestCase(true, true, "kingdom")]
        [TestCase(false, false, "kingdom")]
        [TestCase(false, true, "other-kingdom")]
        public void AnEliminatedLeaderlessOrForeignFounderCannotRegainTheCrown(bool eliminated, bool hasLeader,
            string founderKingdomId)
        {
            Assert.False(_invasion.ShouldRestoreCrown("kingdom", "expanded-clan", false,
                (eliminated, hasLeader, founderKingdomId)));
        }

        [Test]
        public void SuspensionAndSpentForcesDoNotChangeCrownEligibility()
        {
            _invasion.IsSuspended = true;
            Assert.That(_invasion.MemberPartyIds, Is.Empty);

            Assert.True(_invasion.ShouldRestoreCrown("kingdom", "expanded-clan", false,
                (false, true, null)));
        }
    }
}
