using System;
using CustomSpawns.Invasions;
using CustomSpawns.Invasions.CapitalObjectives;
using NUnit.Framework;

namespace CustomSpawns.Tests.Invasions.CapitalObjectives
{
    public class InvasionCapitalObjectiveTests
    {
        [Test]
        public void DefenceStartsWithOnlyAThreeDayGatheringDeadline()
        {
            var objective = new InvasionCapitalObjective(InvasionCapitalObjectiveKind.Defence,
                "capital", 10d);

            Assert.That(objective.Phase, Is.EqualTo(InvasionCapitalObjectivePhase.Gathering));
            Assert.That(objective.GatherDeadlineDays, Is.EqualTo(13d));
            Assert.That(objective.AttackDeadlineDays, Is.Null);
            Assert.That(objective.IsGatherDeadlineReached(12.999d), Is.False);
            Assert.That(objective.IsGatherDeadlineReached(13d), Is.True);
        }

        [Test]
        public void AttackStartsWithThreeAndSevenDayDeadlines()
        {
            var objective = new InvasionCapitalObjective(InvasionCapitalObjectiveKind.Attack,
                "capital", 10d);

            Assert.That(objective.GatherDeadlineDays, Is.EqualTo(13d));
            Assert.That(objective.AttackDeadlineDays, Is.EqualTo(17d));

            objective.MarkReady();

            Assert.That(objective.Phase, Is.EqualTo(InvasionCapitalObjectivePhase.Advancing));
            Assert.That(objective.GatherDeadlineDays, Is.Null);
            Assert.That(objective.IsAttackDeadlineReached(16.999d), Is.False);
            Assert.That(objective.IsAttackDeadlineReached(17d), Is.True);
        }

        [Test]
        public void RetargetStartsAnImmediateAttackWithAFreshSevenDayDeadline()
        {
            var objective = new InvasionCapitalObjective(InvasionCapitalObjectiveKind.Defence,
                "capital", 10d);
            objective.AssignCommander("commander");

            objective.RetargetForAttack("alternative", 13d);

            Assert.That(objective.Kind, Is.EqualTo(InvasionCapitalObjectiveKind.Attack));
            Assert.That(objective.Phase, Is.EqualTo(InvasionCapitalObjectivePhase.Advancing));
            Assert.That(objective.TargetSettlementId, Is.EqualTo("alternative"));
            Assert.That(objective.GatherDeadlineDays, Is.Null);
            Assert.That(objective.AttackDeadlineDays, Is.EqualTo(20d));
            Assert.That(objective.CommanderPartyId, Is.EqualTo("commander"));
        }

        [Test]
        public void RetargetedDefencePreservesItsSiegeEpisodeIdentity()
        {
            var objective = new InvasionCapitalObjective(InvasionCapitalObjectiveKind.Defence,
                "capital", 10d, "siege-a");
            objective.AssignCommander("commander");

            objective.RetargetForAttack("alternative", 13d);

            Assert.That(objective.Kind, Is.EqualTo(InvasionCapitalObjectiveKind.Attack));
            Assert.That(objective.DefenceSiegeEpisodeId, Is.EqualTo("siege-a"));
        }

        [Test]
        public void ReleasedDefencePreservesItsKindAndSiegeEpisodeIdentity()
        {
            var objective = new InvasionCapitalObjective(InvasionCapitalObjectiveKind.Defence,
                "capital", 10d, "siege-a");

            objective.WaitForViableTarget();

            Assert.That(objective.Kind, Is.EqualTo(InvasionCapitalObjectiveKind.Defence));
            Assert.That(objective.Phase, Is.EqualTo(InvasionCapitalObjectivePhase.Dormant));
            Assert.That(objective.DefenceSiegeEpisodeId, Is.EqualTo("siege-a"));
        }

        [Test]
        public void DormantStatesHaveNoTargetCommanderOrDeadline()
        {
            var noTarget = new InvasionCapitalObjective(InvasionCapitalObjectiveKind.Attack,
                "capital", 0d);
            noTarget.AssignCommander("commander");
            noTarget.WaitForViableTarget();

            Assert.That(noTarget.Phase, Is.EqualTo(InvasionCapitalObjectivePhase.Dormant));
            Assert.That(noTarget.DormantReason,
                Is.EqualTo(InvasionCapitalObjectiveDormantReason.NoViableTarget));
            Assert.That(noTarget.TargetSettlementId, Is.Null);
            Assert.That(noTarget.CommanderPartyId, Is.Null);
            Assert.That(noTarget.GatherDeadlineDays, Is.Null);
            Assert.That(noTarget.AttackDeadlineDays, Is.Null);

            var timedOut = new InvasionCapitalObjective(InvasionCapitalObjectiveKind.Attack,
                "capital", 0d);
            timedOut.MarkReady();
            timedOut.TimeOutAttack();

            Assert.That(timedOut.DormantReason,
                Is.EqualTo(InvasionCapitalObjectiveDormantReason.AttackTimedOut));
        }

        [TestCase(InvasionCapitalObjectiveKind.Defence, InvasionCapitalObjectivePhase.Besieging,
            "capital", null, null, InvasionCapitalObjectiveDormantReason.None)]
        [TestCase(InvasionCapitalObjectiveKind.Attack, InvasionCapitalObjectivePhase.Dormant,
            "capital", null, null, InvasionCapitalObjectiveDormantReason.NoViableTarget)]
        [TestCase(InvasionCapitalObjectiveKind.Attack, InvasionCapitalObjectivePhase.Advancing,
            "capital", null, null, InvasionCapitalObjectiveDormantReason.None)]
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
    }
}
