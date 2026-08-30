namespace CustomSpawns.AI.Model
{
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
}
