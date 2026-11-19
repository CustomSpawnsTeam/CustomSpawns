using System;
using System.Collections.Generic;
using System.Linq;
using CustomSpawns.AI.Offensives;

namespace CustomSpawns.Invasions.CapitalObjectives
{
    public enum CapitalObjectiveDecision
    {
        None = 0,
        Gather = 1,
        Advance = 2,
        Retarget = 3,
        AbandonNoTarget = 4,
        AbandonTimeout = 5,
        SiegeStarted = 6,
        Complete = 7,
        ConfigurationMissing = 8,
        AbandonSiegeAborted = 9,
        MobilizationFailed = 10,
        CapitalRelocated = 11
    }

    public readonly struct CapitalObjectiveUpdate
    {
        public CapitalObjectiveUpdate(CapitalObjectiveDecision decision, string? targetSettlementId,
            CapitalObjectiveEvaluation? evaluation = null,
            CapitalArmyMobilizationResult? mobilizationResult = null)
        {
            Decision = decision;
            TargetSettlementId = targetSettlementId;
            Evaluation = evaluation;
            MobilizationResult = mobilizationResult;
        }

        public CapitalObjectiveDecision Decision { get; }
        public string? TargetSettlementId { get; }
        public CapitalObjectiveEvaluation? Evaluation { get; }
        public CapitalArmyMobilizationResult? MobilizationResult { get; }
    }

    public sealed class CapitalObjectiveEvaluation
    {
        public CapitalObjectiveEvaluation(string commanderPartyId,
            CapitalAttackForces attackForces, IReadOnlyList<SiegeTargetFacts> targetFacts)
        {
            if (string.IsNullOrWhiteSpace(commanderPartyId))
            {
                throw new ArgumentException("A commander id is required.", nameof(commanderPartyId));
            }

            CommanderPartyId = commanderPartyId;
            AttackForces = attackForces;
            TargetFacts = targetFacts?.ToArray() ??
                          throw new ArgumentNullException(nameof(targetFacts));
        }

        public string CommanderPartyId { get; }
        public CapitalAttackForces AttackForces { get; }
        public IReadOnlyList<SiegeTargetFacts> TargetFacts { get; }
    }
}
