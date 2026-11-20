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
        private readonly InvasionCapitalSuccession _invasionCapitalSuccession;

        public InvasionCapitalObjectiveCoordinator(
            IInvasionAssaultConfigurationProvider invasionAssaultConfigurationProvider,
            IInvasionCapitalObjectiveWorld invasionCapitalObjectiveWorld,
            CapitalObjectiveStrengthRules capitalObjectiveStrengthRules,
            CapitalArmyMobilizationPlanner capitalArmyMobilizationPlanner,
            SiegeTargetSelector siegeTargetSelector, InvasionCapitalSuccession invasionCapitalSuccession)
        {
            _invasionAssaultConfigurationProvider = invasionAssaultConfigurationProvider;
            _invasionCapitalObjectiveWorld = invasionCapitalObjectiveWorld;
            _capitalObjectiveStrengthRules = capitalObjectiveStrengthRules;
            _capitalArmyMobilizationPlanner = capitalArmyMobilizationPlanner;
            _siegeTargetSelector = siegeTargetSelector;
            _invasionCapitalSuccession = invasionCapitalSuccession;
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
                capitalSettlementId, observation, requirements);

            if (ownershipDecision == CapitalObjectiveDecision.CapitalRelocated)
            {
                return new CapitalObjectiveUpdate(ownershipDecision, invasion.CapitalSettlementId);
            }

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
                objective?.Phase != InvasionCapitalObjectivePhase.Dormant)
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

            // A fief fit to be the capital may have been taken since the lost one was given up on.
            if (requirements.RelocatesCapitalOnLoss && !_invasionCapitalObjectiveWorld.Observe(invasion.ClanId,
                    invasion.CapitalSettlementId, null, null).OwnsDesignatedCapital &&
                RelocateLostCapital(invasion, invasion.CapitalSettlementId, requirements))
            {
                return new CapitalObjectiveUpdate(CapitalObjectiveDecision.CapitalRelocated,
                    invasion.CapitalSettlementId);
            }

            IReadOnlyList<CapitalArmyPartyCandidate> candidates =
                _invasionCapitalObjectiveWorld.FindMobilizationCandidates(invasion.ClanId);
            CapitalArmyMobilizationPlan? plan = _capitalArmyMobilizationPlanner.Create(
                invasion.ClanId, InvasionCapitalObjectiveKind.Attack,
                invasion.CapitalSettlementId, candidates);

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

            // A dormant invasion has no army yet: it is judged by the army it would raise, not by the lord who
            // would lead it, or it never raises one.
            SiegeTargetSelection? target = FindViableTarget(invasion, requirements,
                FindPlannedStrength(plan, candidates), plan.CommanderPartyId, null,
                out IReadOnlyList<SiegeTargetFacts> targetFacts);

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
            string capitalSettlementId, CapitalObjectiveObservation observation,
            InvasionAssaultRequirements requirements)
        {
            InvasionCapitalObjective? objective = invasion.CapitalObjective;

            if (!observation.OwnsDesignatedCapital)
            {
                if (objective?.Kind != InvasionCapitalObjectiveKind.Attack ||
                    objective.DefenceSiegeEpisodeId != null)
                {
                    if (RelocateLostCapital(invasion, capitalSettlementId, requirements))
                    {
                        return CapitalObjectiveDecision.CapitalRelocated;
                    }

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

            if (objective.Kind == InvasionCapitalObjectiveKind.Defence ||
                !objective.IsGatherDeadlineReached(observation.NowDays))
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

            SiegeTargetSelection? target = objective.CommanderPartyId == null
                ? null
                : FindViableTarget(invasion, requirements, observation.AttackForces.AssembledFieldStrength,
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

        private static float FindPlannedStrength(CapitalArmyMobilizationPlan plan,
            IReadOnlyList<CapitalArmyPartyCandidate> candidates)
        {
            var roster = new HashSet<string>(plan.PartyIds);

            return candidates.Where(candidate => roster.Remove(candidate.PartyId))
                .Sum(candidate => candidate.Strength);
        }

        private SiegeTargetSelection? FindViableTarget(Invasion invasion,
            InvasionAssaultRequirements requirements, float assembledStrength,
            string commanderPartyId, string? excludedSettlementId,
            out IReadOnlyList<SiegeTargetFacts> candidates)
        {
            candidates = _invasionCapitalObjectiveWorld.FindTargetFacts(invasion.ClanId,
                commanderPartyId, requirements, excludedSettlementId);
            var criteria = new SiegeTargetCriteria(requirements.TargetKinds, excludedSettlementId,
                assembledStrength, requirements.MinimumTargetRatio,
                requirements.MinimumRoamingRatio, SiegeTargetSelectionMode.RequireViableNow);

            return _siegeTargetSelector.FindClosest(candidates, criteria);
        }

        // Moves the capital to a fief fit to take its place, when so configured, instead of setting out to retake it.
        private bool RelocateLostCapital(Invasion invasion, string lostCapitalSettlementId,
            InvasionAssaultRequirements requirements)
        {
            if (!requirements.RelocatesCapitalOnLoss)
            {
                return false;
            }

            string? successor = _invasionCapitalSuccession.FindSuccessor(requirements,
                _invasionCapitalObjectiveWorld.FindHeldFiefs(invasion.ClanId, lostCapitalSettlementId));

            if (successor == null)
            {
                return false;
            }

            _invasionCapitalObjectiveWorld.RelocateCapital(invasion.ClanId, successor);
            invasion.RelocateCapital(successor);
            return true;
        }

        private CapitalObjectiveUpdate CompleteCapturedTarget(Invasion invasion,
            CapitalObjectiveObservation observation)
        {
            string targetSettlementId = invasion.CapitalObjective!.TargetSettlementId!;

            if (!observation.OwnsDesignatedCapital)
            {
                _invasionCapitalObjectiveWorld.RelocateCapital(invasion.ClanId, targetSettlementId);
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
