using System.Collections.Generic;
using System.Collections.ObjectModel;
using CustomSpawns.Data.Adapter;

namespace CustomSpawns.Data.Dto
{
    public sealed class BesiegeClosestSettlementData : SettlementOffensiveData
    {
        public IReadOnlyList<SpawnSettlementType> TargetTypes { get; }

        public BesiegeClosestSettlementData(IReadOnlyList<SpawnSettlementType> targetTypes,
            float minimumAttackerToTargetRatio, float minimumAttackerToRoamingHostilesRatio,
            float minimumRequiredClearTimeInDays, float maximumRequiredClearTimeInDays,
            float skipWhenTargetStrengthRatio, float minimumOperationWindowInDays,
            float maximumOperationWindowInDays, float cooldownInDays)
            : base(minimumAttackerToTargetRatio, minimumAttackerToRoamingHostilesRatio,
                minimumRequiredClearTimeInDays, maximumRequiredClearTimeInDays,
                skipWhenTargetStrengthRatio, minimumOperationWindowInDays,
                maximumOperationWindowInDays, cooldownInDays)
        {
            TargetTypes = new ReadOnlyCollection<SpawnSettlementType>(
                new List<SpawnSettlementType>(targetTypes));
        }
    }
}
