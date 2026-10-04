using System;
using System.Xml.Serialization;

namespace CustomSpawns.Data.Model
{
    public sealed class InvasionConfig
    {
        [XmlElement("RequiresFirstCoastalConquest")]
        public bool RequiresFirstCoastalConquest { get; set; }

        [XmlElement("Supplies")]
        public InvasionSuppliesConfig Supplies { get; set; } = new();

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
            if (config.Supplies == null || config.Reinforcements == null)
            {
                throw new ArgumentException("Invasion configuration groups cannot be null.");
            }

            ValidateNonNegativeFinite("Invasion Supplies FoodStockFloor", config.Supplies.FoodStockFloor);
            ValidateNonNegativeFinite("Invasion Supplies DailyFoodBonus", config.Supplies.DailyFoodBonus);
            if (string.IsNullOrWhiteSpace(config.Reinforcements.PartyTemplateId))
            {
                throw new ArgumentException("Invasion Reinforcements PartyTemplateId is required.");
            }
            if (!IsFinite(config.Reinforcements.IntervalInDays) || config.Reinforcements.IntervalInDays <= 0f)
            {
                throw new ArgumentException("Invasion Reinforcements IntervalInDays must be finite and greater than zero.");
            }
            if (config.Reinforcements.MaxConcurrentParties <= 0)
            {
                throw new ArgumentException("Invasion Reinforcements MaxConcurrentParties must be greater than zero.");
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
