using System;
using System.Collections.Generic;
using System.Linq;
using CustomSpawns.AI.Model;

namespace CustomSpawns.AI
{
    internal static class SettlementOffensiveBehaviour
    {
        internal const float DaysPerHour = 1f / 24f;

        internal static bool TickCooldown(SettlementOffensiveBehaviourState state)
        {
            if (state.Phase != SettlementOffensivePhase.Cooldown)
            {
                return false;
            }

            state.CooldownRemainingDays = Math.Max(0f, state.CooldownRemainingDays - DaysPerHour);
            if (state.CooldownRemainingDays <= 0f)
            {
                state.Phase = SettlementOffensivePhase.Idle;
            }
            return true;
        }

        internal static void BeginEpisode(SettlementOffensiveBehaviourState state,
            SettlementOffensiveSettings settings, Func<float, float, float> roll)
        {
            if (state.Phase != SettlementOffensivePhase.Idle)
            {
                return;
            }

            state.Target = null;
            state.TargetMapFaction = null;
            state.Phase = SettlementOffensivePhase.Search;
            state.RequiredClearReconDays = roll(settings.MinimumRequiredClearTimeInDays,
                settings.MaximumRequiredClearTimeInDays);
            state.AccumulatedClearReconDays = 0f;
            state.OperationWindowDays = roll(settings.MinimumOperationWindowInDays,
                settings.MaximumOperationWindowInDays);
            state.ElapsedOperationDays = 0f;
            state.RecoveryGraceDays = settings.GracePeriodInDays;
            state.RecoveryElapsedDays = 0f;
            state.FailedAttempts = 0;
            state.CooldownRemainingDays = 0f;
        }

        internal static void AssignTarget(SettlementOffensiveBehaviourState state, bool skipRecon)
        {
            state.AccumulatedClearReconDays = 0f;
            state.Phase = skipRecon ? SettlementOffensivePhase.Approach : SettlementOffensivePhase.Recon;
        }

        internal static ActiveTargetObservation ObserveTarget(SettlementOffensiveBehaviourState state,
            bool isValid, bool isComplete, bool isMatchingOperationActive,
            bool hasNativeContinuation, int maximumFailedAttempts, float cooldownInDays)
        {
            bool wasActive = state.Phase == SettlementOffensivePhase.Active;
            if (wasActive && isComplete)
            {
                EnterCooldown(state, cooldownInDays);
                return ActiveTargetObservation.EnteredCooldown;
            }

            if (!isValid)
            {
                ClearTarget(state);
                return ActiveTargetObservation.Cleared;
            }

            if (isMatchingOperationActive)
            {
                state.Phase = SettlementOffensivePhase.Active;
                state.RecoveryElapsedDays = 0f;
                return ActiveTargetObservation.Unchanged;
            }

            if (!wasActive)
            {
                return ActiveTargetObservation.Unchanged;
            }

            if (hasNativeContinuation)
            {
                state.Phase = SettlementOffensivePhase.Approach;
                return ActiveTargetObservation.Unchanged;
            }

            if (state.FailedAttempts >= maximumFailedAttempts)
            {
                ClearTarget(state);
                return ActiveTargetObservation.Cleared;
            }

            state.FailedAttempts++;
            state.RecoveryElapsedDays = 0f;
            state.Phase = SettlementOffensivePhase.Recovery;
            return ActiveTargetObservation.Unchanged;
        }

        internal static bool TickRecovery(SettlementOffensiveBehaviourState state)
        {
            if (state.Phase != SettlementOffensivePhase.Recovery)
            {
                return false;
            }

            state.RecoveryElapsedDays += DaysPerHour;
            if (state.RecoveryElapsedDays < state.RecoveryGraceDays)
            {
                return false;
            }

            ClearTarget(state);
            return true;
        }

        internal static void UpdateRecon(SettlementOffensiveBehaviourState state,
            bool safetyRatioPasses, bool skipRecon)
        {
            if (state.Phase != SettlementOffensivePhase.Recon)
            {
                return;
            }

            if (!safetyRatioPasses)
            {
                state.AccumulatedClearReconDays = 0f;
                return;
            }

            if (skipRecon)
            {
                state.Phase = SettlementOffensivePhase.Approach;
                return;
            }

            state.AccumulatedClearReconDays += DaysPerHour;
            if (state.AccumulatedClearReconDays >= state.RequiredClearReconDays)
            {
                state.Phase = SettlementOffensivePhase.Approach;
            }
        }

        internal static bool AdvanceOperationClock(SettlementOffensiveBehaviourState state,
            float cooldownInDays)
        {
            if (state.Phase == SettlementOffensivePhase.Active ||
                state.Phase == SettlementOffensivePhase.Recovery ||
                state.Phase == SettlementOffensivePhase.Cooldown)
            {
                return false;
            }

            state.ElapsedOperationDays += DaysPerHour;
            if (state.ElapsedOperationDays < state.OperationWindowDays)
            {
                return false;
            }

            EnterCooldown(state, cooldownInDays);
            return true;
        }

        internal static void ReturnApproachToRecon(SettlementOffensiveBehaviourState state)
        {
            if (state.Phase == SettlementOffensivePhase.Approach)
            {
                state.Phase = SettlementOffensivePhase.Recon;
                state.AccumulatedClearReconDays = 0f;
            }
        }

        internal static bool IsWithinSeeingRange(float distance, float observerSeeingRange)
        {
            return distance <= observerSeeingRange;
        }

        internal static float SumVisibleHostileStrength(IEnumerable<RoamingPartyStrength> parties,
            float observerSeeingRange)
        {
            return parties.Where(p => !p.IsSelf && p.IsActive && p.IsHostile &&
                                      IsWithinSeeingRange(p.Distance, observerSeeingRange))
                .GroupBy(p => p.ArmyIdentity)
                .Sum(army => army.Sum(p => p.PartyStrength));
        }

        internal static bool MeetsStrengthRatio(float attackerStrength, float defenderStrength,
            float minimumRatio)
        {
            return defenderStrength <= 0f || attackerStrength / defenderStrength >= minimumRatio;
        }

        internal static T? ChooseClosestTarget<T>(IEnumerable<RaidTargetCandidate<T>> candidates,
            float attackerStrength, float minimumRatio) where T : class
        {
            return candidates.Where(c => c.IsValid &&
                                         MeetsStrengthRatio(attackerStrength, c.DefenderStrength, minimumRatio))
                .OrderBy(c => c.Distance)
                .Select(c => c.Target)
                .FirstOrDefault();
        }

        internal static void ClearTarget(SettlementOffensiveBehaviourState state)
        {
            state.Target = null;
            state.TargetMapFaction = null;
            state.Phase = SettlementOffensivePhase.Search;
            state.AccumulatedClearReconDays = 0f;
            state.RecoveryElapsedDays = 0f;
        }

        private static void EnterCooldown(SettlementOffensiveBehaviourState state, float cooldownInDays)
        {
            state.Target = null;
            state.TargetMapFaction = null;
            state.Phase = SettlementOffensivePhase.Cooldown;
            state.CooldownRemainingDays = cooldownInDays;
            state.RecoveryElapsedDays = 0f;
        }
    }
}
