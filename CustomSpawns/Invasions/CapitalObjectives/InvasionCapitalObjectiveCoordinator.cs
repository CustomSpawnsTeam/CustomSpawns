using System.Collections.Generic;
using System.Linq;
using CustomSpawns.AI.Offensives;

namespace CustomSpawns.Invasions.CapitalObjectives
{
    public sealed class InvasionCapitalObjectiveCoordinator
    {
        private readonly IInvasionAssaultConfigurationProvider _invasionAssaultConfigurationProvider;
        private readonly IInvasionCapitalObjectiveWorld _invasionCapitalObjectiveWorld;
        private readonly CapitalObjectiveStrengthRules _capitalObjectiveStrengthRules;
        private readonly CapitalArmyMobilizationPlanner _capitalArmyMobilizationPlanner;
        private readonly SiegeTargetSelector _siegeTargetSelector;

        public InvasionCapitalObjectiveCoordinator(
            IInvasionAssaultConfigurationProvider invasionAssaultConfigurationProvider,
            IInvasionCapitalObjectiveWorld invasionCapitalObjectiveWorld,
            CapitalObjectiveStrengthRules capitalObjectiveStrengthRules,
            CapitalArmyMobilizationPlanner capitalArmyMobilizationPlanner,
            SiegeTargetSelector siegeTargetSelector)
        {
            _invasionAssaultConfigurationProvider = invasionAssaultConfigurationProvider;
            _invasionCapitalObjectiveWorld = invasionCapitalObjectiveWorld;
            _capitalObjectiveStrengthRules = capitalObjectiveStrengthRules;
            _capitalArmyMobilizationPlanner = capitalArmyMobilizationPlanner;
            _siegeTargetSelector = siegeTargetSelector;
        }

        public CapitalObjectiveUpdate UpdateHourly(Invasion invasion)
        {
            string? capitalSettlementId = invasion.CapitalSettlementId;

            if (capitalSettlementId == null || invasion.IsSuspended)
            {
                return NoChange(invasion);
            }

            InvasionAssaultRequirements? requirements =
                _invasionAssaultConfigurationProvider.Find(invasion.SpawnTemplateId);

            if (requirements == null)
            {
                invasion.CompleteCapitalObjective();
                return new CapitalObjectiveUpdate(CapitalObjectiveDecision.ConfigurationMissing, null);
            }

            InvasionCapitalObjective? objective = invasion.CapitalObjective;
            CapitalObjectiveObservation observation = _invasionCapitalObjectiveWorld.Observe(
                invasion.ClanId, capitalSettlementId, objective?.TargetSettlementId,
                objective?.CommanderPartyId);

            CapitalObjectiveDecision ownershipDecision = ReconcileCapitalOwnership(invasion,
                capitalSettlementId, observation);

            if (ownershipDecision == CapitalObjectiveDecision.Complete)
            {
                return new CapitalObjectiveUpdate(ownershipDecision, null);
            }

            objective = invasion.CapitalObjective;

            if (objective == null || objective.Phase == InvasionCapitalObjectivePhase.Dormant)
            {
                return new CapitalObjectiveUpdate(ownershipDecision, objective?.TargetSettlementId);
            }

            if (objective.Kind == InvasionCapitalObjectiveKind.Attack &&
                observation.TargetIsOwnedByInvasion)
            {
                return CompleteCapturedTarget(invasion, observation);
            }

            if (objective.Kind == InvasionCapitalObjectiveKind.Attack &&
                objective.Phase != InvasionCapitalObjectivePhase.Besieging &&
                observation.TargetHasMatchingKingdomSiege)
            {
                string? target = objective.TargetSettlementId;
                invasion.MarkCapitalSiegeStarted();
                return new CapitalObjectiveUpdate(CapitalObjectiveDecision.SiegeStarted, target);
            }

            if ((objective.Phase == InvasionCapitalObjectivePhase.Gathering ||
                 objective.Phase == InvasionCapitalObjectivePhase.Advancing) &&
                (!observation.CommanderIsEligible || !observation.ObjectiveArmyIsUnified))
            {
                objective.RequireRemobilization();
            }

            if (objective.Phase != InvasionCapitalObjectivePhase.Advancing &&
                objective.IsAttackDeadlineReached(observation.NowDays))
            {
                return TimeOutAttack(invasion);
            }

            CapitalArmyMobilizationResult? mobilizationResult = null;
            if (objective.Phase == InvasionCapitalObjectivePhase.Mobilizing)
            {
                mobilizationResult = TryMobilize(invasion, objective,
                    observation.NowDays);

                if (!mobilizationResult.Succeeded)
                {
                    return new CapitalObjectiveUpdate(CapitalObjectiveDecision.MobilizationFailed,
                        objective.TargetSettlementId, mobilizationResult: mobilizationResult);
                }

                observation = _invasionCapitalObjectiveWorld.Observe(invasion.ClanId,
                    capitalSettlementId, objective.TargetSettlementId,
                    objective.CommanderPartyId);
            }

            CapitalObjectiveUpdate? gathering = EvaluateGathering(invasion, requirements, observation);

            if (gathering != null)
            {
                return new CapitalObjectiveUpdate(gathering.Value.Decision,
                    gathering.Value.TargetSettlementId, gathering.Value.Evaluation,
                    gathering.Value.MobilizationResult ?? mobilizationResult);
            }

            objective = invasion.CapitalObjective;

            if (objective?.Phase == InvasionCapitalObjectivePhase.Besieging)
            {
                if (!observation.TargetHasMatchingKingdomSiege)
                {
                    string? endedSiegeTarget = objective.TargetSettlementId;
                    objective.AbortSiege();
                    return new CapitalObjectiveUpdate(CapitalObjectiveDecision.AbandonSiegeAborted,
                        endedSiegeTarget);
                }

                return NoChange(invasion);
            }

            if (objective?.IsAttackDeadlineReached(observation.NowDays) == true)
            {
                return TimeOutAttack(invasion);
            }

            return new CapitalObjectiveUpdate(ownershipDecision, objective?.TargetSettlementId,
                mobilizationResult: mobilizationResult);
        }

