using System;
using System.Collections.Generic;
using System.Linq;
using CustomSpawns.Data.Dto;
using CustomSpawns.Data.Model;

namespace CustomSpawns.Data.Adapter
{
    internal sealed class BesiegeClosestSettlementConfigAdapter
        : PriorityPartyBehaviourConfigAdapter<BesiegeClosestSettlementConfig>
    {
        private const string BehaviourName = "BesiegeClosestSettlement";

        protected override PriorityPartyBehaviourData Adapt(BesiegeClosestSettlementConfig config)
        {
            List<SpawnSettlementType> targetTypes = ResolveTargetTypes(config);
            StrengthAssessmentConfig strengthAssessment = PriorityPartyBehaviourConfigDefaults
                .ApplyStrengthDefaults(config.StrengthAssessment, 1.5f, 1.2f);
            ReconnaissanceConfig reconnaissance = PriorityPartyBehaviourConfigDefaults
                .ApplyReconnaissanceDefaults(config.Reconnaissance);
            FloatRangeConfig operationWindow = PriorityPartyBehaviourConfigDefaults
                .ApplyRangeDefaults(config.OperationWindowInDays, 5f, 10f);
            PriorityPartyBehaviourConfigValidator.ValidateOffensiveValues(BehaviourName, strengthAssessment,
                reconnaissance, operationWindow, config.CooldownInDays);

            return new BesiegeClosestSettlementData(targetTypes, strengthAssessment.MinimumAttackerToTargetRatio,
                strengthAssessment.MinimumAttackerToRoamingHostilesRatio,
                reconnaissance.RequiredClearTimeInDays.Minimum,
                reconnaissance.RequiredClearTimeInDays.Maximum,
                reconnaissance.SkipWhenTargetStrengthRatio, operationWindow.Minimum,
                operationWindow.Maximum, config.CooldownInDays);
        }

        private static List<SpawnSettlementType> ResolveTargetTypes(BesiegeClosestSettlementConfig config)
        {
            List<SpawnSettlementType> targetTypes =
                config.TargetTypesSpecified || config.TargetTypes?.Count > 0
                    ? config.TargetTypes ?? new List<SpawnSettlementType>()
                    : new List<SpawnSettlementType> { SpawnSettlementType.Town, SpawnSettlementType.Castle };
            if (targetTypes.Count == 0)
            {
                throw new ArgumentException(BehaviourName + " requires at least one target type.");
            }
            if (targetTypes.Any(type => type != SpawnSettlementType.Town && type != SpawnSettlementType.Castle))
            {
                throw new ArgumentException(BehaviourName + " only supports Town and Castle target types.");
            }

            return targetTypes;
        }
    }
}
