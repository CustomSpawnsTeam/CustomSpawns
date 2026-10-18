using CustomSpawns.Invasions.Adapters;
using CustomSpawns.Invasions.CapitalObjectives;
using NUnit.Framework;

namespace CustomSpawns.Tests.Invasions
{
    public class InvasionCapitalObjectiveLogPolicyTests
    {
        private readonly InvasionCapitalObjectiveLogPolicy _policy = new();

        [TestCase(false, true, "commander", null,
            InvasionCapitalObjectiveDiagnosticsSource.Previous)]
        [TestCase(false, false, "commander", null,
            InvasionCapitalObjectiveDiagnosticsSource.Previous)]
        [TestCase(false, true, "commander", "commander",
            InvasionCapitalObjectiveDiagnosticsSource.Current)]
        [TestCase(false, true, null, null,
            InvasionCapitalObjectiveDiagnosticsSource.Current)]
        [TestCase(true, true, null, null,
            InvasionCapitalObjectiveDiagnosticsSource.Evaluation)]
        public void DetailedDiagnosticsSelectTheRelevantStateSnapshot(bool hasEvaluation,
            bool hasCurrentObjective, string? previousCommanderPartyId,
            string? currentCommanderPartyId, InvasionCapitalObjectiveDiagnosticsSource expected)
        {
            Assert.That(_policy.SelectDiagnostics(hasEvaluation, hasCurrentObjective,
                previousCommanderPartyId, currentCommanderPartyId), Is.EqualTo(expected));
        }

        [TestCase(InvasionCapitalObjectivePhase.Dormant,
            InvasionCapitalObjectiveDormantReason.NoViableTarget, true)]
        [TestCase(InvasionCapitalObjectivePhase.Dormant,
            InvasionCapitalObjectiveDormantReason.AttackTimedOut, false)]
        [TestCase(InvasionCapitalObjectivePhase.Advancing,
            InvasionCapitalObjectiveDormantReason.None, false)]
        public void DailyNoChangeIsLoggedOnlyWhileWaitingForAViableTarget(
            InvasionCapitalObjectivePhase phase,
            InvasionCapitalObjectiveDormantReason dormantReason, bool expected)
        {
            Assert.That(_policy.IncludesDailyNoChange(phase, dormantReason),
                Is.EqualTo(expected));
        }
    }
}