        public CapitalObjectiveUpdate UpdateDaily(Invasion invasion)
        {
            InvasionCapitalObjective? objective = invasion.CapitalObjective;

            if (invasion.IsSuspended || invasion.CapitalSettlementId == null ||
                objective?.Phase != InvasionCapitalObjectivePhase.Dormant ||
                objective.DormantReason != InvasionCapitalObjectiveDormantReason.NoViableTarget)
            {
                return NoChange(invasion);
            }

            InvasionAssaultRequirements? requirements =
                _invasionAssaultConfigurationProvider.Find(invasion.SpawnTemplateId);

            if (requirements == null)
            {
                invasion.CompleteCapitalObjective();
                return new CapitalObjectiveUpdate(CapitalObjectiveDecision.ConfigurationMissing, null);
            }

            CapitalArmyMobilizationPlan? plan = _capitalArmyMobilizationPlanner.Create(
                invasion.ClanId, InvasionCapitalObjectiveKind.Attack,
                invasion.CapitalSettlementId,
                _invasionCapitalObjectiveWorld.FindMobilizationCandidates(invasion.ClanId));

            if (plan == null)
            {
                return NoChange(invasion);
            }

            CapitalObjectiveObservation observation = _invasionCapitalObjectiveWorld.Observe(
                invasion.ClanId, invasion.CapitalSettlementId, null, plan.CommanderPartyId);

            if (observation.OwnsDesignatedCapital)
            {
                return NoChange(invasion);
            }

            SiegeTargetSelection? target = FindViableTarget(invasion, requirements, observation,
                plan.CommanderPartyId, null, out IReadOnlyList<SiegeTargetFacts> targetFacts);

            if (target == null)
            {
                return new CapitalObjectiveUpdate(CapitalObjectiveDecision.None,
                    invasion.CapitalObjective?.TargetSettlementId,
                    new CapitalObjectiveEvaluation(plan.CommanderPartyId,
                        observation.AttackForces, targetFacts));
            }

            objective.RestartAttackForMobilization(target.Value.SettlementId,
                observation.NowDays);

            return new CapitalObjectiveUpdate(CapitalObjectiveDecision.Retarget,
                target.Value.SettlementId);
        }

