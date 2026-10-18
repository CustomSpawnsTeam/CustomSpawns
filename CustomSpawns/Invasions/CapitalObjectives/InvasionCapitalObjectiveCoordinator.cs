using System.Collections.Generic;
using CustomSpawns.AI.Offensives;

namespace CustomSpawns.Invasions.CapitalObjectives
{
    public sealed class InvasionCapitalObjectiveCoordinator
    {
        private readonly IInvasionAssaultConfigurationProvider _invasionAssaultConfigurationProvider;
        private readonly IInvasionCapitalObjectiveWorld _invasionCapitalObjectiveWorld;
        private readonly CapitalObjectiveStrengthRules _capitalObjectiveStrengthRules;
        private readonly CapitalObjectiveCommanderSelector _capitalObjectiveCommanderSelector;
        private readonly SiegeTargetSelector _siegeTargetSelector;

        public InvasionCapitalObjectiveCoordinator(
            IInvasionAssaultConfigurationProvider invasionAssaultConfigurationProvider,
            IInvasionCapitalObjectiveWorld invasionCapitalObjectiveWorld,
            CapitalObjectiveStrengthRules capitalObjectiveStrengthRules,
            CapitalObjectiveCommanderSelector capitalObjectiveCommanderSelector,
            SiegeTargetSelector siegeTargetSelector)
        {
            _invasionAssaultConfigurationProvider = invasionAssaultConfigurationProvider;
            _invasionCapitalObjectiveWorld = invasionCapitalObjectiveWorld;
            _capitalObjectiveStrengthRules = capitalObjectiveStrengthRules;
            _capitalObjectiveCommanderSelector = capitalObjectiveCommanderSelector;
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

            if (objective.Phase == InvasionCapitalObjectivePhase.Gathering ||
                objective.Phase == InvasionCapitalObjectivePhase.Advancing)
            {
                EnsureCommander(invasion, objective, observation);
                objective = invasion.CapitalObjective!;
            }

            observation = _invasionCapitalObjectiveWorld.Observe(invasion.ClanId,
                capitalSettlementId, objective.TargetSettlementId, objective.CommanderPartyId);

            if (objective.Kind == InvasionCapitalObjectiveKind.Attack &&
                observation.TargetIsOwnedByInvasion)
            {
                return CompleteCapturedTarget(invasion, observation);
            }

            CapitalObjectiveUpdate? gathering = EvaluateGathering(invasion, requirements, observation);

            if (gathering != null)
            {
                return gathering.Value;
            }

            objective = invasion.CapitalObjective;

            if (objective?.Phase == InvasionCapitalObjectivePhase.Besieging)
            {
                if (!observation.TargetHasMatchingKingdomSiege)
                {
                    string? endedSiegeTarget = objective.TargetSettlementId;
                    invasion.CompleteCapitalObjective();
                    return new CapitalObjectiveUpdate(CapitalObjectiveDecision.Complete,
                        endedSiegeTarget);
                }

                return NoChange(invasion);
            }

            if (objective?.Kind == InvasionCapitalObjectiveKind.Attack &&
                objective.Phase == InvasionCapitalObjectivePhase.Advancing &&
                observation.TargetHasMatchingKingdomSiege)
            {
                string? target = objective.TargetSettlementId;
                invasion.MarkCapitalSiegeStarted();
                return new CapitalObjectiveUpdate(CapitalObjectiveDecision.SiegeStarted, target);
            }

            if (objective?.IsAttackDeadlineReached(observation.NowDays) == true)
            {
                invasion.TimeOutCapitalAttack();
                return new CapitalObjectiveUpdate(CapitalObjectiveDecision.AbandonTimeout, null);
            }

            return new CapitalObjectiveUpdate(ownershipDecision, objective?.TargetSettlementId);
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

            CapitalObjectiveCommanderCandidate? commander = _capitalObjectiveCommanderSelector.Find(
                _invasionCapitalObjectiveWorld.FindCommanderCandidates(invasion.ClanId));

            if (commander == null)
            {
                return NoChange(invasion);
            }

            CapitalObjectiveObservation observation = _invasionCapitalObjectiveWorld.Observe(
                invasion.ClanId, invasion.CapitalSettlementId, null, commander.Value.PartyId);

            if (observation.OwnsDesignatedCapital)
            {
                return NoChange(invasion);
            }

            SiegeTargetSelection? target = FindViableTarget(invasion, requirements, observation,
                commander.Value.PartyId, null, out IReadOnlyList<SiegeTargetFacts> targetFacts);

            if (target == null)
            {
                return new CapitalObjectiveUpdate(CapitalObjectiveDecision.None,
                    invasion.CapitalObjective?.TargetSettlementId,
                    new CapitalObjectiveEvaluation(commander.Value.PartyId,
                        observation.AttackForces, targetFacts));
            }

            invasion.RetargetCapitalAttack(target.Value.SettlementId, observation.NowDays);
            invasion.AssignCapitalObjectiveCommander(commander.Value.PartyId);
            _invasionCapitalObjectiveWorld.BeginAdvance(commander.Value.PartyId);

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

        private void EnsureCommander(Invasion invasion, InvasionCapitalObjective objective,
            CapitalObjectiveObservation observation)
        {
            if (objective.CommanderPartyId != null && observation.CommanderIsEligible)
            {
                return;
            }

            invasion.ClearCapitalObjectiveCommander();
            CapitalObjectiveCommanderCandidate? selected = _capitalObjectiveCommanderSelector.Find(
                _invasionCapitalObjectiveWorld.FindCommanderCandidates(invasion.ClanId));

            if (selected == null)
            {
                return;
            }

            invasion.AssignCapitalObjectiveCommander(selected.Value.PartyId);

            if (objective.Phase == InvasionCapitalObjectivePhase.Gathering)
            {
                _invasionCapitalObjectiveWorld.BeginGathering(selected.Value.PartyId,
                    objective.TargetSettlementId!);
            }
            else
            {
                _invasionCapitalObjectiveWorld.BeginAdvance(selected.Value.PartyId);
            }
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

        private static CapitalObjectiveUpdate NoChange(Invasion invasion) =>
            new(CapitalObjectiveDecision.None, invasion.CapitalObjective?.TargetSettlementId);
    }
}
