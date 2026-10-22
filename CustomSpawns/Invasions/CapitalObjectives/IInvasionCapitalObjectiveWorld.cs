using System.Collections.Generic;
using CustomSpawns.AI.Offensives;

namespace CustomSpawns.Invasions.CapitalObjectives
{
    public interface IInvasionCapitalObjectiveWorld
    {
        CapitalObjectiveObservation Observe(string invasionId, string designatedCapitalSettlementId,
            string? targetSettlementId, string? commanderPartyId);

        IReadOnlyList<CapitalArmyPartyCandidate> FindMobilizationCandidates(string invasionId);

        IReadOnlyList<SiegeTargetFacts> FindTargetFacts(string invasionId, string commanderPartyId,
            InvasionAssaultRequirements requirements, string? excludedSettlementId);

        CapitalArmyMobilizationResult MobilizeArmy(CapitalArmyMobilizationPlan plan);
        void BeginAdvance(string commanderPartyId);
        void RelocateCapitalAfterObjectiveCapture(string invasionId, string settlementId);
    }
}
