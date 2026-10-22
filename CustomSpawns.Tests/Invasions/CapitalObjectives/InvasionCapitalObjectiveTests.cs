using System;
using CustomSpawns.Invasions;
using CustomSpawns.Invasions.CapitalObjectives;
using NUnit.Framework;

namespace CustomSpawns.Tests.Invasions.CapitalObjectives
{
    public class InvasionCapitalObjectiveTests
    {
        [Test]
        public void NewObjectiveMobilizesBeforeStartingTheGatherClock()
        {
            var objective = new InvasionCapitalObjective(InvasionCapitalObjectiveKind.Defence,
                "capital", 10d);

            Assert.That(objective.Phase, Is.EqualTo(InvasionCapitalObjectivePhase.Mobilizing));
            Assert.That(objective.GatherDeadlineDays, Is.Null);
            Assert.That(objective.CommanderPartyId, Is.Null);
            Assert.That(objective.StagingSettlementId, Is.Null);
            Assert.That(objective.MobilizedPartyIds, Is.Empty);
        }

        [Test]
        public void SuccessfulMobilizationStartsThreeDayGatheringClock()
        {
            var objective = new InvasionCapitalObjective(InvasionCapitalObjectiveKind.Defence,
                "capital", 10d);

            objective.MarkMobilized(Success("leader", "staging", "leader", "party-b"), 12d);

            Assert.That(objective.Phase, Is.EqualTo(InvasionCapitalObjectivePhase.Gathering));
            Assert.That(objective.GatherDeadlineDays, Is.EqualTo(15d));
            Assert.That(objective.CommanderPartyId, Is.EqualTo("leader"));
            Assert.That(objective.StagingSettlementId, Is.EqualTo("staging"));
            Assert.That(objective.MobilizedPartyIds, Is.EqualTo(new[] { "leader", "party-b" }));
        }

        [Test]
        public void AttackDeadlineStartsWithTheObjectiveAndSurvivesMobilization()
        {
            var objective = new InvasionCapitalObjective(InvasionCapitalObjectiveKind.Attack,
                "capital", 10d);

            objective.MarkMobilized(Success("leader", "staging", "leader"), 12d);
            objective.MarkReady();

            Assert.That(objective.AttackDeadlineDays, Is.EqualTo(17d));
            Assert.That(objective.IsAttackDeadlineReached(16.999d), Is.False);
            Assert.That(objective.IsAttackDeadlineReached(17d), Is.True);
        }

        [Test]
        public void AttackDeadlineCoversMobilizingAndGathering()
        {
            var mobilizing = new InvasionCapitalObjective(InvasionCapitalObjectiveKind.Attack,
                "capital", 10d);
            var gathering = new InvasionCapitalObjective(InvasionCapitalObjectiveKind.Attack,
                "capital", 10d);
            gathering.MarkMobilized(Success("leader", "staging", "leader"), 11d);

            Assert.That(mobilizing.IsAttackDeadlineReached(17d), Is.True);
            Assert.That(gathering.IsAttackDeadlineReached(17d), Is.True);

            mobilizing.TimeOutAttack();
            gathering.TimeOutAttack();

            Assert.That(mobilizing.DormantReason,
                Is.EqualTo(InvasionCapitalObjectiveDormantReason.AttackTimedOut));
            Assert.That(gathering.DormantReason,
                Is.EqualTo(InvasionCapitalObjectiveDormantReason.AttackTimedOut));
        }

        [Test]
        public void ObservedSiegeCanBeginBeforeArmyMobilization()
        {
            var objective = new InvasionCapitalObjective(InvasionCapitalObjectiveKind.Attack,
                "capital", 10d);

            objective.MarkBesieging();

            Assert.That(objective.Phase, Is.EqualTo(InvasionCapitalObjectivePhase.Besieging));
            Assert.That(objective.AttackDeadlineDays, Is.EqualTo(17d));
            Assert.That(objective.CommanderPartyId, Is.Null);
            Assert.That(objective.MobilizedPartyIds, Is.Empty);
        }

