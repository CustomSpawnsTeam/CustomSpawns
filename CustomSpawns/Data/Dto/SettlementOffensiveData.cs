namespace CustomSpawns.Data.Dto
{
    public abstract class SettlementOffensiveData : PriorityPartyBehaviourData
    {
        public float MinimumAttackerToTargetRatio { get; }
        public float MinimumAttackerToRoamingHostilesRatio { get; }
        public float MinimumRequiredClearTimeInDays { get; }
        public float MaximumRequiredClearTimeInDays { get; }
        public float SkipWhenTargetStrengthRatio { get; }
        public float MinimumOperationWindowInDays { get; }
        public float MaximumOperationWindowInDays { get; }
        public float CooldownInDays { get; }

        protected SettlementOffensiveData(float minimumAttackerToTargetRatio,
            float minimumAttackerToRoamingHostilesRatio, float minimumRequiredClearTimeInDays,
            float maximumRequiredClearTimeInDays, float skipWhenTargetStrengthRatio,
            float minimumOperationWindowInDays, float maximumOperationWindowInDays,
            float cooldownInDays)
        {
            MinimumAttackerToTargetRatio = minimumAttackerToTargetRatio;
            MinimumAttackerToRoamingHostilesRatio = minimumAttackerToRoamingHostilesRatio;
            MinimumRequiredClearTimeInDays = minimumRequiredClearTimeInDays;
            MaximumRequiredClearTimeInDays = maximumRequiredClearTimeInDays;
            SkipWhenTargetStrengthRatio = skipWhenTargetStrengthRatio;
            MinimumOperationWindowInDays = minimumOperationWindowInDays;
            MaximumOperationWindowInDays = maximumOperationWindowInDays;
            CooldownInDays = cooldownInDays;
        }
    }
}
