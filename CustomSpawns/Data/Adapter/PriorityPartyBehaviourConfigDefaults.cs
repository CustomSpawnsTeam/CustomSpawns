using CustomSpawns.Data.Model;

namespace CustomSpawns.Data.Adapter
{
    /**
     * Fills in the values a behaviour configuration left out of the XML. An omitted value is not the same
     * as a zero, so the *Specified flags decide whether the documented default applies.
     */
    internal static class PriorityPartyBehaviourConfigDefaults
    {
        public static StrengthAssessmentConfig ApplyStrengthDefaults(StrengthAssessmentConfig config,
            float minimumAttackerToTargetRatio, float minimumAttackerToRoamingHostilesRatio)
        {
            return new StrengthAssessmentConfig(
                config.MinimumAttackerToTargetRatioSpecified
                    ? config.MinimumAttackerToTargetRatio
                    : minimumAttackerToTargetRatio,
                config.MinimumAttackerToRoamingHostilesRatioSpecified
                    ? config.MinimumAttackerToRoamingHostilesRatio
                    : minimumAttackerToRoamingHostilesRatio);
        }

        public static ReconnaissanceConfig ApplyReconnaissanceDefaults(ReconnaissanceConfig config)
        {
            return new ReconnaissanceConfig
            {
                RequiredClearTimeInDays = ApplyRangeDefaults(config.RequiredClearTimeInDays, 0.5f, 1f),
                SkipWhenTargetStrengthRatio = config.SkipWhenTargetStrengthRatio
            };
        }

        public static FloatRangeConfig ApplyRangeDefaults(FloatRangeConfig config,
            float minimum, float maximum)
        {
            return new FloatRangeConfig(config.MinimumSpecified ? config.Minimum : minimum,
                config.MaximumSpecified ? config.Maximum : maximum);
        }
    }
}
