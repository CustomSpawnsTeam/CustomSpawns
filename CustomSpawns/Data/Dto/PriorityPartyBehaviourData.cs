using System.Collections.Generic;
using System.Collections.ObjectModel;
using CustomSpawns.Data.Adapter;

namespace CustomSpawns.Data.Dto
{
    public abstract class PriorityPartyBehaviourData
    {
    }

    public sealed class BesiegeClosestSettlementData : PriorityPartyBehaviourData
    {
        public IReadOnlyList<SpawnSettlementType> TargetTypes { get; }
        public float MinimumAttackerToTargetRatio { get; }
        public float MinimumAttackerToRoamingHostilesRatio { get; }
        public float MinimumRequiredClearTimeInDays { get; }
        public float MaximumRequiredClearTimeInDays { get; }
        public float SkipWhenTargetStrengthRatio { get; }
        public float MinimumOperationWindowInDays { get; }
        public float MaximumOperationWindowInDays { get; }
        public float GracePeriodInDays { get; }
        public int MaximumFailedAttempts { get; }
        public float CooldownInDays { get; }

        public BesiegeClosestSettlementData(IReadOnlyList<SpawnSettlementType> targetTypes,
            float minimumAttackerToTargetRatio, float minimumAttackerToRoamingHostilesRatio,
            float minimumRequiredClearTimeInDays, float maximumRequiredClearTimeInDays,
            float skipWhenTargetStrengthRatio, float minimumOperationWindowInDays,
            float maximumOperationWindowInDays, float gracePeriodInDays, int maximumFailedAttempts,
            float cooldownInDays)
        {
            TargetTypes = new ReadOnlyCollection<SpawnSettlementType>(
                new List<SpawnSettlementType>(targetTypes));
            MinimumAttackerToTargetRatio = minimumAttackerToTargetRatio;
            MinimumAttackerToRoamingHostilesRatio = minimumAttackerToRoamingHostilesRatio;
            MinimumRequiredClearTimeInDays = minimumRequiredClearTimeInDays;
            MaximumRequiredClearTimeInDays = maximumRequiredClearTimeInDays;
            SkipWhenTargetStrengthRatio = skipWhenTargetStrengthRatio;
            MinimumOperationWindowInDays = minimumOperationWindowInDays;
            MaximumOperationWindowInDays = maximumOperationWindowInDays;
            GracePeriodInDays = gracePeriodInDays;
            MaximumFailedAttempts = maximumFailedAttempts;
            CooldownInDays = cooldownInDays;
        }
    }

    public sealed class RaidClosestVillageData : PriorityPartyBehaviourData
    {
        public float MinimumAttackerToTargetRatio { get; }
        public float MinimumAttackerToRoamingHostilesRatio { get; }
        public float MinimumRequiredClearTimeInDays { get; }
        public float MaximumRequiredClearTimeInDays { get; }
        public float SkipWhenTargetStrengthRatio { get; }
        public float MinimumOperationWindowInDays { get; }
        public float MaximumOperationWindowInDays { get; }
        public float GracePeriodInDays { get; }
        public int MaximumFailedAttempts { get; }
        public float CooldownInDays { get; }

        public RaidClosestVillageData(float minimumAttackerToTargetRatio,
            float minimumAttackerToRoamingHostilesRatio, float minimumRequiredClearTimeInDays,
            float maximumRequiredClearTimeInDays, float skipWhenTargetStrengthRatio,
            float minimumOperationWindowInDays, float maximumOperationWindowInDays,
            float gracePeriodInDays, int maximumFailedAttempts, float cooldownInDays)
        {
            MinimumAttackerToTargetRatio = minimumAttackerToTargetRatio;
            MinimumAttackerToRoamingHostilesRatio = minimumAttackerToRoamingHostilesRatio;
            MinimumRequiredClearTimeInDays = minimumRequiredClearTimeInDays;
            MaximumRequiredClearTimeInDays = maximumRequiredClearTimeInDays;
            SkipWhenTargetStrengthRatio = skipWhenTargetStrengthRatio;
            MinimumOperationWindowInDays = minimumOperationWindowInDays;
            MaximumOperationWindowInDays = maximumOperationWindowInDays;
            GracePeriodInDays = gracePeriodInDays;
            MaximumFailedAttempts = maximumFailedAttempts;
            CooldownInDays = cooldownInDays;
        }
    }

    public sealed class PatrolAroundClosestLestInterruptedAndSwitchData : PriorityPartyBehaviourData
    {
        public float MinStableDays { get; }
        public float MaxStableDays { get; }
        public IReadOnlyList<SpawnSettlementType> PreferredSettlementTypes { get; }

        public PatrolAroundClosestLestInterruptedAndSwitchData(float minStableDays, float maxStableDays,
            IReadOnlyList<SpawnSettlementType> preferredSettlementTypes)
        {
            MinStableDays = minStableDays;
            MaxStableDays = maxStableDays;
            PreferredSettlementTypes = new ReadOnlyCollection<SpawnSettlementType>(
                new List<SpawnSettlementType>(preferredSettlementTypes));
        }
    }
}