        [Test]
        public void ObservedSiegeClearsTheGatherDeadline()
        {
            var objective = new InvasionCapitalObjective(InvasionCapitalObjectiveKind.Attack,
                "capital", 10d);
            objective.MarkMobilized(Success("leader", "staging", "leader"), 10d);

            objective.MarkBesieging();

            Assert.That(objective.Phase, Is.EqualTo(InvasionCapitalObjectivePhase.Besieging));
            Assert.That(objective.GatherDeadlineDays, Is.Null);
            Assert.That(objective.CommanderPartyId, Is.EqualTo("leader"));
        }

        [Test]
        public void FailedMobilizationCannotBeginGathering()
        {
            var objective = new InvasionCapitalObjective(InvasionCapitalObjectiveKind.Defence,
                "capital", 10d);
            var failed = new CapitalArmyMobilizationResult(false, null, null, null,
                failureReason: "native_creation_failed");

            Assert.Throws<ArgumentException>(() => objective.MarkMobilized(failed, 12d));
            Assert.That(objective.Phase, Is.EqualTo(InvasionCapitalObjectivePhase.Mobilizing));
            Assert.That(objective.GatherDeadlineDays, Is.Null);
        }

        [Test]
        public void FragmentedAttackRemobilizesWithoutResettingItsAttackDeadline()
        {
            var objective = new InvasionCapitalObjective(InvasionCapitalObjectiveKind.Attack,
                "capital", 10d);
            objective.MarkMobilized(Success("leader", "staging", "leader", "party-b"), 10d);
            objective.MarkReady();

            objective.RequireRemobilization();

            Assert.That(objective.Phase, Is.EqualTo(InvasionCapitalObjectivePhase.Mobilizing));
            Assert.That(objective.AttackDeadlineDays, Is.EqualTo(17d));
            Assert.That(objective.GatherDeadlineDays, Is.Null);
            Assert.That(objective.CommanderPartyId, Is.Null);
            Assert.That(objective.MobilizedPartyIds, Is.Empty);
        }

        [Test]
        public void RetargetKeepsUnifiedArmyAndStartsAFreshSevenDayDeadline()
        {
            var objective = new InvasionCapitalObjective(InvasionCapitalObjectiveKind.Attack,
                "capital", 10d);
            objective.MarkMobilized(Success("leader", "staging", "leader", "party-b"), 10d);

            objective.RetargetForAttack("alternative", 13d);

            Assert.That(objective.Phase, Is.EqualTo(InvasionCapitalObjectivePhase.Advancing));
            Assert.That(objective.TargetSettlementId, Is.EqualTo("alternative"));
            Assert.That(objective.AttackDeadlineDays, Is.EqualTo(20d));
            Assert.That(objective.CommanderPartyId, Is.EqualTo("leader"));
            Assert.That(objective.MobilizedPartyIds, Is.EqualTo(new[] { "leader", "party-b" }));
        }

        [Test]
        public void OldGatheringSaveWithoutUnifiedArmyMetadataIsRemobilized()
        {
            var restored = new InvasionCapitalObjective(InvasionCapitalObjectiveKind.Attack,
                InvasionCapitalObjectivePhase.Gathering, "capital", 13d, 17d,
                InvasionCapitalObjectiveDormantReason.None, "old-leader");

            Assert.That(restored.Phase, Is.EqualTo(InvasionCapitalObjectivePhase.Mobilizing));
            Assert.That(restored.GatherDeadlineDays, Is.Null);
            Assert.That(restored.AttackDeadlineDays, Is.EqualTo(17d));
            Assert.That(restored.CommanderPartyId, Is.Null);
        }

