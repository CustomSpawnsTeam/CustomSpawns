using System.Collections.Generic;
using System.Linq;
using CustomSpawns.Data.Dto;
using CustomSpawns.Data.Model;

namespace CustomSpawns.Data.Adapter
{
    internal static class InvasionDtoAdapter
    {
        public static InvasionData Adapt(InvasionConfig config)
        {
            InvasionConfigValidator.Validate(config);
            List<InvasionReinforcementRuleData> rules = config.Reinforcements.Rules
                .Select(rule => new InvasionReinforcementRuleData(rule.Id!, rule.PartyTemplateId!,
                    rule.IntervalInDays, rule.LordsPerWave, rule.MaxWaves)).ToList();
            return new InvasionData(config.RequiresFirstCoastalConquest, rules, config.MaxFactionStrength,
                config.StrengthPerTownHeld, config.SupplyLine, config.MinimumCapitalGarrison,
                config.Supplies.FoodPerConvoy, config.Supplies.IntervalInDays);
        }
    }
}