        private CapitalObjectiveDecision ReconcileCapitalOwnership(Invasion invasion,
            string capitalSettlementId, CapitalObjectiveObservation observation)
        {
            InvasionCapitalObjective? objective = invasion.CapitalObjective;

            if (!observation.OwnsDesignatedCapital)
            {
                if (objective?.Kind != InvasionCapitalObjectiveKind.Attack ||
                    objective.DefenceSiegeEpisodeId != null)
                {
                    invasion.BeginCapitalAttack(capitalSettlementId, observation.NowDays);
                    return CapitalObjectiveDecision.Gather;
                }

                return CapitalObjectiveDecision.None;
            }

            if (observation.DesignatedCapitalIsBesieged)
            {
                bool sameDefenceEpisode = objective?.DefenceSiegeEpisodeId != null &&
                    objective.DefenceSiegeEpisodeId == observation.DesignatedCapitalSiegeEpisodeId;
                bool sameLegacyActiveDefence = objective?.Kind ==
                        InvasionCapitalObjectiveKind.Defence &&
                    objective.TargetSettlementId == capitalSettlementId &&
                    objective.DefenceSiegeEpisodeId == observation.DesignatedCapitalSiegeEpisodeId;

                if (sameDefenceEpisode || sameLegacyActiveDefence)
                {
                    return CapitalObjectiveDecision.None;
                }

                invasion.BeginCapitalDefence(capitalSettlementId, observation.NowDays,
                    observation.DesignatedCapitalSiegeEpisodeId);
                return CapitalObjectiveDecision.Gather;
            }

            if (objective != null)
            {
                invasion.CompleteCapitalObjective();
                return CapitalObjectiveDecision.Complete;
            }

            return CapitalObjectiveDecision.None;
        }

        private CapitalArmyMobilizationResult TryMobilize(Invasion invasion,
            InvasionCapitalObjective objective, double nowDays)
        {
            IReadOnlyList<CapitalArmyPartyCandidate> candidates =
                _invasionCapitalObjectiveWorld.FindMobilizationCandidates(invasion.ClanId);
            CapitalArmyMobilizationPlan? plan = _capitalArmyMobilizationPlanner.Create(
                invasion.ClanId, objective.Kind, objective.TargetSettlementId!, candidates);

            if (plan == null)
            {
                return new CapitalArmyMobilizationResult(false, null, null, null,
                    candidates.Where(candidate => !candidate.IsMobilizable)
                        .Select(candidate => new CapitalArmyPartyDeferral(candidate.PartyId,
                            candidate.DeferredReason!))
                        .ToArray(), "no_mobilizable_commander");
            }

            CapitalArmyMobilizationResult result =
                _invasionCapitalObjectiveWorld.MobilizeArmy(plan);

            if (!result.Succeeded)
            {
                return result;
            }

            objective.MarkMobilized(result, nowDays);
            return result;
        }