        [Test]
        public void ReconstructingNormalizedLegacyStateDoesNotChangeItAgain()
        {
            var restored = new InvasionCapitalObjective(InvasionCapitalObjectiveKind.Attack,
                InvasionCapitalObjectivePhase.Gathering, "capital", 13d, 17d,
                InvasionCapitalObjectiveDormantReason.None, "old-leader");

            var reconstructed = new InvasionCapitalObjective(restored.Kind, restored.Phase,
                restored.TargetSettlementId, restored.GatherDeadlineDays,
                restored.AttackDeadlineDays, restored.DormantReason,
                restored.CommanderPartyId, restored.DefenceSiegeEpisodeId,
                restored.StagingSettlementId, restored.MobilizedPartyIds);

            Assert.That(reconstructed.Phase,
                Is.EqualTo(InvasionCapitalObjectivePhase.Mobilizing));
            Assert.That(reconstructed.TargetSettlementId, Is.EqualTo("capital"));
            Assert.That(reconstructed.GatherDeadlineDays, Is.Null);
            Assert.That(reconstructed.AttackDeadlineDays, Is.EqualTo(17d));
            Assert.That(reconstructed.CommanderPartyId, Is.Null);
            Assert.That(reconstructed.StagingSettlementId, Is.Null);
            Assert.That(reconstructed.MobilizedPartyIds, Is.Empty);
        }

        [Test]
        public void EndedSiegeBecomesDormantAndCannotRestartHourly()
        {
            var objective = new InvasionCapitalObjective(InvasionCapitalObjectiveKind.Attack,
                "capital", 10d);
            objective.MarkMobilized(Success("leader", "staging", "leader"), 10d);
            objective.MarkReady();
            objective.MarkBesieging();

            Assert.That(objective.AttackDeadlineDays, Is.EqualTo(17d));

            objective.AbortSiege();

            Assert.That(objective.Phase, Is.EqualTo(InvasionCapitalObjectivePhase.Dormant));
            Assert.That(objective.DormantReason,
                Is.EqualTo(InvasionCapitalObjectiveDormantReason.SiegeAborted));
            Assert.That(objective.TargetSettlementId, Is.Null);
            Assert.That(objective.CommanderPartyId, Is.Null);
        }

        [Test]
        public void DormantStatesHaveNoTargetCommanderArmyOrDeadline()
        {
            var noTarget = new InvasionCapitalObjective(InvasionCapitalObjectiveKind.Attack,
                "capital", 0d);
            noTarget.WaitForViableTarget();

            Assert.That(noTarget.Phase, Is.EqualTo(InvasionCapitalObjectivePhase.Dormant));
            Assert.That(noTarget.DormantReason,
                Is.EqualTo(InvasionCapitalObjectiveDormantReason.NoViableTarget));
            Assert.That(noTarget.TargetSettlementId, Is.Null);
            Assert.That(noTarget.CommanderPartyId, Is.Null);
            Assert.That(noTarget.StagingSettlementId, Is.Null);
            Assert.That(noTarget.MobilizedPartyIds, Is.Empty);
            Assert.That(noTarget.GatherDeadlineDays, Is.Null);
            Assert.That(noTarget.AttackDeadlineDays, Is.Null);
        }

        [TestCase(InvasionCapitalObjectiveKind.Defence, InvasionCapitalObjectivePhase.Besieging,
            "capital", null, null, InvasionCapitalObjectiveDormantReason.None)]
        [TestCase(InvasionCapitalObjectiveKind.Attack, InvasionCapitalObjectivePhase.Dormant,
            "capital", null, null, InvasionCapitalObjectiveDormantReason.NoViableTarget)]
        public void RestoreConstructorRejectsImpossibleStates(InvasionCapitalObjectiveKind kind,
            InvasionCapitalObjectivePhase phase, string? target, double? gatherDeadline,
            double? attackDeadline, InvasionCapitalObjectiveDormantReason dormantReason)
        {
            Assert.Throws<ArgumentException>(() => new InvasionCapitalObjective(kind, phase, target,
                gatherDeadline, attackDeadline, dormantReason, null));
        }

        [Test]
        public void ReobservingTheSameAttackDoesNotResetItsDeadline()
        {
            var invasion = new Invasion("template", "clan");
            invasion.BeginCapitalAttack("capital", 10d);

            invasion.BeginCapitalAttack("capital", 12d);

            Assert.That(invasion.CapitalObjective!.AttackDeadlineDays, Is.EqualTo(17d));
        }

        private static CapitalArmyMobilizationResult Success(string commanderPartyId,
            string stagingSettlementId, params string[] partyIds) =>
            new(true, commanderPartyId, partyIds, stagingSettlementId);
    }
}
