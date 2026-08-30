using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using CustomSpawns.AI;
using CustomSpawns.AI.Model;
using NUnit.Framework;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.SaveSystem;

namespace CustomSpawns.Tests.AI
{
    public class SettlementOffensiveStateMachineTests
    {
        [Test]
        public void Recon_ClearProgressAdvancesHourlyAndThreatResetDoesNotRerollEpisode()
        {
            SettlementOffensiveBehaviourState state = BeginEpisode(out int rolls);
            state.Phase = SettlementOffensivePhase.Recon;

            Machine(state).UpdateRecon(true, false);
            Machine(state).UpdateRecon(false, false);

            Assert.That(state.AccumulatedClearReconDays, Is.Zero);
            Assert.That(state.RequiredClearReconDays, Is.EqualTo(2f));
            Assert.That(state.OperationWindowDays, Is.EqualTo(3f));
            Assert.That(rolls, Is.EqualTo(2));
        }

        [Test]
        public void Recon_DirectSkipRequiresSafetyAndDoesNotAlterRolledValues()
        {
            SettlementOffensiveBehaviourState state = BeginEpisode(out _);
            state.Phase = SettlementOffensivePhase.Recon;
            float reconRoll = state.RequiredClearReconDays;
            float windowRoll = state.OperationWindowDays;

            Machine(state).UpdateRecon(false, true);
            Assert.That(state.Phase, Is.EqualTo(SettlementOffensivePhase.Recon));

            Machine(state).UpdateRecon(true, true);
            Assert.That(state.Phase, Is.EqualTo(SettlementOffensivePhase.Approach));
            Assert.That(state.RequiredClearReconDays, Is.EqualTo(reconRoll));
            Assert.That(state.OperationWindowDays, Is.EqualTo(windowRoll));
        }

        [TestCase((int)SettlementOffensivePhase.Search)]
        [TestCase((int)SettlementOffensivePhase.Recon)]
        [TestCase((int)SettlementOffensivePhase.Approach)]
        public void OperationClock_AdvancesDuringPreOperationPhases(int phaseValue)
        {
            var phase = (SettlementOffensivePhase)phaseValue;
            var state = new SettlementOffensiveBehaviourState
            {
                Phase = phase,
                OperationWindowDays = 10f
            };

            Assert.That(Machine(state).AdvanceOperationClock(2f), Is.False);
            Assert.That(state.ElapsedOperationDays, Is.EqualTo(1f / 24f));
        }

        [TestCase((int)SettlementOffensivePhase.Active)]
        [TestCase((int)SettlementOffensivePhase.Recovery)]
        public void OperationClock_FreezesDuringActiveAndRecovery(int phaseValue)
        {
            var phase = (SettlementOffensivePhase)phaseValue;
            var state = new SettlementOffensiveBehaviourState
            {
                Phase = phase,
                OperationWindowDays = 1f / 24f,
                ElapsedOperationDays = 1f / 24f
            };

            Assert.That(Machine(state).AdvanceOperationClock(2f), Is.False);
            Assert.That(state.Phase, Is.EqualTo(phase));
            Assert.That(state.ElapsedOperationDays, Is.EqualTo(1f / 24f));
        }

        [Test]
        public void ActiveOperation_IsNeverCancelledByExpiredOperationWindow()
        {
            var state = new SettlementOffensiveBehaviourState
            {
                Phase = SettlementOffensivePhase.Approach,
                OperationWindowDays = 0f
            };

            Machine(state).ObserveTarget(true, false, true,
                false, 1, 2f);

            Assert.That(state.Phase, Is.EqualTo(SettlementOffensivePhase.Active));
            Assert.That(Machine(state).AdvanceOperationClock(2f), Is.False);
        }

