using System;
using System.Collections.Generic;
using CustomSpawns.Data.Adapter;
using CustomSpawns.Data.Model;

namespace CustomSpawns.Data.Reader.Impl
{
    internal static class PriorityPartyBehaviourTemplateResolver
    {
        internal static void Resolve(Spawns document)
        {
            Dictionary<string, PriorityPartyBehaviourTemplate> templates =
                new(StringComparer.Ordinal);

            foreach (PriorityPartyBehaviourTemplate template in document.PriorityPartyBehaviourTemplates ??
                                                                 new List<PriorityPartyBehaviourTemplate>())
            {
                string? id = template.Id;
                if (string.IsNullOrWhiteSpace(id))
                {
                    throw new ArgumentException("PriorityPartyBehaviourTemplate has a blank Id.");
                }

                if (!template.PriorityPartyBehavioursSpecified)
                {
                    throw new ArgumentException("PriorityPartyBehaviourTemplate '" + id +
                                                "' is missing its PriorityPartyBehaviours wrapper.");
                }

                if (templates.ContainsKey(id!))
                {
                    throw new ArgumentException("Duplicate PriorityPartyBehaviourTemplate Id '" + id + "'.");
                }

                templates.Add(id!, template);
            }

            List<(Model.Spawn Spawn, PriorityPartyBehaviourTemplate Template)> references = new();
            foreach (Model.Spawn spawn in document.AllSpawns ?? new List<Model.Spawn>())
            {
                if (!spawn.PriorityPartyBehaviourTemplateIdSpecified)
                {
                    continue;
                }

                string? reference = spawn.PriorityPartyBehaviourTemplateId;
                string partyTemplate = spawn.PartyTemplate ?? "<blank>";
                if (spawn.PriorityPartyBehavioursSpecified)
                {
                    throw new ArgumentException("Spawn party_template '" + partyTemplate +
                                                "' cannot use inline PriorityPartyBehaviours and " +
                                                "PriorityPartyBehaviourTemplateId '" +
                                                (string.IsNullOrWhiteSpace(reference) ? "<blank>" : reference) + "'.");
                }

                if (string.IsNullOrWhiteSpace(reference))
                {
                    throw new ArgumentException("Spawn party_template '" + partyTemplate +
                                                "' has a blank PriorityPartyBehaviourTemplateId reference.");
                }

                if (!templates.TryGetValue(reference!, out PriorityPartyBehaviourTemplate template))
                {
                    throw new ArgumentException("Spawn party_template '" + partyTemplate +
                                                "' references unknown PriorityPartyBehaviourTemplate Id '" +
                                                reference + "'.");
                }

                references.Add((spawn, template));
            }

            foreach ((Model.Spawn spawn, PriorityPartyBehaviourTemplate template) in references)
            {
                spawn.PriorityPartyBehaviours = CloneBehaviours(template.PriorityPartyBehaviours);
                spawn.PriorityPartyBehavioursSpecified = true;
            }
        }

        private static List<PriorityPartyBehaviourConfig> CloneBehaviours(
            List<PriorityPartyBehaviourConfig>? behaviours)
        {
            List<PriorityPartyBehaviourConfig> clones = new();
            foreach (PriorityPartyBehaviourConfig behaviour in behaviours ?? new List<PriorityPartyBehaviourConfig>())
            {
                clones.Add(CloneBehaviour(behaviour));
            }

            return clones;
        }

        private static PriorityPartyBehaviourConfig CloneBehaviour(PriorityPartyBehaviourConfig behaviour)
        {
            switch (behaviour)
            {
                case BesiegeClosestSettlementConfig besiege:
                    return new BesiegeClosestSettlementConfig
                    {
                        TargetTypes = CloneTargetTypes(besiege.TargetTypes),
                        TargetTypesSpecified = besiege.TargetTypesSpecified,
                        StrengthAssessment = CloneStrengthAssessment(besiege.StrengthAssessment),
                        Reconnaissance = CloneReconnaissance(besiege.Reconnaissance),
                        OperationWindowInDays = CloneRange(besiege.OperationWindowInDays),
                        Recovery = CloneRecovery(besiege.Recovery),
                        CooldownInDays = besiege.CooldownInDays
                    };
                case RaidClosestVillageConfig raid:
                    return new RaidClosestVillageConfig
                    {
                        StrengthAssessment = CloneStrengthAssessment(raid.StrengthAssessment),
                        Reconnaissance = CloneReconnaissance(raid.Reconnaissance),
                        OperationWindowInDays = CloneRange(raid.OperationWindowInDays),
                        Recovery = CloneRecovery(raid.Recovery),
                        CooldownInDays = raid.CooldownInDays
                    };
                case PatrolAroundClosestLestInterruptedAndSwitchConfig patrol:
                    return new PatrolAroundClosestLestInterruptedAndSwitchConfig
                    {
                        TargetTypes = CloneTargetTypes(patrol.TargetTypes),
                        TargetTypesSpecified = patrol.TargetTypesSpecified,
                        StableTimeInDays = CloneRange(patrol.StableTimeInDays)
                    };
                default:
                    return behaviour;
            }
        }

        private static List<SpawnSettlementType>? CloneTargetTypes(List<SpawnSettlementType>? targetTypes)
        {
            return targetTypes == null ? null : new List<SpawnSettlementType>(targetTypes);
        }

        private static StrengthAssessmentConfig CloneStrengthAssessment(StrengthAssessmentConfig source)
        {
            return new StrengthAssessmentConfig
            {
                MinimumAttackerToTargetRatio = source.MinimumAttackerToTargetRatio,
                MinimumAttackerToTargetRatioSpecified = source.MinimumAttackerToTargetRatioSpecified,
                MinimumAttackerToRoamingHostilesRatio = source.MinimumAttackerToRoamingHostilesRatio,
                MinimumAttackerToRoamingHostilesRatioSpecified =
                    source.MinimumAttackerToRoamingHostilesRatioSpecified
            };
        }

        private static ReconnaissanceConfig CloneReconnaissance(ReconnaissanceConfig source)
        {
            return new ReconnaissanceConfig
            {
                RequiredClearTimeInDays = CloneRange(source.RequiredClearTimeInDays),
                SkipWhenTargetStrengthRatio = source.SkipWhenTargetStrengthRatio
            };
        }

        private static RecoveryConfig CloneRecovery(RecoveryConfig source)
        {
            return new RecoveryConfig
            {
                GracePeriodInDays = source.GracePeriodInDays,
                GracePeriodInDaysSpecified = source.GracePeriodInDaysSpecified,
                MaximumFailedAttempts = source.MaximumFailedAttempts,
                MaximumFailedAttemptsSpecified = source.MaximumFailedAttemptsSpecified
            };
        }

        private static FloatRangeConfig CloneRange(FloatRangeConfig source)
        {
            return new FloatRangeConfig
            {
                Minimum = source.Minimum,
                MinimumSpecified = source.MinimumSpecified,
                Maximum = source.Maximum,
                MaximumSpecified = source.MaximumSpecified
            };
        }
    }
}
