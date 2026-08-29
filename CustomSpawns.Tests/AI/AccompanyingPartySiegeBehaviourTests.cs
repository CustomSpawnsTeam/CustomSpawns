using CustomSpawns.AI;
using NUnit.Framework;

namespace CustomSpawns.Tests.AI
{
    public class AccompanyingPartySiegeBehaviourTests
    {
        [Test]
        public void Behaviour_JoinsTheSiegeOfTheMainPartyItSpawnedWith()
        {
            Assert.That(AccompanyingPartySiegeBehaviour.ShouldMatchSupportedPartySiege(
                campsAlreadyMatch: false, supportedPartyIsActive: true,
                partyIsInMapEvent: false, partyIsInSettlement: false), Is.True);
        }

        [Test]
        public void Behaviour_DoesNotRejoinTheSiegeItIsAlreadyPartOf()
        {
            Assert.That(AccompanyingPartySiegeBehaviour.ShouldMatchSupportedPartySiege(
                campsAlreadyMatch: true, supportedPartyIsActive: true,
                partyIsInMapEvent: false, partyIsInSettlement: false), Is.False);
        }

        [Test]
        public void Behaviour_DoesNotFollowAMainPartyThatIsNoLongerActive()
        {
            Assert.That(AccompanyingPartySiegeBehaviour.ShouldMatchSupportedPartySiege(
                campsAlreadyMatch: false, supportedPartyIsActive: false,
                partyIsInMapEvent: false, partyIsInSettlement: false), Is.False);
        }

        [Test]
        public void Behaviour_DoesNotPullAPartyOutOfAnOngoingBattle()
        {
            Assert.That(AccompanyingPartySiegeBehaviour.ShouldMatchSupportedPartySiege(
                campsAlreadyMatch: false, supportedPartyIsActive: true,
                partyIsInMapEvent: true, partyIsInSettlement: false), Is.False);
        }

        [Test]
        public void Behaviour_DoesNotPullAPartyOutOfASettlement()
        {
            Assert.That(AccompanyingPartySiegeBehaviour.ShouldMatchSupportedPartySiege(
                campsAlreadyMatch: false, supportedPartyIsActive: true,
                partyIsInMapEvent: false, partyIsInSettlement: true), Is.False);
        }
    }
}