        [Test]
        public void ActiveOperation_LootCompletionEntersCooldown()
        {
            var state = new SettlementOffensiveBehaviourState { Phase = SettlementOffensivePhase.Active };

            ActiveTargetObservation result = Machine(state).ObserveTarget(true, true, false, false, 2, 4f);

            Assert.That(result, Is.EqualTo(ActiveTargetObservation.EnteredCooldown));
            Assert.That(state.Phase, Is.EqualTo(SettlementOffensivePhase.Cooldown));
            Assert.That(state.CooldownRemainingDays, Is.EqualTo(4f));
        }

        [Test]
        public void InvalidTarget_IsDiscardedWithoutFailedAttempt()
        {
            var state = new SettlementOffensiveBehaviourState
            {
                Target = DummySettlement(),
                Phase = SettlementOffensivePhase.Active
            };

            ActiveTargetObservation result = Machine(state).ObserveTarget(false, false, false, false, 2, 4f);

            Assert.That(result, Is.EqualTo(ActiveTargetObservation.Cleared));
            Assert.That(state.Target, Is.Null);
            Assert.That(state.FailedAttempts, Is.Zero);
            Assert.That(state.Phase, Is.EqualTo(SettlementOffensivePhase.Search));
        }

        [Test]
        public void MilitiaContinuation_ReturnsToApproachWithoutFailure()
        {
            Settlement target = DummySettlement();
            var state = new SettlementOffensiveBehaviourState
            {
                Target = target,
                Phase = SettlementOffensivePhase.Active
            };

            Machine(state).ObserveTarget(true, false, false,
                true, 2, 4f);

            Assert.That(state.Target, Is.SameAs(target));
            Assert.That(state.Phase, Is.EqualTo(SettlementOffensivePhase.Approach));
            Assert.That(state.FailedAttempts, Is.Zero);
        }

        [Test]
        public void Interruption_EntersSameTargetRecoveryAndIncrementsExactlyOnce()
        {
            Settlement target = DummySettlement();
            var state = new SettlementOffensiveBehaviourState
            {
                Target = target,
                Phase = SettlementOffensivePhase.Active,
                RecoveryGraceDays = 1f
            };

            Machine(state).ObserveTarget(true, false, false,
                false, 2, 4f);
            Machine(state).ObserveTarget(true, false, false,
                false, 2, 4f);

            Assert.That(state.Target, Is.SameAs(target));
            Assert.That(state.Phase, Is.EqualTo(SettlementOffensivePhase.Recovery));
            Assert.That(state.FailedAttempts, Is.EqualTo(1));
        }

        [Test]
        public void Recovery_MatchingOperationResumesAndGraceExpiryDiscardsTarget()
        {
            Settlement target = DummySettlement();
            var resumed = new SettlementOffensiveBehaviourState
            {
                Target = target,
                Phase = SettlementOffensivePhase.Recovery,
                RecoveryGraceDays = 1f / 24f
            };
            Machine(resumed).ObserveTarget(true, false, true,
                false, 2, 4f);
            Assert.That(resumed.Phase, Is.EqualTo(SettlementOffensivePhase.Active));
            Assert.That(resumed.Target, Is.SameAs(target));

            var expired = new SettlementOffensiveBehaviourState
            {
                Target = target,
                Phase = SettlementOffensivePhase.Recovery,
                RecoveryGraceDays = 1f / 24f
            };
            Assert.That(Machine(expired).TickRecovery(), Is.True);
            Assert.That(expired.Target, Is.Null);
            Assert.That(expired.Phase, Is.EqualTo(SettlementOffensivePhase.Search));
        }

        [Test]
        public void FailureCap_AllowsFinalResumeThenDiscardsNextInterruptionWithoutIncrement()
        {
            var state = new SettlementOffensiveBehaviourState
            {
                Target = DummySettlement(),
                Phase = SettlementOffensivePhase.Active,
                FailedAttempts = 1
            };

            Machine(state).ObserveTarget(true, false, false,
                false, 2, 4f);
            Assert.That(state.FailedAttempts, Is.EqualTo(2));
            Assert.That(state.Phase, Is.EqualTo(SettlementOffensivePhase.Recovery));

            Machine(state).ObserveTarget(true, false, true,
                false, 2, 4f);
            Machine(state).ObserveTarget(true, false, false,
                false, 2, 4f);

            Assert.That(state.Target, Is.Null);
            Assert.That(state.FailedAttempts, Is.EqualTo(2));
            Assert.That(state.Phase, Is.EqualTo(SettlementOffensivePhase.Search));
        }

