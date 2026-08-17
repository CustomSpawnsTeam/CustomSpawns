using System;
using System.Collections.Generic;
using System.Linq;
using CustomSpawns.Data.Dto;
using CustomSpawns.Data.Model;

namespace CustomSpawns.Data.Adapter
{
    public class PartyBehaviourDtoAdapter
    {
        public PartyBehaviourDto Adapt(PriorityPartyBehaviourTemplate template)
        {
            return new PartyBehaviourDto
            {
                Id = template.Id!,
                PriorityPartyBehaviours = AdaptPriorityPartyBehaviours(template.PriorityPartyBehaviours)
            };
        }

        internal static IReadOnlyList<PriorityPartyBehaviourData> AdaptPriorityPartyBehaviours(
            List<PriorityPartyBehaviourConfig>? configurations)
        {
            configurations ??= new List<PriorityPartyBehaviourConfig>();
            if (configurations.GroupBy(configuration => configuration.GetType()).Any(group => group.Count() > 1))
            {
                throw new ArgumentException("PriorityPartyBehaviours cannot contain duplicate behaviour types.");
            }

            return configurations.Select(AdaptPriorityPartyBehaviour).ToList();
        }

        private static PriorityPartyBehaviourData AdaptPriorityPartyBehaviour(
            PriorityPartyBehaviourConfig configuration)
        {
            switch (configuration)
            {
                case BesiegeClosestSettlementConfig besiege:
                    return AdaptBesiegeClosestSettlement(besiege);
                case RaidClosestVillageConfig raid:
                    return AdaptRaidClosestVillage(raid);
                case PatrolAroundClosestLestInterruptedAndSwitchConfig patrol:
                    FloatRangeConfig stableTime = ApplyRangeDefaults(patrol.StableTimeInDays, 0f, 10f);
                    ValidateRange("patrol stable time", stableTime.Minimum, stableTime.Maximum);
                    List<SpawnSettlementType> targetTypes = patrol.TargetTypesSpecified || patrol.TargetTypes?.Count > 0
                        ? patrol.TargetTypes ?? new List<SpawnSettlementType>()
                        : AllSettlementTypes();
                    return new PatrolAroundClosestLestInterruptedAndSwitchData(
                        stableTime.Minimum, stableTime.Maximum, targetTypes);
                default:
                    throw new ArgumentException("Unsupported priority party behaviour configuration.");
            }
        }

        private static BesiegeClosestSettlementData AdaptBesiegeClosestSettlement(
            BesiegeClosestSettlementConfig config)
        {
            List<SpawnSettlementType> targetTypes = config.TargetTypesSpecified || config.TargetTypes?.Count > 0
                ? config.TargetTypes ?? new List<SpawnSettlementType>()
                : new List<SpawnSettlementType> { SpawnSettlementType.Town, SpawnSettlementType.Castle };
            if (targetTypes.Count == 0)
            {
                throw new ArgumentException("BesiegeClosestSettlement requires at least one target type.");
            }
            if (targetTypes.Any(type => type != SpawnSettlementType.Town && type != SpawnSettlementType.Castle))
            {
                throw new ArgumentException("BesiegeClosestSettlement only supports Town and Castle target types.");
            }

            StrengthAssessmentConfig strengthAssessment = ApplyStrengthDefaults(config.StrengthAssessment,
                1.5f, 1.2f);
            ReconnaissanceConfig reconnaissance = ApplyReconnaissanceDefaults(config.Reconnaissance);
            FloatRangeConfig operationWindow = ApplyRangeDefaults(config.OperationWindowInDays, 5f, 10f);
            RecoveryConfig recovery = ApplyRecoveryDefaults(config.Recovery, 2f, 2);
            ValidateOffensiveValues("BesiegeClosestSettlement", strengthAssessment, reconnaissance,
                operationWindow, recovery, config.CooldownInDays);
            return new BesiegeClosestSettlementData(targetTypes, strengthAssessment.MinimumAttackerToTargetRatio,
                strengthAssessment.MinimumAttackerToRoamingHostilesRatio,
                reconnaissance.RequiredClearTimeInDays.Minimum,
                reconnaissance.RequiredClearTimeInDays.Maximum,
                reconnaissance.SkipWhenTargetStrengthRatio, operationWindow.Minimum,
                operationWindow.Maximum, recovery.GracePeriodInDays,
                recovery.MaximumFailedAttempts, config.CooldownInDays);
        }

