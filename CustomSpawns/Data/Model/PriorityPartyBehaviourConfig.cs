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
        private List<SpawnSettlementType>? _targetTypes;

        [XmlArray("TargetTypes")]
        [XmlArrayItem("TargetType")]
        public List<SpawnSettlementType>? TargetTypes
        {
            get => _targetTypes;
            set
            {
                _targetTypes = value;
                if (value?.Count > 0)
                {
                    TargetTypesSpecified = true;
                }
            }
        }

        [XmlIgnore]
        public bool TargetTypesSpecified { get; set; }

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
        private List<SpawnSettlementType>? _targetTypes;

        [XmlArray("TargetTypes")]
        [XmlArrayItem("TargetType")]
        public List<SpawnSettlementType>? TargetTypes
        {
            get => _targetTypes;
            set
            {
                _targetTypes = value;
                if (value?.Count > 0)
                {
                    TargetTypesSpecified = true;
                }
            }
        }

        [XmlIgnore]
        public bool TargetTypesSpecified { get; set; }

        [XmlElement("StableTimeInDays")]
        public FloatRangeConfig StableTimeInDays { get; set; } = new(0f, 10f);
    }

    public sealed class StrengthAssessmentConfig
    {
        private float _minimumAttackerToTargetRatio;
        private float _minimumAttackerToRoamingHostilesRatio;

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
        public float MinimumAttackerToTargetRatio
        {
            get => _minimumAttackerToTargetRatio;
            set
            {
                _minimumAttackerToTargetRatio = value;
                MinimumAttackerToTargetRatioSpecified = true;
            }
        }

        [XmlIgnore]
        public bool MinimumAttackerToTargetRatioSpecified { get; set; }

        [XmlElement("MinimumAttackerToRoamingHostilesRatio")]
        public float MinimumAttackerToRoamingHostilesRatio
        {
            get => _minimumAttackerToRoamingHostilesRatio;
            set
            {
                _minimumAttackerToRoamingHostilesRatio = value;
                MinimumAttackerToRoamingHostilesRatioSpecified = true;
            }
        }

        [XmlIgnore]
        public bool MinimumAttackerToRoamingHostilesRatioSpecified { get; set; }
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
        private float _gracePeriodInDays;
        private int _maximumFailedAttempts;

        public RecoveryConfig()
        {
        }

        public RecoveryConfig(float gracePeriodInDays, int maximumFailedAttempts)
        {
            GracePeriodInDays = gracePeriodInDays;
            MaximumFailedAttempts = maximumFailedAttempts;
        }

        [XmlElement("GracePeriodInDays")]
        public float GracePeriodInDays
        {
            get => _gracePeriodInDays;
            set
            {
                _gracePeriodInDays = value;
                GracePeriodInDaysSpecified = true;
            }
        }

        [XmlIgnore]
        public bool GracePeriodInDaysSpecified { get; set; }

        [XmlElement("MaximumFailedAttempts")]
        public int MaximumFailedAttempts
        {
            get => _maximumFailedAttempts;
            set
            {
                _maximumFailedAttempts = value;
                MaximumFailedAttemptsSpecified = true;
            }
        }

        [XmlIgnore]
        public bool MaximumFailedAttemptsSpecified { get; set; }
    }

    public sealed class FloatRangeConfig
    {
        private float _minimum;
        private float _maximum;

        public FloatRangeConfig()
        {
        }

        public FloatRangeConfig(float minimum, float maximum)
        {
            Minimum = minimum;
            Maximum = maximum;
        }

        [XmlElement("Minimum")]
        public float Minimum
        {
            get => _minimum;
            set
            {
                _minimum = value;
                MinimumSpecified = true;
            }
        }

        [XmlIgnore]
        public bool MinimumSpecified { get; set; }

        [XmlElement("Maximum")]
        public float Maximum
        {
            get => _maximum;
            set
            {
                _maximum = value;
                MaximumSpecified = true;
            }
        }

        [XmlIgnore]
        public bool MaximumSpecified { get; set; }
    }
}
