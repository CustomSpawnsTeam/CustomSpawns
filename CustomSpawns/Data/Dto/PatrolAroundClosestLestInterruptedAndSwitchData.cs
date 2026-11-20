using System.Collections.Generic;
using System.Collections.ObjectModel;
using CustomSpawns.Data.Adapter;

namespace CustomSpawns.Data.Dto
{
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
