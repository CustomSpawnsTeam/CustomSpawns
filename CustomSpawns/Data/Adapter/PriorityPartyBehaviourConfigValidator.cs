using System;
using CustomSpawns.Data.Model;

namespace CustomSpawns.Data.Adapter
{
    /**
     * Rejects behaviour configurations the AI cannot act on. A ratio of zero, a negative cooldown or an
     * inverted range would otherwise surface much later as a party that never picks a target.
     */
    public static class PriorityPartyBehaviourConfigValidator
    {
        public static void ValidateOffensiveValues(string behaviourName, StrengthAssessmentConfig strengthAssessment,
            ReconnaissanceConfig reconnaissance, FloatRangeConfig operationWindow, float cooldownInDays)
        {
            if (!IsFinite(strengthAssessment.MinimumAttackerToTargetRatio) ||
                !IsFinite(strengthAssessment.MinimumAttackerToRoamingHostilesRatio) ||
                !IsFinite(reconnaissance.SkipWhenTargetStrengthRatio))
            {
                throw new ArgumentException(behaviourName + " strength ratios must be finite.");
            }
            if (strengthAssessment.MinimumAttackerToTargetRatio <= 0f ||
                strengthAssessment.MinimumAttackerToRoamingHostilesRatio <= 0f ||
                reconnaissance.SkipWhenTargetStrengthRatio <= 0f)
            {
                throw new ArgumentException(behaviourName + " strength ratios must be greater than zero.");
            }
            ValidateRange(behaviourName + " reconnaissance clear time",
                reconnaissance.RequiredClearTimeInDays.Minimum, reconnaissance.RequiredClearTimeInDays.Maximum);
            ValidateRange(behaviourName + " operation window", operationWindow.Minimum, operationWindow.Maximum);
            if (!IsFinite(cooldownInDays))
            {
                throw new ArgumentException(behaviourName + " cooldown must be finite.");
            }
            if (cooldownInDays < 0f)
            {
                throw new ArgumentException(behaviourName + " cooldown cannot be negative.");
            }
        }

        public static void ValidateRange(string name, float minimum, float maximum)
        {
            if (!IsFinite(minimum) || !IsFinite(maximum))
            {
                throw new ArgumentException(name + " values must be finite.");
            }
            if (minimum < 0f)
            {
                throw new ArgumentException(name + " minimum cannot be negative.");
            }
            if (maximum < minimum)
            {
                throw new ArgumentException(name + " maximum must be greater than or equal to its minimum.");
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
