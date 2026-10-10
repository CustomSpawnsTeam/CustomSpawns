using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using CustomSpawns.Invasions;

namespace CustomSpawns.Data.Model
{
    public sealed class InvasionConfig
    {
        [XmlElement("RequiresFirstCoastalConquest")]
        public bool RequiresFirstCoastalConquest { get; set; }

        // Where the invasion's supplies may come from: Coastal, Any or None. Left out, supplies come by
        // sea, which is what every invasion did before there was a choice.
        [XmlElement("SupplyLine")]
        public string? SupplyLine { get; set; }

        [XmlElement("Supplies")]
        public InvasionSuppliesConfig Supplies { get; set; } = new();

        // The strength the whole invasion may field at once, shared by all of its reinforcement rules.
        // Left out, the invasion is uncapped.
        [XmlElement("MaxFactionStrength")]
        public float MaxFactionStrength { get; set; }

        // Raises that ceiling for every town the invasion's kingdom currently holds, allies' towns included.
        [XmlElement("StrengthPerTownHeld")]
        public float StrengthPerTownHeld { get; set; }

        [XmlElement("Reinforcements")]
        public InvasionReinforcementsConfig Reinforcements { get; set; } = new();
    }

    public sealed class InvasionSuppliesConfig
    {
        [XmlElement("FoodStockFloor")]
        public float FoodStockFloor { get; set; }

        [XmlElement("DailyFoodBonus")]
        public float DailyFoodBonus { get; set; }
    }

    public sealed class InvasionReinforcementsConfig
    {
        [XmlElement("Rule")]
        public List<InvasionReinforcementRuleConfig> Rules { get; set; } = new();
    }

    public sealed class InvasionReinforcementRuleConfig
    {
        // Names the rule's own clock and its own concurrent count, so editing or reordering the
        // configuration does not move a running schedule onto another rule.
        [XmlElement("Id")]
        public string? Id { get; set; }

        [XmlElement("PartyTemplateId")]
        public string? PartyTemplateId { get; set; }

        [XmlElement("IntervalInDays")]
        public float IntervalInDays { get; set; } = 7f;

        [XmlElement("MaxConcurrentParties")]
        public int MaxConcurrentParties { get; set; } = 1;
    }

    internal static class InvasionConfigValidator
    {
        public static void Validate(InvasionConfig config)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }
            if (config.Supplies == null || config.Reinforcements?.Rules == null)
            {
                throw new ArgumentException("Invasion configuration groups cannot be null.");
            }

            if (InvasionSupplyLines.Find(config.SupplyLine) == null)
            {
                throw new ArgumentException("Invasion SupplyLine '" + config.SupplyLine + "' must be one of " +
                                           InvasionSupplyLines.KnownNames + ".");
            }

            ValidateNonNegativeFinite("Invasion Supplies FoodStockFloor", config.Supplies.FoodStockFloor);
            ValidateNonNegativeFinite("Invasion Supplies DailyFoodBonus", config.Supplies.DailyFoodBonus);
            ValidateNonNegativeFinite("Invasion MaxFactionStrength", config.MaxFactionStrength);
            ValidateNonNegativeFinite("Invasion StrengthPerTownHeld", config.StrengthPerTownHeld);
            if (config.Reinforcements.Rules.Count == 0)
            {
                throw new ArgumentException("Invasion Reinforcements requires at least one Rule.");
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);

            foreach (InvasionReinforcementRuleConfig rule in config.Reinforcements.Rules)
            {
                ValidateRule(rule, ids);
            }
        }

        private static void ValidateRule(InvasionReinforcementRuleConfig rule, HashSet<string> ids)
        {
            if (string.IsNullOrWhiteSpace(rule.Id))
            {
                throw new ArgumentException("Invasion Reinforcements Rule Id is required.");
            }
            if (!ids.Add(rule.Id!))
            {
                throw new ArgumentException("Invasion Reinforcements Rule Id '" + rule.Id + "' is used twice.");
            }
            if (string.IsNullOrWhiteSpace(rule.PartyTemplateId))
            {
                throw new ArgumentException("Invasion Reinforcements Rule '" + rule.Id +
                                           "' PartyTemplateId is required.");
            }
            if (!IsFinite(rule.IntervalInDays) || rule.IntervalInDays <= 0f)
            {
                throw new ArgumentException("Invasion Reinforcements Rule '" + rule.Id +
                                           "' IntervalInDays must be finite and greater than zero.");
            }
            if (rule.MaxConcurrentParties <= 0)
            {
                throw new ArgumentException("Invasion Reinforcements Rule '" + rule.Id +
                                           "' MaxConcurrentParties must be greater than zero.");
            }
        }

        private static void ValidateNonNegativeFinite(string name, float value)
        {
            if (!IsFinite(value))
            {
                throw new ArgumentException(name + " must be finite.");
            }
            if (value < 0f)
            {
                throw new ArgumentException(name + " cannot be negative.");
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
