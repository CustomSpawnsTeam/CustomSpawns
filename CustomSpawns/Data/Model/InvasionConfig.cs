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

        // The strength the whole invasion may field at once, shared by all of its reinforcement rules.
        // Left out, the invasion is uncapped.
        [XmlElement("MaxFactionStrength")]
        public float MaxFactionStrength { get; set; }

        // Raises that ceiling for every town the invasion's kingdom currently holds, allies' towns included.
        [XmlElement("StrengthPerTownHeld")]
        public float StrengthPerTownHeld { get; set; }

        // How many men stay standing in the invasion's capital: its lords draw that garrison down to this
        // but never below it.
        [XmlElement("MinimumCapitalGarrison")]
        public int MinimumCapitalGarrison { get; set; }

        // The food convoys the invasion runs into its capital, on their own cadence.
        [XmlElement("Supplies")]
        public InvasionSuppliesConfig Supplies { get; set; } = new();

        [XmlElement("Reinforcements")]
        public InvasionReinforcementsConfig Reinforcements { get; set; } = new();
    }

    public sealed class InvasionSuppliesConfig
    {
        // How much food each convoy unloads into the capital's market, for the town to live off as it does
        // off any other food in it. Left out, the invasion runs no convoys and its capital feeds itself.
        [XmlElement("FoodPerConvoy")]
        public int FoodPerConvoy { get; set; }

        // How often a convoy arrives.
        [XmlElement("IntervalInDays")]
        public float IntervalInDays { get; set; } = 7f;
    }

    public sealed class InvasionReinforcementsConfig
    {
        [XmlElement("Rule")]
        public List<InvasionReinforcementRuleConfig> Rules { get; set; } = new();
    }

    public sealed class InvasionReinforcementRuleConfig
    {
        // Names the rule's own clock, so editing or reordering the configuration does not move a running
        // schedule onto another rule.
        [XmlElement("Id")]
        public string? Id { get; set; }

        [XmlElement("PartyTemplateId")]
        public string? PartyTemplateId { get; set; }

        [XmlElement("IntervalInDays")]
        public float IntervalInDays { get; set; } = 7f;

        // How many waves the rule ever sends. Left out, it keeps sending for as long as the invasion lives;
        // one wave makes the rule a single reinforcement that arrives the interval after the capital falls.
        [XmlElement("MaxWaves")]
        public int MaxWaves { get; set; }

        // How many lords join the invasion on each wave while it holds its capital. They fill their parties
        // from the garrison. Left out, a wave brings troops only.
        [XmlElement("LordsPerWave")]
        public int LordsPerWave { get; set; }
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

            ValidateNonNegativeFinite("Invasion MaxFactionStrength", config.MaxFactionStrength);
            ValidateNonNegativeFinite("Invasion StrengthPerTownHeld", config.StrengthPerTownHeld);
            ValidateNonNegative("Invasion MinimumCapitalGarrison", config.MinimumCapitalGarrison);
            ValidateNonNegative("Invasion Supplies FoodPerConvoy", config.Supplies.FoodPerConvoy);
            // Convoys are looked at once a day, so a shorter interval than that would only lie about itself.
            if (config.Supplies.FoodPerConvoy > 0 &&
                (!IsFinite(config.Supplies.IntervalInDays) || config.Supplies.IntervalInDays < 1f))
            {
                throw new ArgumentException("Invasion Supplies IntervalInDays must be finite and at least one day.");
            }
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
            // Waves are looked at once a day, so a shorter interval than that would only lie about itself.
            if (!IsFinite(rule.IntervalInDays) || rule.IntervalInDays < 1f)
            {
                throw new ArgumentException("Invasion Reinforcements Rule '" + rule.Id +
                                           "' IntervalInDays must be finite and at least one day.");
            }
            ValidateNonNegative("Invasion Reinforcements Rule '" + rule.Id + "' LordsPerWave", rule.LordsPerWave);
            ValidateNonNegative("Invasion Reinforcements Rule '" + rule.Id + "' MaxWaves", rule.MaxWaves);
        }

        private static void ValidateNonNegative(string name, int value)
        {
            if (value < 0)
            {
                throw new ArgumentException(name + " cannot be negative.");
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
