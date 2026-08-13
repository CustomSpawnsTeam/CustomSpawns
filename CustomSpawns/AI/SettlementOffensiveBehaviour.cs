using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.SaveSystem;

namespace CustomSpawns.AI
{
    internal enum SettlementOffensivePhase
    {
        Idle,
        Search,
        Recon,
        Approach,
        Active,
        Recovery,
        Cooldown
    }

    internal sealed class SettlementOffensiveBehaviourState
    {
        [SaveableField(1)]
        private Settlement? _target;

        [SaveableField(2)]
        private SettlementOffensivePhase _phase;

        [SaveableField(3)]
        private float _requiredClearReconDays;

        [SaveableField(4)]
        private float _accumulatedClearReconDays;

        [SaveableField(5)]
        private float _operationWindowDays;

        [SaveableField(6)]
        private float _elapsedOperationDays;

        [SaveableField(7)]
        private float _recoveryGraceDays;

        [SaveableField(8)]
        private float _recoveryElapsedDays;

        [SaveableField(9)]
        private int _failedAttempts;

        [SaveableField(10)]
        private float _cooldownRemainingDays;

        [SaveableField(11)]
        private IFaction? _targetMapFaction;

        public Settlement? Target { get => _target; set => _target = value; }
        public SettlementOffensivePhase Phase { get => _phase; set => _phase = value; }
        public float RequiredClearReconDays { get => _requiredClearReconDays; set => _requiredClearReconDays = value; }
        public float AccumulatedClearReconDays { get => _accumulatedClearReconDays; set => _accumulatedClearReconDays = value; }
        public float OperationWindowDays { get => _operationWindowDays; set => _operationWindowDays = value; }
        public float ElapsedOperationDays { get => _elapsedOperationDays; set => _elapsedOperationDays = value; }
        public float RecoveryGraceDays { get => _recoveryGraceDays; set => _recoveryGraceDays = value; }
        public float RecoveryElapsedDays { get => _recoveryElapsedDays; set => _recoveryElapsedDays = value; }
        public int FailedAttempts { get => _failedAttempts; set => _failedAttempts = value; }
        public float CooldownRemainingDays { get => _cooldownRemainingDays; set => _cooldownRemainingDays = value; }
        public IFaction? TargetMapFaction { get => _targetMapFaction; set => _targetMapFaction = value; }
    }

    internal readonly struct SettlementOffensiveSettings
    {
        public float MinimumRequiredClearTimeInDays { get; }
        public float MaximumRequiredClearTimeInDays { get; }
        public float MinimumOperationWindowInDays { get; }
        public float MaximumOperationWindowInDays { get; }
        public float GracePeriodInDays { get; }
        public int MaximumFailedAttempts { get; }
        public float CooldownInDays { get; }

        public SettlementOffensiveSettings(float minimumRequiredClearTimeInDays,
            float maximumRequiredClearTimeInDays, float minimumOperationWindowInDays,
            float maximumOperationWindowInDays, float gracePeriodInDays,
            int maximumFailedAttempts, float cooldownInDays)
        {
            MinimumRequiredClearTimeInDays = minimumRequiredClearTimeInDays;
            MaximumRequiredClearTimeInDays = maximumRequiredClearTimeInDays;
            MinimumOperationWindowInDays = minimumOperationWindowInDays;
            MaximumOperationWindowInDays = maximumOperationWindowInDays;
            GracePeriodInDays = gracePeriodInDays;
            MaximumFailedAttempts = maximumFailedAttempts;
            CooldownInDays = cooldownInDays;
        }
    }

    internal enum ActiveTargetObservation
    {
        Unchanged,
        Cleared,
        EnteredCooldown
    }

    internal readonly struct RoamingPartyStrength
    {
        public bool IsSelf { get; }
        public bool IsActive { get; }
        public bool IsHostile { get; }
        public object? ArmyIdentity { get; }
        public float Distance { get; }
        public float PartyStrength { get; }

        public RoamingPartyStrength(bool isSelf, bool isActive, bool isHostile,
            object? armyIdentity, float distance, float partyStrength)
        {
            IsSelf = isSelf;
            IsActive = isActive;
            IsHostile = isHostile;
            ArmyIdentity = armyIdentity;
            Distance = distance;
            PartyStrength = partyStrength;
        }
    }

    internal readonly struct RaidTargetCandidate<T>
    {
        public T Target { get; }
        public bool IsValid { get; }
        public float Distance { get; }
        public float DefenderStrength { get; }

        public RaidTargetCandidate(T target, bool isValid, float distance, float defenderStrength)
        {
            Target = target;
            IsValid = isValid;
            Distance = distance;
            DefenderStrength = defenderStrength;
        }
    }

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

        private static void ClearTarget(SettlementOffensiveBehaviourState state)
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
