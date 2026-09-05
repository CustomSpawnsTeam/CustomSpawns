using System;
using System.Collections.Generic;
using System.Linq;
using CustomSpawns.Data.Dto;

namespace CustomSpawns.Data.Adapter
{
    internal static class InvasionRuntimeConfigurationValidator
    {
        public static void ValidateReferences(InvasionData invasion, string spawnPartyTemplateId, bool spawnClanIsBandit,
            bool reinforcementTemplateExists)
        {
            if (spawnClanIsBandit)
            {
                throw new ArgumentException("Invasion spawn '" + spawnPartyTemplateId + "' cannot use a bandit clan.");
            }
            if (!reinforcementTemplateExists)
            {
                throw new ArgumentException("Invasion spawn '" + spawnPartyTemplateId + "' references unknown reinforcement party template '" +
                                            invasion.ReinforcementPartyTemplateId + "'.");
            }
        }

        public static void ValidateEffectiveSiegeTemplate(string spawnPartyTemplateId, string? templateId,
            bool templateExists, IReadOnlyList<PriorityPartyBehaviourData> behaviours)
        {
            if (string.IsNullOrWhiteSpace(templateId))
            {
                throw new ArgumentException("Invasion spawn '" + spawnPartyTemplateId + "' requires PriorityPartyBehaviourTemplateId.");
            }
            if (!templateExists)
            {
                throw new ArgumentException("Invasion spawn '" + spawnPartyTemplateId + "' references unknown priority party behaviour template '" +
                                            templateId + "'.");
            }
            if (!behaviours.OfType<BesiegeClosestSettlementData>().Any(behaviour =>
                    behaviour.TargetTypes.Contains(SpawnSettlementType.Town)))
            {
                throw new ArgumentException("Invasion spawn '" + spawnPartyTemplateId + "' priority party behaviour template '" +
                                            templateId + "' must contain BesiegeClosestSettlement targeting Town.");
            }
        }
    }

}
