using CustomSpawns.Invasions.CapitalObjectives;

namespace CustomSpawns.Invasions.Adapters
{
    public enum InvasionCapitalObjectiveDiagnosticsSource
    {
        Current = 0,
        Previous = 1,
        Evaluation = 2
    }

    public sealed class InvasionCapitalObjectiveLogPolicy
    {
        public InvasionCapitalObjectiveDiagnosticsSource SelectDiagnostics(bool hasEvaluation,
            bool hasCurrentObjective,
            string? previousCommanderPartyId, string? currentCommanderPartyId)
        {
            if (hasEvaluation)
            {
                return InvasionCapitalObjectiveDiagnosticsSource.Evaluation;
            }

            return !hasCurrentObjective ||
                   previousCommanderPartyId != null && currentCommanderPartyId == null
                ? InvasionCapitalObjectiveDiagnosticsSource.Previous
                : InvasionCapitalObjectiveDiagnosticsSource.Current;
        }

        public bool IncludesDailyNoChange(InvasionCapitalObjectivePhase? phase,
            InvasionCapitalObjectiveDormantReason? dormantReason)
        {
            return phase.HasValue;
        }
    }
}
