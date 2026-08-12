using System.Reflection;
using CustomSpawns.AI;
using NUnit.Framework;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.SaveSystem;

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

        [Test]
        public void StablePatrolDays_AreRetainedAcrossRepeatedTicksAndLoadedState()
        {
            int rollCount = 0;
            float loadedStableDays = 3.75f;

            float firstTick = PatrolAroundClosestLestInterruptedAndSwitchBehaviour.RetainOrRollStablePatrolDays(
                true, loadedStableDays, Roll);
            float secondTick = PatrolAroundClosestLestInterruptedAndSwitchBehaviour.RetainOrRollStablePatrolDays(
                true, firstTick, Roll);

            Assert.That(firstTick, Is.EqualTo(loadedStableDays));
            Assert.That(secondTick, Is.EqualTo(loadedStableDays));
            Assert.That(rollCount, Is.Zero);

            float Roll()
            {
                rollCount++;
                return 9f;
            }
        }

        [Test]
        public void PatrolState_UsesStableDistinctSaveIds()
        {
            Assert.That(SaveId("_target"), Is.EqualTo(1));
            Assert.That(SaveId("_uninterruptedPatrolDays"), Is.EqualTo(2));
            Assert.That(SaveId("_stablePatrolDays"), Is.EqualTo(3));
        }

        private static short SaveId(string fieldName)
        {
            FieldInfo field = typeof(PatrolAroundClosestLestInterruptedAndSwitchBehaviour.PatrolState)
                .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)!;
            return field.GetCustomAttribute<SaveableFieldAttribute>()!.LocalSaveId;
        }
    }
}
