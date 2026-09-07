namespace CustomSpawns.Data.Dto
{
    public sealed class RaidClosestVillageData : SettlementOffensiveData
    {
        public RaidClosestVillageData(float minimumAttackerToTargetRatio,
            float minimumAttackerToRoamingHostilesRatio, float minimumRequiredClearTimeInDays,
            float maximumRequiredClearTimeInDays, float skipWhenTargetStrengthRatio,
            float minimumOperationWindowInDays, float maximumOperationWindowInDays,
            float cooldownInDays)
            : base(minimumAttackerToTargetRatio, minimumAttackerToRoamingHostilesRatio,
                minimumRequiredClearTimeInDays, maximumRequiredClearTimeInDays,
                skipWhenTargetStrengthRatio, minimumOperationWindowInDays,
                maximumOperationWindowInDays, cooldownInDays)
        {
        }
    }
}
