using System.Collections.Generic;
using System.Linq;
using CustomSpawns.Data.Dto;
using CustomSpawns.Data.Model;

namespace CustomSpawns.Data.Adapter
{
    public static class InvasionDtoAdapter
    {
        public static InvasionData Adapt(InvasionConfig config)
        {
            InvasionConfigValidator.Validate(config);
            List<InvasionReinforcementRuleData> rules = config.Reinforcements.Rules
                .Select(rule => new InvasionReinforcementRuleData(rule.Id!, rule.PartyTemplateId!,
                    rule.IntervalInDays, rule.LordsPerWave, rule.MaxWaves, rule.MaxMenPerWave,
                    rule.MenPerTownHeld)).ToList();
            return new InvasionData(config.RequiresFirstCoastalConquest, rules, config.MaxBankedTroops,
                config.BankedTroopsPerTownHeld, config.SupplyLine, config.MinimumCapitalGarrison,
                config.Supplies.FoodPerConvoy, config.Supplies.IntervalInDays, config.MaxLords,
                config.LordsPerTownHeld, config.MinimumFiefGarrison,
                config.Conquest == null
                    ? null
                    : new InvasionConquestData(config.Conquest.ThreatPerFiefHeld,
                        config.Conquest.OpenFrontIntervalInDays, config.Conquest.TargetSiegePreference),
                config.LordPartySizeBonus);
        }
    }
}