        [Test]
        public void OperationExpiryAndCooldown_ReturnNotApplicableForEveryCooldownTick()
        {
            var state = new SettlementOffensiveBehaviourState
            {
                Phase = SettlementOffensivePhase.Search,
                OperationWindowDays = 1f / 24f
            };

            Assert.That(Machine(state).AdvanceOperationClock(2f), Is.True);
            Assert.That(state.Phase, Is.EqualTo(SettlementOffensivePhase.Cooldown));
            Assert.That(Machine(state).TickCooldown(), Is.True);
            Assert.That(state.CooldownRemainingDays, Is.EqualTo(2f - 1f / 24f));
        }

        [Test]
        public void PersistedState_RetainsEpisodeRollsProgressAndCountersWithStableIds()
        {
            var state = new SettlementOffensiveBehaviourState
            {
                Target = DummySettlement(),
                Phase = SettlementOffensivePhase.Recovery,
                RequiredClearReconDays = 1.1f,
                AccumulatedClearReconDays = 0.2f,
                OperationWindowDays = 3.3f,
                ElapsedOperationDays = 1.4f,
                RecoveryGraceDays = 0.5f,
                RecoveryElapsedDays = 0.1f,
                FailedAttempts = 2,
                CooldownRemainingDays = 4.2f
            };

            Assert.Multiple(() =>
            {
                Assert.That(state.RequiredClearReconDays, Is.EqualTo(1.1f));
                Assert.That(state.OperationWindowDays, Is.EqualTo(3.3f));
                Assert.That(state.FailedAttempts, Is.EqualTo(2));
                Assert.That(SaveId("_target"), Is.EqualTo(1));
                Assert.That(SaveId("_phase"), Is.EqualTo(2));
                Assert.That(SaveId("_requiredClearReconDays"), Is.EqualTo(3));
                Assert.That(SaveId("_accumulatedClearReconDays"), Is.EqualTo(4));
                Assert.That(SaveId("_operationWindowDays"), Is.EqualTo(5));
                Assert.That(SaveId("_elapsedOperationDays"), Is.EqualTo(6));
                Assert.That(SaveId("_recoveryGraceDays"), Is.EqualTo(7));
                Assert.That(SaveId("_recoveryElapsedDays"), Is.EqualTo(8));
                Assert.That(SaveId("_failedAttempts"), Is.EqualTo(9));
                Assert.That(SaveId("_cooldownRemainingDays"), Is.EqualTo(10));
                Assert.That(SaveId("_targetMapFaction"), Is.EqualTo(11));
            });
        }

        private static SettlementOffensiveStateMachine Machine(SettlementOffensiveBehaviourState state)
        {
            return new SettlementOffensiveStateMachine(state);
        }

        private static SettlementOffensiveBehaviourState BeginEpisode(out int rolls)
        {
            int rollCount = 0;
            var state = new SettlementOffensiveBehaviourState();
            var settings = new SettlementOffensiveSettings(2f, 2f, 3f, 3f,
                1f, 2, 4f);
            Machine(state).BeginEpisode(settings, Roll);
            rolls = rollCount;
            return state;

            float Roll(float minimum, float maximum)
            {
                rollCount++;
                return minimum;
            }
        }


        private static Settlement DummySettlement()
        {
            return (Settlement)FormatterServices.GetUninitializedObject(typeof(Settlement));
        }

        private static short SaveId(string fieldName)
        {
            FieldInfo field = typeof(SettlementOffensiveBehaviourState)
                .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)!;
            return field.GetCustomAttribute<SaveableFieldAttribute>()!.LocalSaveId;
        }
    }
}
