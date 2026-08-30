using CustomSpawns.Data.Dto;
using CustomSpawns.Data.Model;

namespace CustomSpawns.Data.Adapter
{
    internal sealed class RaidClosestVillageConfigAdapter
        : PriorityPartyBehaviourConfigAdapter<RaidClosestVillageConfig>
    {
        private const string BehaviourName = "RaidClosestVillage";

        protected override PriorityPartyBehaviourData Adapt(RaidClosestVillageConfig config)
        {
            StrengthAssessmentConfig strengthAssessment = PriorityPartyBehaviourConfigDefaults
                .ApplyStrengthDefaults(config.StrengthAssessment, 1.25f, 1.1f);
            ReconnaissanceConfig reconnaissance = PriorityPartyBehaviourConfigDefaults
                .ApplyReconnaissanceDefaults(config.Reconnaissance);
            FloatRangeConfig operationWindow = PriorityPartyBehaviourConfigDefaults
                .ApplyRangeDefaults(config.OperationWindowInDays, 3f, 6f);
            RecoveryConfig recovery = PriorityPartyBehaviourConfigDefaults
                .ApplyRecoveryDefaults(config.Recovery, 1f, 2);
            PriorityPartyBehaviourConfigValidator.ValidateOffensiveValues(BehaviourName, strengthAssessment,
                reconnaissance, operationWindow, recovery, config.CooldownInDays);

            return new RaidClosestVillageData(strengthAssessment.MinimumAttackerToTargetRatio,
                strengthAssessment.MinimumAttackerToRoamingHostilesRatio,
                reconnaissance.RequiredClearTimeInDays.Minimum,
                reconnaissance.RequiredClearTimeInDays.Maximum,
                reconnaissance.SkipWhenTargetStrengthRatio, operationWindow.Minimum,
                operationWindow.Maximum, recovery.GracePeriodInDays,
                recovery.MaximumFailedAttempts, config.CooldownInDays);
        }
    }
}
