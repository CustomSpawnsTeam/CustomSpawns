using System.Collections.Generic;
using CustomSpawns.Data.Dto;
using CustomSpawns.Data.Model;

namespace CustomSpawns.Data.Adapter
{
    public sealed class PatrolAroundClosestLestInterruptedAndSwitchConfigAdapter
        : PriorityPartyBehaviourConfigAdapter<PatrolAroundClosestLestInterruptedAndSwitchConfig>
    {
        protected override PriorityPartyBehaviourData Adapt(
            PatrolAroundClosestLestInterruptedAndSwitchConfig config)
        {
            FloatRangeConfig stableTime = PriorityPartyBehaviourConfigDefaults
                .ApplyRangeDefaults(config.StableTimeInDays, 0f, 10f);
            PriorityPartyBehaviourConfigValidator.ValidateRange("patrol stable time",
                stableTime.Minimum, stableTime.Maximum);
            List<SpawnSettlementType> targetTypes =
                config.TargetTypesSpecified || config.TargetTypes?.Count > 0
                    ? config.TargetTypes ?? new List<SpawnSettlementType>()
                    : AllSettlementTypes();

            return new PatrolAroundClosestLestInterruptedAndSwitchData(
                stableTime.Minimum, stableTime.Maximum, targetTypes);
        }

        private static List<SpawnSettlementType> AllSettlementTypes()
        {
            return new List<SpawnSettlementType>
            {
                SpawnSettlementType.Village,
                SpawnSettlementType.Town,
                SpawnSettlementType.Castle
            };
        }
    }
}
