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
                    rule.IntervalInDays, rule.LordsPerWave, rule.MaxWaves, rule.MenPerWave,
                    rule.MenPerTownHeld)).ToList();
            return new InvasionData(config.FirstConquestMustBeSupplyBase, rules, config.MaxCapitalGarrison,
                config.CapitalGarrisonPerTownHeld, config.SupplyLine, config.MinimumCapitalGarrison,
                config.MaxLords,
                config.LordsPerTownHeld, config.MinimumFiefGarrison,
                config.Conquest == null
                    ? null
                    : new InvasionConquestData(config.Conquest.ThreatPerFiefHeld,
                        config.Conquest.OpenFrontIntervalInDays, config.Conquest.TargetSiegePreference,
                        config.Conquest.ReconsiderTargetIntervalInDays,
                        config.Conquest.GarrisonArmy == null
                            ? null
                            : new InvasionGarrisonArmyData(config.Conquest.GarrisonArmy.IntervalInDays,
                                config.Conquest.GarrisonArmy.GarrisonShare),
                        config.Conquest.MaxKingdoms),
                config.LordPartySizeBonus,
                string.IsNullOrWhiteSpace(config.FollowsInvasion) ? null : config.FollowsInvasion!.Trim(),
                config.RelocateCapitalOnLoss);
        }
    }
}
