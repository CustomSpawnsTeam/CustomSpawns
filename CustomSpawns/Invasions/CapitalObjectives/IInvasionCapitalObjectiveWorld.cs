using System.Collections.Generic;
using CustomSpawns.AI.Offensives;

namespace CustomSpawns.Invasions.CapitalObjectives
{
    public interface IInvasionCapitalObjectiveWorld
    {
        CapitalObjectiveObservation Observe(string invasionId, string designatedCapitalSettlementId,
            string? targetSettlementId, string? commanderPartyId);

        IReadOnlyList<CapitalObjectiveCommanderCandidate> FindCommanderCandidates(string invasionId);

        IReadOnlyList<SiegeTargetFacts> FindTargetFacts(string invasionId, string commanderPartyId,
            InvasionAssaultRequirements requirements, string? excludedSettlementId);

        void BeginGathering(string commanderPartyId, string targetSettlementId);
        void BeginAdvance(string commanderPartyId);
        void RelocateCapitalAfterObjectiveCapture(string invasionId, string settlementId);
    }
}
