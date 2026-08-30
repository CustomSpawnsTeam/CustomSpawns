using System;
using CustomSpawns.AI.Model;

namespace CustomSpawns.AI
{
    /**
     * The phases one party's settlement offensive moves through. It owns the state it advances, so callers
     * describe what they observed rather than handing the state back in on every call.
     */
    internal sealed class SettlementOffensiveStateMachine
    {
        internal const float DaysPerHour = 1f / 24f;

        private readonly SettlementOffensiveBehaviourState _state;

        internal SettlementOffensiveStateMachine(SettlementOffensiveBehaviourState state)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
        }

        internal SettlementOffensiveBehaviourState State => _state;

        internal bool TickCooldown()
        {
            if (_state.Phase != SettlementOffensivePhase.Cooldown)
            {
                return false;
            }

            _state.CooldownRemainingDays = Math.Max(0f, _state.CooldownRemainingDays - DaysPerHour);
            if (_state.CooldownRemainingDays <= 0f)
            {
                _state.Phase = SettlementOffensivePhase.Idle;
            }
            return true;
        }

        internal void BeginEpisode(SettlementOffensiveSettings settings, Func<float, float, float> roll)
        {
            if (_state.Phase != SettlementOffensivePhase.Idle)
            {
                return;
            }

            _state.Target = null;
            _state.TargetMapFaction = null;
            _state.Phase = SettlementOffensivePhase.Search;
            _state.RequiredClearReconDays = roll(settings.MinimumRequiredClearTimeInDays,
                settings.MaximumRequiredClearTimeInDays);
            _state.AccumulatedClearReconDays = 0f;
            _state.OperationWindowDays = roll(settings.MinimumOperationWindowInDays,
                settings.MaximumOperationWindowInDays);
            _state.ElapsedOperationDays = 0f;
            _state.RecoveryGraceDays = settings.GracePeriodInDays;
            _state.RecoveryElapsedDays = 0f;
            _state.FailedAttempts = 0;
            _state.CooldownRemainingDays = 0f;
        }

        internal void AssignTarget(bool skipRecon)
        {
            _state.AccumulatedClearReconDays = 0f;
            _state.Phase = skipRecon ? SettlementOffensivePhase.Approach : SettlementOffensivePhase.Recon;
        }

        internal ActiveTargetObservation ObserveTarget(bool isValid, bool isComplete,
            bool isMatchingOperationActive, bool hasNativeContinuation, int maximumFailedAttempts,
            float cooldownInDays)
        {
            bool wasActive = _state.Phase == SettlementOffensivePhase.Active;
            if (wasActive && isComplete)
            {
                EnterCooldown(cooldownInDays);
                return ActiveTargetObservation.EnteredCooldown;
            }

            if (!isValid)
            {
                ClearTarget();
                return ActiveTargetObservation.Cleared;
            }

            if (isMatchingOperationActive)
            {
                _state.Phase = SettlementOffensivePhase.Active;
                _state.RecoveryElapsedDays = 0f;
                return ActiveTargetObservation.Unchanged;
            }

            if (!wasActive)
            {
                return ActiveTargetObservation.Unchanged;
            }

            if (hasNativeContinuation)
            {
                _state.Phase = SettlementOffensivePhase.Approach;
                return ActiveTargetObservation.Unchanged;
            }

            if (_state.FailedAttempts >= maximumFailedAttempts)
            {
                ClearTarget();
                return ActiveTargetObservation.Cleared;
            }

            _state.FailedAttempts++;
            _state.RecoveryElapsedDays = 0f;
            _state.Phase = SettlementOffensivePhase.Recovery;
            return ActiveTargetObservation.Unchanged;
        }

        internal bool TickRecovery()
        {
            if (_state.Phase != SettlementOffensivePhase.Recovery)
            {
                return false;
            }

            _state.RecoveryElapsedDays += DaysPerHour;
            if (_state.RecoveryElapsedDays < _state.RecoveryGraceDays)
            {
                return false;
            }

            ClearTarget();
            return true;
        }

        internal void UpdateRecon(bool safetyRatioPasses, bool skipRecon)
        {
            if (_state.Phase != SettlementOffensivePhase.Recon)
            {
                return;
            }

            if (!safetyRatioPasses)
            {
                _state.AccumulatedClearReconDays = 0f;
                return;
            }

            if (skipRecon)
            {
                _state.Phase = SettlementOffensivePhase.Approach;
                return;
            }

            _state.AccumulatedClearReconDays += DaysPerHour;
            if (_state.AccumulatedClearReconDays >= _state.RequiredClearReconDays)
            {
                _state.Phase = SettlementOffensivePhase.Approach;
            }
        }

        internal bool AdvanceOperationClock(float cooldownInDays)
        {
            if (_state.Phase == SettlementOffensivePhase.Active ||
                _state.Phase == SettlementOffensivePhase.Recovery ||
                _state.Phase == SettlementOffensivePhase.Cooldown)
            {
                return false;
            }

            _state.ElapsedOperationDays += DaysPerHour;
            if (_state.ElapsedOperationDays < _state.OperationWindowDays)
            {
                return false;
            }

            EnterCooldown(cooldownInDays);
            return true;
        }

        internal void ReturnApproachToRecon()
        {
            if (_state.Phase == SettlementOffensivePhase.Approach)
            {
                _state.Phase = SettlementOffensivePhase.Recon;
                _state.AccumulatedClearReconDays = 0f;
            }
        }

        internal void ClearTarget()
        {
            _state.Target = null;
            _state.TargetMapFaction = null;
            _state.Phase = SettlementOffensivePhase.Search;
            _state.AccumulatedClearReconDays = 0f;
            _state.RecoveryElapsedDays = 0f;
        }

        private void EnterCooldown(float cooldownInDays)
        {
            _state.Target = null;
            _state.TargetMapFaction = null;
            _state.Phase = SettlementOffensivePhase.Cooldown;
            _state.CooldownRemainingDays = cooldownInDays;
            _state.RecoveryElapsedDays = 0f;
        }
    }
}