        private CapitalObjectiveUpdate? EvaluateGathering(Invasion invasion,
            InvasionAssaultRequirements requirements, CapitalObjectiveObservation observation)
        {
            InvasionCapitalObjective? objective = invasion.CapitalObjective;

            if (objective?.Phase != InvasionCapitalObjectivePhase.Gathering)
            {
                return null;
            }

            bool ready = objective.Kind == InvasionCapitalObjectiveKind.Defence
                ? _capitalObjectiveStrengthRules.CanDefend(observation.DefenceForces,
                    requirements.MinimumRoamingRatio)
                : _capitalObjectiveStrengthRules.CanAttack(observation.AttackForces,
                    requirements.MinimumTargetRatio, requirements.MinimumRoamingRatio);

            if (ready && objective.CommanderPartyId != null)
            {
                string commanderPartyId = objective.CommanderPartyId;
                invasion.AdvanceCapitalObjective();
                _invasionCapitalObjectiveWorld.BeginAdvance(commanderPartyId);
                return new CapitalObjectiveUpdate(CapitalObjectiveDecision.Advance,
                    objective.TargetSettlementId);
            }

            if (objective.Kind == InvasionCapitalObjectiveKind.Defence)
            {
                IReadOnlyList<CapitalArmyPartyCandidate> candidates =
                    _invasionCapitalObjectiveWorld.FindMobilizationCandidates(invasion.ClanId);
                string[] reinforcements = candidates
                    .Where(candidate => candidate.IsMobilizable &&
                                        !objective.MobilizedPartyIds.Contains(candidate.PartyId))
                    .Select(candidate => candidate.PartyId)
                    .Distinct()
                    .ToArray();
                IReadOnlyList<string> invited = _invasionCapitalObjectiveWorld.InviteToArmy(
                    invasion.ClanId, objective.CommanderPartyId!, reinforcements);
                if (invited.Count > 0)
                {
                    objective.IncludeMobilizedParties(invited);
                    return new CapitalObjectiveUpdate(CapitalObjectiveDecision.Gather,
                        objective.TargetSettlementId, mobilizationResult:
                        new CapitalArmyMobilizationResult(true, objective.CommanderPartyId,
                            objective.MobilizedPartyIds, objective.StagingSettlementId,
                            candidates.Where(candidate => !candidate.IsMobilizable)
                                .Select(candidate => new CapitalArmyPartyDeferral(candidate.PartyId,
                                    candidate.DeferredReason!)).ToArray()));
                }

                return null;
            }

            if (!objective.IsGatherDeadlineReached(observation.NowDays))
            {
                return null;
            }

            SiegeTargetSelection? target = objective.CommanderPartyId == null
                ? null
                : FindViableTarget(invasion, requirements, observation,
                    objective.CommanderPartyId, objective.TargetSettlementId, out _);

            if (target == null)
            {
                invasion.WaitForViableCapitalTarget();
                return new CapitalObjectiveUpdate(CapitalObjectiveDecision.AbandonNoTarget, null);
            }

            string commanderId = objective.CommanderPartyId!;
            invasion.RetargetCapitalAttack(target.Value.SettlementId, observation.NowDays);
            _invasionCapitalObjectiveWorld.BeginAdvance(commanderId);

            return new CapitalObjectiveUpdate(CapitalObjectiveDecision.Retarget,
                target.Value.SettlementId);
        }

        private SiegeTargetSelection? FindViableTarget(Invasion invasion,
            InvasionAssaultRequirements requirements, CapitalObjectiveObservation observation,
            string commanderPartyId, string? excludedSettlementId,
            out IReadOnlyList<SiegeTargetFacts> candidates)
        {
            candidates = _invasionCapitalObjectiveWorld.FindTargetFacts(invasion.ClanId,
                commanderPartyId, requirements, excludedSettlementId);
            var criteria = new SiegeTargetCriteria(requirements.TargetKinds, excludedSettlementId,
                observation.AttackForces.AssembledFieldStrength, requirements.MinimumTargetRatio,
                requirements.MinimumRoamingRatio, SiegeTargetSelectionMode.RequireViableNow);

            return _siegeTargetSelector.FindClosest(candidates, criteria);
        }

        private CapitalObjectiveUpdate CompleteCapturedTarget(Invasion invasion,
            CapitalObjectiveObservation observation)
        {
            string targetSettlementId = invasion.CapitalObjective!.TargetSettlementId!;

            if (!observation.OwnsDesignatedCapital)
            {
                _invasionCapitalObjectiveWorld.RelocateCapitalAfterObjectiveCapture(invasion.ClanId,
                    targetSettlementId);
                invasion.RelocateCapitalAfterObjectiveCapture(targetSettlementId);
            }
            else
            {
                invasion.CompleteCapitalObjective();
            }

            return new CapitalObjectiveUpdate(CapitalObjectiveDecision.Complete,
                targetSettlementId);
        }

        private static CapitalObjectiveUpdate TimeOutAttack(Invasion invasion)
        {
            invasion.TimeOutCapitalAttack();
            return new CapitalObjectiveUpdate(CapitalObjectiveDecision.AbandonTimeout, null);
        }

        private static CapitalObjectiveUpdate NoChange(Invasion invasion) =>
            new(CapitalObjectiveDecision.None, invasion.CapitalObjective?.TargetSettlementId);
    }
}
