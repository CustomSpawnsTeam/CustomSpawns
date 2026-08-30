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
    public class SettlementOffensiveBehaviourTests
    {
        [Test]
        public void VisibleHostiles_UseObserverSeeingRangeBoundaryAndExcludeInvalidParties()
        {
            var parties = new[]
            {
                Strength(distance: 5f, partyStrength: 10f),
                Strength(distance: 5.001f, partyStrength: 100f),
                Strength(distance: 1f, partyStrength: 100f, isSelf: true),
                Strength(distance: 1f, partyStrength: 100f, isActive: false),
                Strength(distance: 1f, partyStrength: 100f, isHostile: false)
            };

            Assert.That(SettlementOffensiveBehaviour.SumVisibleHostileStrength(parties, 5f),
                Is.EqualTo(10f));
            Assert.That(SettlementOffensiveBehaviour.IsWithinSeeingRange(5f, 5f), Is.True);
            Assert.That(SettlementOffensiveBehaviour.IsWithinSeeingRange(5.001f, 5f), Is.False);
        }

        [Test]
        public void VisibleHostiles_CountOnlyVisibleArmyMembersOnce()
        {
            object army = new();
            var parties = new[]
            {
                Strength(1f, 20f, army),
                Strength(1f, 30f, army),
                Strength(5.001f, 100f, army)
            };

            Assert.That(SettlementOffensiveBehaviour.SumVisibleHostileStrength(parties, 5f),
                Is.EqualTo(50f));
        }

        [Test]
        public void VisibleHostiles_CountVisibleAttachmentWhenLeaderIsOutsideRange()
        {
            object army = new();
            var parties = new[]
            {
                Strength(5.001f, 20f, army),
                Strength(5f, 30f, army)
            };

            Assert.That(SettlementOffensiveBehaviour.SumVisibleHostileStrength(parties, 5f),
                Is.EqualTo(30f));
        }

        [Test]
        public void VisibleHostiles_DoNotCountInvisibleAttachmentsThroughVisibleLeader()
        {
            object army = new();
            var parties = new[]
            {
                Strength(5f, 20f, army),
                Strength(5.001f, 30f, army)
            };

            Assert.That(SettlementOffensiveBehaviour.SumVisibleHostileStrength(parties, 5f),
                Is.EqualTo(20f));
        }

        [Test]
        public void Recon_ClearProgressAdvancesHourlyAndThreatResetDoesNotRerollEpisode()
        {
            SettlementOffensiveBehaviourState state = BeginEpisode(out int rolls);
            state.Phase = SettlementOffensivePhase.Recon;

            SettlementOffensiveBehaviour.UpdateRecon(state, true, false);
            SettlementOffensiveBehaviour.UpdateRecon(state, false, false);

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

            SettlementOffensiveBehaviour.UpdateRecon(state, false, true);
            Assert.That(state.Phase, Is.EqualTo(SettlementOffensivePhase.Recon));

            SettlementOffensiveBehaviour.UpdateRecon(state, true, true);
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

            Assert.That(SettlementOffensiveBehaviour.AdvanceOperationClock(state, 2f), Is.False);
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

            Assert.That(SettlementOffensiveBehaviour.AdvanceOperationClock(state, 2f), Is.False);
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

            SettlementOffensiveBehaviour.ObserveTarget(state, true, false, true,
                false, 1, 2f);

            Assert.That(state.Phase, Is.EqualTo(SettlementOffensivePhase.Active));
            Assert.That(SettlementOffensiveBehaviour.AdvanceOperationClock(state, 2f), Is.False);
        }

        [Test]
        public void ActiveOperation_LootCompletionEntersCooldown()
        {
            var state = new SettlementOffensiveBehaviourState { Phase = SettlementOffensivePhase.Active };

            ActiveTargetObservation result = SettlementOffensiveBehaviour.ObserveTarget(state,
                true, true, false, false, 2, 4f);

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

            ActiveTargetObservation result = SettlementOffensiveBehaviour.ObserveTarget(state,
                false, false, false, false, 2, 4f);

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

            SettlementOffensiveBehaviour.ObserveTarget(state, true, false, false,
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

            SettlementOffensiveBehaviour.ObserveTarget(state, true, false, false,
                false, 2, 4f);
            SettlementOffensiveBehaviour.ObserveTarget(state, true, false, false,
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
            SettlementOffensiveBehaviour.ObserveTarget(resumed, true, false, true,
                false, 2, 4f);
            Assert.That(resumed.Phase, Is.EqualTo(SettlementOffensivePhase.Active));
            Assert.That(resumed.Target, Is.SameAs(target));

            var expired = new SettlementOffensiveBehaviourState
            {
                Target = target,
                Phase = SettlementOffensivePhase.Recovery,
                RecoveryGraceDays = 1f / 24f
            };
            Assert.That(SettlementOffensiveBehaviour.TickRecovery(expired), Is.True);
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

            SettlementOffensiveBehaviour.ObserveTarget(state, true, false, false,
                false, 2, 4f);
            Assert.That(state.FailedAttempts, Is.EqualTo(2));
            Assert.That(state.Phase, Is.EqualTo(SettlementOffensivePhase.Recovery));

            SettlementOffensiveBehaviour.ObserveTarget(state, true, false, true,
                false, 2, 4f);
            SettlementOffensiveBehaviour.ObserveTarget(state, true, false, false,
                false, 2, 4f);

            Assert.That(state.Target, Is.Null);
            Assert.That(state.FailedAttempts, Is.EqualTo(2));
            Assert.That(state.Phase, Is.EqualTo(SettlementOffensivePhase.Search));
        }

        [Test]
        public void ClosestQualifiedTarget_HandlesZeroDefendersAndRetargetsByDistance()
        {
            object weak = new();
            object zero = new();
            object closestQualified = new();
            var candidates = new[]
            {
                new RaidTargetCandidate<object>(weak, true, 1f, 100f),
                new RaidTargetCandidate<object>(zero, true, 3f, 0f),
                new RaidTargetCandidate<object>(closestQualified, true, 2f, 10f)
            };

            object? selected = SettlementOffensiveBehaviour.ChooseClosestTarget(candidates, 20f, 1.5f);

            Assert.That(selected, Is.SameAs(closestQualified));
            Assert.That(SettlementOffensiveBehaviour.MeetsStrengthRatio(0f, 0f, 100f), Is.True);
        }

        [Test]
        public void OperationExpiryAndCooldown_ReturnNotApplicableForEveryCooldownTick()
        {
            var state = new SettlementOffensiveBehaviourState
            {
                Phase = SettlementOffensivePhase.Search,
                OperationWindowDays = 1f / 24f
            };

            Assert.That(SettlementOffensiveBehaviour.AdvanceOperationClock(state, 2f), Is.True);
            Assert.That(state.Phase, Is.EqualTo(SettlementOffensivePhase.Cooldown));
            Assert.That(SettlementOffensiveBehaviour.TickCooldown(state), Is.True);
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

        private static SettlementOffensiveBehaviourState BeginEpisode(out int rolls)
        {
            int rollCount = 0;
            var state = new SettlementOffensiveBehaviourState();
            var settings = new SettlementOffensiveSettings(2f, 2f, 3f, 3f,
                1f, 2, 4f);
            SettlementOffensiveBehaviour.BeginEpisode(state, settings, Roll);
            rolls = rollCount;
            return state;

            float Roll(float minimum, float maximum)
            {
                rollCount++;
                return minimum;
            }
        }

        private static RoamingPartyStrength Strength(float distance, float partyStrength,
            object? armyIdentity = null, bool isSelf = false, bool isActive = true,
            bool isHostile = true)
        {
            return new RoamingPartyStrength(isSelf, isActive, isHostile, armyIdentity,
                distance, partyStrength);
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
