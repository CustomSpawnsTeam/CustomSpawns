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
        IReadOnlyList<string> InviteToArmy(string invasionId, string commanderPartyId,
            IReadOnlyList<string> partyIds);
        void BeginAdvance(string commanderPartyId);
        IReadOnlyList<CapitalSuccessorFacts> FindHeldFiefs(string invasionId, string lostCapitalSettlementId);

        void RelocateCapital(string invasionId, string settlementId);
    }
}
