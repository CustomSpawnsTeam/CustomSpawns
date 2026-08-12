using CustomSpawns.AI;
using NUnit.Framework;
using TaleWorlds.CampaignSystem.Party;

namespace CustomSpawns.Tests.AI
{
    public class PatrolAroundClosestLestInterruptedAndSwitchBehaviourTests
    {
        [TestCase(AiBehavior.EngageParty)]
        [TestCase(AiBehavior.GoAroundParty)]
        [TestCase(AiBehavior.JoinParty)]
        [TestCase(AiBehavior.FleeToPoint)]
        public void IsPatrolInterrupted_RecognizesTemporaryPartyActions(AiBehavior behavior)
        {
            Assert.True(PatrolAroundClosestLestInterruptedAndSwitchBehaviour.IsPatrolInterrupted(
                behavior, AiBehavior.None));
            Assert.True(PatrolAroundClosestLestInterruptedAndSwitchBehaviour.IsPatrolInterrupted(
                AiBehavior.None, behavior));
        }

        [Test]
        public void UpdateUninterruptedPatrolDays_ResetsWhenInterrupted()
        {
            Assert.That(PatrolAroundClosestLestInterruptedAndSwitchBehaviour.UpdateUninterruptedPatrolDays(
                3f, true), Is.Zero);
        }

        [Test]
        public void UpdateUninterruptedPatrolDays_AdvancesOneHourWhenUninterrupted()
        {
            Assert.That(PatrolAroundClosestLestInterruptedAndSwitchBehaviour.UpdateUninterruptedPatrolDays(
                1f, false), Is.EqualTo(1f + 1f / 24f));
        }

        [Test]
        public void ShouldSwitchPatrolTarget_RequiresStablePeriodToElapse()
        {
            Assert.False(PatrolAroundClosestLestInterruptedAndSwitchBehaviour.ShouldSwitchPatrolTarget(
                1.99f, 2f, false));
            Assert.True(PatrolAroundClosestLestInterruptedAndSwitchBehaviour.ShouldSwitchPatrolTarget(
                2f, 2f, false));
        }

        [Test]
        public void ShouldSwitchPatrolTarget_DoesNotSwitchWhileInterrupted()
        {
            Assert.False(PatrolAroundClosestLestInterruptedAndSwitchBehaviour.ShouldSwitchPatrolTarget(
                0f, 0f, true));
        }
    }
}
