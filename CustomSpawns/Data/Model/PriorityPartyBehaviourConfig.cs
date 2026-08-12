using System.Collections.Generic;
using System.Xml.Serialization;
using CustomSpawns.Data.Adapter;

namespace CustomSpawns.Data.Model
{
    [XmlInclude(typeof(BesiegeClosestSettlementConfig))]
    [XmlInclude(typeof(RaidClosestVillageConfig))]
    [XmlInclude(typeof(PatrolAroundClosestLestInterruptedAndSwitchConfig))]
    public abstract class PriorityPartyBehaviourConfig
    {
    }

    public sealed class BesiegeClosestSettlementConfig : PriorityPartyBehaviourConfig
    {
        [XmlArray("TargetTypes")]
        [XmlArrayItem("TargetType")]
        public List<SpawnSettlementType>? TargetTypes { get; set; }

        [XmlElement("StrengthAssessment")]
        public StrengthAssessmentConfig StrengthAssessment { get; set; } = new(1.5f, 1.2f);

        [XmlElement("Reconnaissance")]
        public ReconnaissanceConfig Reconnaissance { get; set; } = new();

        [XmlElement("OperationWindowInDays")]
        public FloatRangeConfig OperationWindowInDays { get; set; } = new(5f, 10f);

        [XmlElement("Recovery")]
        public RecoveryConfig Recovery { get; set; } = new(2f, 2);

        [XmlElement("CooldownInDays")]
        public float CooldownInDays { get; set; } = 2f;
    }

    public sealed class RaidClosestVillageConfig : PriorityPartyBehaviourConfig
    {
        [XmlElement("StrengthAssessment")]
        public StrengthAssessmentConfig StrengthAssessment { get; set; } = new(1.25f, 1.1f);

        [XmlElement("Reconnaissance")]
        public ReconnaissanceConfig Reconnaissance { get; set; } = new();

        [XmlElement("OperationWindowInDays")]
        public FloatRangeConfig OperationWindowInDays { get; set; } = new(3f, 6f);

        [XmlElement("Recovery")]
        public RecoveryConfig Recovery { get; set; } = new(1f, 2);

        [XmlElement("CooldownInDays")]
        public float CooldownInDays { get; set; } = 2f;
    }

    public sealed class PatrolAroundClosestLestInterruptedAndSwitchConfig : PriorityPartyBehaviourConfig
    {
        [XmlArray("TargetTypes")]
        [XmlArrayItem("TargetType")]
        public List<SpawnSettlementType>? TargetTypes { get; set; }

        [XmlElement("StableTimeInDays")]
        public FloatRangeConfig StableTimeInDays { get; set; } = new(0f, 10f);
    }

    public sealed class StrengthAssessmentConfig
    {
        public StrengthAssessmentConfig()
        {
        }

        public StrengthAssessmentConfig(float minimumAttackerToTargetRatio,
            float minimumAttackerToRoamingHostilesRatio)
        {
            MinimumAttackerToTargetRatio = minimumAttackerToTargetRatio;
            MinimumAttackerToRoamingHostilesRatio = minimumAttackerToRoamingHostilesRatio;
        }

        [XmlElement("MinimumAttackerToTargetRatio")]
        public float MinimumAttackerToTargetRatio { get; set; }

        [XmlElement("MinimumAttackerToRoamingHostilesRatio")]
        public float MinimumAttackerToRoamingHostilesRatio { get; set; }
    }

    public sealed class ReconnaissanceConfig
    {
        [XmlElement("RequiredClearTimeInDays")]
        public FloatRangeConfig RequiredClearTimeInDays { get; set; } = new(0.5f, 1f);

        [XmlElement("SkipWhenTargetStrengthRatio")]
        public float SkipWhenTargetStrengthRatio { get; set; } = 3f;
    }

    public sealed class RecoveryConfig
    {
        public RecoveryConfig()
        {
        }

        public RecoveryConfig(float gracePeriodInDays, int maximumFailedAttempts)
        {
            GracePeriodInDays = gracePeriodInDays;
            MaximumFailedAttempts = maximumFailedAttempts;
        }

        [XmlElement("GracePeriodInDays")]
        public float GracePeriodInDays { get; set; }

        [XmlElement("MaximumFailedAttempts")]
        public int MaximumFailedAttempts { get; set; }
    }

    public sealed class FloatRangeConfig
    {
        public FloatRangeConfig()
        {
        }

        public FloatRangeConfig(float minimum, float maximum)
        {
            Minimum = minimum;
            Maximum = maximum;
        }

        [XmlElement("Minimum")]
        public float Minimum { get; set; }

        [XmlElement("Maximum")]
        public float Maximum { get; set; }
    }
}