        private static RaidClosestVillageData AdaptRaidClosestVillage(RaidClosestVillageConfig config)
        {
            StrengthAssessmentConfig strengthAssessment = ApplyStrengthDefaults(config.StrengthAssessment,
                1.25f, 1.1f);
            ReconnaissanceConfig reconnaissance = ApplyReconnaissanceDefaults(config.Reconnaissance);
            FloatRangeConfig operationWindow = ApplyRangeDefaults(config.OperationWindowInDays, 3f, 6f);
            RecoveryConfig recovery = ApplyRecoveryDefaults(config.Recovery, 1f, 2);
            ValidateOffensiveValues("RaidClosestVillage", strengthAssessment, reconnaissance,
                operationWindow, recovery, config.CooldownInDays);
            return new RaidClosestVillageData(strengthAssessment.MinimumAttackerToTargetRatio,
                strengthAssessment.MinimumAttackerToRoamingHostilesRatio,
                reconnaissance.RequiredClearTimeInDays.Minimum,
                reconnaissance.RequiredClearTimeInDays.Maximum,
                reconnaissance.SkipWhenTargetStrengthRatio, operationWindow.Minimum,
                operationWindow.Maximum, recovery.GracePeriodInDays,
                recovery.MaximumFailedAttempts, config.CooldownInDays);
        }

        private static StrengthAssessmentConfig ApplyStrengthDefaults(StrengthAssessmentConfig config,
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

        private static ReconnaissanceConfig ApplyReconnaissanceDefaults(ReconnaissanceConfig config)
        {
            return new ReconnaissanceConfig
            {
                RequiredClearTimeInDays = ApplyRangeDefaults(config.RequiredClearTimeInDays, 0.5f, 1f),
                SkipWhenTargetStrengthRatio = config.SkipWhenTargetStrengthRatio
            };
        }

        private static RecoveryConfig ApplyRecoveryDefaults(RecoveryConfig config,
            float gracePeriodInDays, int maximumFailedAttempts)
        {
            return new RecoveryConfig(
                config.GracePeriodInDaysSpecified ? config.GracePeriodInDays : gracePeriodInDays,
                config.MaximumFailedAttemptsSpecified ? config.MaximumFailedAttempts : maximumFailedAttempts);
        }

        private static FloatRangeConfig ApplyRangeDefaults(FloatRangeConfig config,
            float minimum, float maximum)
        {
            return new FloatRangeConfig(config.MinimumSpecified ? config.Minimum : minimum,
                config.MaximumSpecified ? config.Maximum : maximum);
        }

        private static void ValidateOffensiveValues(string behaviourName, StrengthAssessmentConfig strengthAssessment,
            ReconnaissanceConfig reconnaissance, FloatRangeConfig operationWindow, RecoveryConfig recovery,
            float cooldownInDays)
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
            if (!IsFinite(recovery.GracePeriodInDays))
            {
                throw new ArgumentException(behaviourName + " grace period must be finite.");
            }
            if (recovery.GracePeriodInDays < 0f)
            {
                throw new ArgumentException(behaviourName + " grace period cannot be negative.");
            }
            if (recovery.MaximumFailedAttempts < 1)
            {
                throw new ArgumentException(behaviourName + " maximum failed attempts must be at least one.");
            }
            if (!IsFinite(cooldownInDays))
            {
                throw new ArgumentException(behaviourName + " cooldown must be finite.");
            }
            if (cooldownInDays < 0f)
            {
                throw new ArgumentException(behaviourName + " cooldown cannot be negative.");
            }
        }

        internal static void ValidateRange(string name, float minimum, float maximum)
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

        private static List<SpawnSettlementType> AllSettlementTypes()
        {
            return new List<SpawnSettlementType>
            {
                SpawnSettlementType.Village,
                SpawnSettlementType.Town,
                SpawnSettlementType.Castle
            };
        }
    }
}
