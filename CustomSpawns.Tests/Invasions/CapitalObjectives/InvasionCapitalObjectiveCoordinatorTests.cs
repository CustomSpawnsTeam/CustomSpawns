using System.Collections.Generic;
using CustomSpawns.AI.Offensives;
using CustomSpawns.Invasions;
using CustomSpawns.Invasions.CapitalObjectives;
using NUnit.Framework;

namespace CustomSpawns.Tests.Invasions.CapitalObjectives
{
    public class InvasionCapitalObjectiveCoordinatorTests
    {
        private Invasion _invasion = null!;
        private FakeWorld _world = null!;
        private InvasionCapitalObjectiveCoordinator _coordinator = null!;

        [SetUp]
        public void SetUp()
        {
            _invasion = new Invasion("template", "clan") { CapitalSettlementId = "capital" };
            _world = new FakeWorld();
            _world.Commanders.Add(new CapitalObjectiveCommanderCandidate("commander", 100f,
                true, false, true));

            var ratios = new ForceRatioRules();
            _coordinator = new InvasionCapitalObjectiveCoordinator(
                new FakeConfigurationProvider(Requirements()), _world,
                new CapitalObjectiveStrengthRules(ratios),
                new CapitalObjectiveCommanderSelector(), new SiegeTargetSelector(ratios));
        }

        [Test]
        public void ReadyDefenceAdvancesImmediatelyWithoutWaitingForCalledParties()
        {
            _world.Observation = Observation(1d, ownsCapital: true, capitalBesieged: true,
                commanderEligible: false, defence: new CapitalDefenceForces(40f, 20f, 50f));

            CapitalObjectiveUpdate update = _coordinator.UpdateHourly(_invasion);

            Assert.That(update.Decision, Is.EqualTo(CapitalObjectiveDecision.Advance));
            Assert.That(_invasion.CapitalObjective!.Kind,
                Is.EqualTo(InvasionCapitalObjectiveKind.Defence));
            Assert.That(_invasion.CapitalObjective.Phase,
                Is.EqualTo(InvasionCapitalObjectivePhase.Advancing));
            Assert.That(_world.GatheredCommander, Is.EqualTo("commander"));
            Assert.That(_world.AdvancedCommander, Is.EqualTo("commander"));
        }

        [Test]
        public void ExactThreeDayExpiryRetargetsToTheClosestViableAlternative()
        {
            _invasion.BeginCapitalAttack("capital", 0d);
            _invasion.AssignCapitalObjectiveCommander("commander");
            _world.Observation = Observation(3d, ownsCapital: false, capitalBesieged: false,
                commanderEligible: true, attack: new CapitalAttackForces(150f, 200f, 10f));
            _world.Targets.Add(Viable("far", 20f));
            _world.Targets.Add(Viable("near", 10f));

            CapitalObjectiveUpdate update = _coordinator.UpdateHourly(_invasion);

            Assert.That(update.Decision, Is.EqualTo(CapitalObjectiveDecision.Retarget));
            Assert.That(_world.ExcludedSettlementId, Is.EqualTo("capital"));
            Assert.That(_invasion.CapitalObjective!.TargetSettlementId, Is.EqualTo("near"));
            Assert.That(_invasion.CapitalObjective.Phase,
                Is.EqualTo(InvasionCapitalObjectivePhase.Advancing));
            Assert.That(_invasion.CapitalObjective.AttackDeadlineDays, Is.EqualTo(10d));
        }

        [Test]
        public void NoAlternativeAtThreeDaysReleasesTheForcedOrder()
        {
            _invasion.BeginCapitalAttack("capital", 0d);
            _invasion.AssignCapitalObjectiveCommander("commander");
            _world.Observation = Observation(3d, ownsCapital: false, capitalBesieged: false,
                commanderEligible: true, attack: new CapitalAttackForces(50f, 200f, 10f));

            CapitalObjectiveUpdate update = _coordinator.UpdateHourly(_invasion);

            Assert.That(update.Decision, Is.EqualTo(CapitalObjectiveDecision.AbandonNoTarget));
            Assert.That(_invasion.CapitalObjective!.Phase,
                Is.EqualTo(InvasionCapitalObjectivePhase.Dormant));
            Assert.That(_invasion.CapitalObjective.DormantReason,
                Is.EqualTo(InvasionCapitalObjectiveDormantReason.NoViableTarget));
        }

        [Test]
        public void ReleasedDefenceDoesNotRestartDuringTheSameSiegeEpisode()
        {
            _invasion.BeginCapitalDefence("capital", 0d, "siege-a");
            _invasion.AssignCapitalObjectiveCommander("commander");
            _world.Observation = Observation(3d, ownsCapital: true, capitalBesieged: true,
                commanderEligible: true, defence: new CapitalDefenceForces(10f, 10f, 100f),
                siegeEpisodeId: "siege-a");

            CapitalObjectiveUpdate abandoned = _coordinator.UpdateHourly(_invasion);
            CapitalObjectiveUpdate followingHour = _coordinator.UpdateHourly(_invasion);

            Assert.That(abandoned.Decision,
                Is.EqualTo(CapitalObjectiveDecision.AbandonNoTarget));
            Assert.That(followingHour.Decision, Is.EqualTo(CapitalObjectiveDecision.None));
            Assert.That(_invasion.CapitalObjective!.Phase,
                Is.EqualTo(InvasionCapitalObjectivePhase.Dormant));
        }

        [Test]
        public void ReleasedDefenceStartsAFreshAttackWhenTheCapitalFalls()
        {
            _invasion.BeginCapitalDefence("capital", 0d, "siege-a");
            _invasion.AssignCapitalObjectiveCommander("commander");
            _world.Observation = Observation(3d, ownsCapital: true, capitalBesieged: true,
                commanderEligible: true, defence: new CapitalDefenceForces(10f, 10f, 100f),
                siegeEpisodeId: "siege-a");
            _coordinator.UpdateHourly(_invasion);

            _world.Observation = Observation(4d, ownsCapital: false, capitalBesieged: false,
                commanderEligible: false, attack: new CapitalAttackForces(0f, 100f, 100f));

            CapitalObjectiveUpdate update = _coordinator.UpdateHourly(_invasion);

            Assert.That(update.Decision, Is.EqualTo(CapitalObjectiveDecision.Gather));
            Assert.That(_invasion.CapitalObjective!.Kind,
                Is.EqualTo(InvasionCapitalObjectiveKind.Attack));
            Assert.That(_invasion.CapitalObjective.Phase,
                Is.EqualTo(InvasionCapitalObjectivePhase.Gathering));
            Assert.That(_invasion.CapitalObjective.GatherDeadlineDays, Is.EqualTo(7d));
            Assert.That(_invasion.CapitalObjective.AttackDeadlineDays, Is.EqualTo(11d));
            Assert.That(_invasion.CapitalObjective.DefenceSiegeEpisodeId, Is.Null);
        }

        [Test]
        public void ANewSiegeEpisodeStartsAFreshDefenceAfterThePreviousEpisodeRetargeted()
        {
            _invasion.BeginCapitalDefence("capital", 0d, "siege-a");
            _invasion.AssignCapitalObjectiveCommander("commander");
            _invasion.RetargetCapitalAttack("alternative", 3d);
            _world.Observation = Observation(4d, ownsCapital: true, capitalBesieged: true,
                commanderEligible: true, defence: new CapitalDefenceForces(0f, 0f, 100f),
                siegeEpisodeId: "siege-a");

            CapitalObjectiveUpdate sameEpisode = _coordinator.UpdateHourly(_invasion);

            Assert.That(sameEpisode.Decision, Is.EqualTo(CapitalObjectiveDecision.None));
            Assert.That(_invasion.CapitalObjective!.Kind,
                Is.EqualTo(InvasionCapitalObjectiveKind.Attack));
            Assert.That(_invasion.CapitalObjective.TargetSettlementId, Is.EqualTo("alternative"));

            _world.Observation = Observation(5d, ownsCapital: true, capitalBesieged: true,
                commanderEligible: false, defence: new CapitalDefenceForces(0f, 0f, 100f),
                siegeEpisodeId: "siege-b");

            CapitalObjectiveUpdate newEpisode = _coordinator.UpdateHourly(_invasion);

            Assert.That(newEpisode.Decision, Is.EqualTo(CapitalObjectiveDecision.Gather));
            Assert.That(_invasion.CapitalObjective!.Kind,
                Is.EqualTo(InvasionCapitalObjectiveKind.Defence));
            Assert.That(_invasion.CapitalObjective.TargetSettlementId, Is.EqualTo("capital"));
            Assert.That(_invasion.CapitalObjective.GatherDeadlineDays, Is.EqualTo(8d));
            Assert.That(_invasion.CapitalObjective.DefenceSiegeEpisodeId, Is.EqualTo("siege-b"));
        }

        [Test]
        public void MatchingSiegeAtTheExactAttackDeadlineWinsOverTimeout()
        {
            _invasion.BeginCapitalAttack("capital", 0d);
            _invasion.AdvanceCapitalObjective();
            _invasion.AssignCapitalObjectiveCommander("commander");
            _world.Observation = Observation(7d, ownsCapital: false, capitalBesieged: false,
                commanderEligible: true, matchingSiege: true);

            CapitalObjectiveUpdate update = _coordinator.UpdateHourly(_invasion);

            Assert.That(update.Decision, Is.EqualTo(CapitalObjectiveDecision.SiegeStarted));
            Assert.That(_invasion.CapitalObjective!.Phase,
                Is.EqualTo(InvasionCapitalObjectivePhase.Besieging));
            Assert.That(_invasion.CapitalObjective.AttackDeadlineDays, Is.Null);
        }

        [Test]
        public void BesiegingDoesNotTryToSelectOrAssignACommander()
        {
            _invasion.BeginCapitalAttack("capital", 0d);
            _invasion.AdvanceCapitalObjective();
            _invasion.AssignCapitalObjectiveCommander("commander");
            _invasion.MarkCapitalSiegeStarted();
            _world.Observation = Observation(5d, ownsCapital: false, capitalBesieged: false,
                commanderEligible: false, matchingSiege: true);

            CapitalObjectiveUpdate update = _coordinator.UpdateHourly(_invasion);

            Assert.That(update.Decision, Is.EqualTo(CapitalObjectiveDecision.None));
            Assert.That(_invasion.CapitalObjective!.Phase,
                Is.EqualTo(InvasionCapitalObjectivePhase.Besieging));
            Assert.That(_invasion.CapitalObjective.CommanderPartyId, Is.Null);
            Assert.That(_world.CommanderQueries, Is.Zero);
        }

        [Test]
        public void EndedSiegeWithoutCaptureReleasesTheForcedObjective()
        {
            _invasion.BeginCapitalAttack("capital", 0d);
            _invasion.AdvanceCapitalObjective();
            _invasion.AssignCapitalObjectiveCommander("commander");
            _invasion.MarkCapitalSiegeStarted();
            _world.Observation = Observation(5d, ownsCapital: false, capitalBesieged: false,
                commanderEligible: false, matchingSiege: false, targetOwned: false);

            CapitalObjectiveUpdate update = _coordinator.UpdateHourly(_invasion);

            Assert.That(update.Decision, Is.EqualTo(CapitalObjectiveDecision.Complete));
            Assert.That(update.TargetSettlementId, Is.EqualTo("capital"));
            Assert.That(_invasion.CapitalObjective, Is.Null);
            Assert.That(_world.CommanderQueries, Is.Zero);
        }

        [Test]
        public void CaptureAtTheEndOfASiegeRelocatesBeforeTheObjectiveIsReleased()
        {
            _invasion.BeginCapitalAttack("capital", 0d);
            _invasion.RetargetCapitalAttack("alternative", 0d);
            _invasion.AssignCapitalObjectiveCommander("commander");
            _invasion.MarkCapitalSiegeStarted();
            _world.Observation = Observation(5d, ownsCapital: false, capitalBesieged: false,
                commanderEligible: false, matchingSiege: false, targetOwned: true);

            CapitalObjectiveUpdate update = _coordinator.UpdateHourly(_invasion);

            Assert.That(update.Decision, Is.EqualTo(CapitalObjectiveDecision.Complete));
            Assert.That(_world.RelocatedSettlementId, Is.EqualTo("alternative"));
            Assert.That(_invasion.CapitalSettlementId, Is.EqualTo("alternative"));
            Assert.That(_invasion.CapitalObjective, Is.Null);
        }

        [Test]
        public void AttackTimesOutAtExactlySevenDaysAndDoesNotDailyRestart()
        {
            _invasion.BeginCapitalAttack("capital", 0d);
            _invasion.AdvanceCapitalObjective();
            _invasion.AssignCapitalObjectiveCommander("commander");
            _world.Observation = Observation(7d, ownsCapital: false, capitalBesieged: false,
                commanderEligible: true);

            CapitalObjectiveUpdate hourly = _coordinator.UpdateHourly(_invasion);
            CapitalObjectiveUpdate daily = _coordinator.UpdateDaily(_invasion);

            Assert.That(hourly.Decision, Is.EqualTo(CapitalObjectiveDecision.AbandonTimeout));
            Assert.That(daily.Decision, Is.EqualTo(CapitalObjectiveDecision.None));
            Assert.That(_invasion.CapitalObjective!.DormantReason,
                Is.EqualTo(InvasionCapitalObjectiveDormantReason.AttackTimedOut));
            Assert.That(_world.TargetFactQueries, Is.Zero);
        }

        [Test]
        public void DailyRecoveryStartsAReadyTargetDirectlyInAdvancing()
        {
            _invasion.BeginCapitalAttack("capital", 0d);
            _invasion.WaitForViableCapitalTarget();
            _world.Observation = Observation(8d, ownsCapital: false, capitalBesieged: false,
                commanderEligible: true, attack: new CapitalAttackForces(150f, 0f, 0f));
            _world.Targets.Add(Viable("alternative", 10f));

            CapitalObjectiveUpdate update = _coordinator.UpdateDaily(_invasion);

            Assert.That(update.Decision, Is.EqualTo(CapitalObjectiveDecision.Retarget));
            Assert.That(_invasion.CapitalObjective!.Phase,
                Is.EqualTo(InvasionCapitalObjectivePhase.Advancing));
            Assert.That(_invasion.CapitalObjective.TargetSettlementId, Is.EqualTo("alternative"));
            Assert.That(_invasion.CapitalObjective.GatherDeadlineDays, Is.Null);
            Assert.That(_invasion.CapitalObjective.AttackDeadlineDays, Is.EqualTo(15d));
            Assert.That(_world.AdvancedCommander, Is.EqualTo("commander"));
        }

        [Test]
        public void DailyFailedEvaluationReturnsCommanderForceAndTargetFactsForDiagnostics()
        {
            _invasion.BeginCapitalAttack("capital", 0d);
            _invasion.WaitForViableCapitalTarget();
            var forces = new CapitalAttackForces(75f, 0f, 0f);
            _world.Observation = Observation(8d, ownsCapital: false, capitalBesieged: false,
                commanderEligible: true, attack: forces);
            _world.Targets.Add(Viable("too-strong", 10f));

            CapitalObjectiveUpdate update = _coordinator.UpdateDaily(_invasion);

            Assert.That(update.Decision, Is.EqualTo(CapitalObjectiveDecision.None));
            Assert.That(update.Evaluation, Is.Not.Null);
            Assert.That(update.Evaluation!.CommanderPartyId, Is.EqualTo("commander"));
            Assert.That(update.Evaluation.AttackForces.AssembledFieldStrength, Is.EqualTo(75f));
            Assert.That(update.Evaluation.TargetFacts, Has.Count.EqualTo(1));
            Assert.That(update.Evaluation.TargetFacts[0].SettlementId,
                Is.EqualTo("too-strong"));
            Assert.That(_invasion.CapitalObjective!.DormantReason,
                Is.EqualTo(InvasionCapitalObjectiveDormantReason.NoViableTarget));
        }

        [Test]
        public void LosingTheCapitalConvertsDefenceToAttackWithFreshClocks()
        {
            _invasion.BeginCapitalDefence("capital", 1d);
            _invasion.AssignCapitalObjectiveCommander("commander");
            _world.Observation = Observation(2d, ownsCapital: false, capitalBesieged: false,
                commanderEligible: true, attack: new CapitalAttackForces(0f, 100f, 100f));

            _coordinator.UpdateHourly(_invasion);

            Assert.That(_invasion.CapitalObjective!.Kind,
                Is.EqualTo(InvasionCapitalObjectiveKind.Attack));
            Assert.That(_invasion.CapitalObjective.GatherDeadlineDays, Is.EqualTo(5d));
            Assert.That(_invasion.CapitalObjective.AttackDeadlineDays, Is.EqualTo(9d));
        }

        [Test]
        public void CapturedAlternativeBecomesCapitalWhenTheOldCapitalIsStillLost()
        {
            _invasion.BeginCapitalAttack("capital", 0d);
            _invasion.RetargetCapitalAttack("alternative", 0d);
            _invasion.AssignCapitalObjectiveCommander("commander");
            _world.Observation = Observation(1d, ownsCapital: false, capitalBesieged: false,
                commanderEligible: true, targetOwned: true);

            CapitalObjectiveUpdate update = _coordinator.UpdateHourly(_invasion);

            Assert.That(update.Decision, Is.EqualTo(CapitalObjectiveDecision.Complete));
            Assert.That(_world.RelocatedSettlementId, Is.EqualTo("alternative"));
            Assert.That(_invasion.CapitalSettlementId, Is.EqualTo("alternative"));
            Assert.That(_invasion.CapitalObjective, Is.Null);
        }

        [Test]
        public void CapturedAlternativeAtTheExactAttackDeadlineWinsOverTimeout()
        {
            _invasion.BeginCapitalAttack("capital", 0d);
            _invasion.RetargetCapitalAttack("alternative", 0d);
            _invasion.AssignCapitalObjectiveCommander("commander");
            _world.Observation = Observation(7d, ownsCapital: false, capitalBesieged: false,
                commanderEligible: true, targetOwned: true);

            CapitalObjectiveUpdate update = _coordinator.UpdateHourly(_invasion);

            Assert.That(update.Decision, Is.EqualTo(CapitalObjectiveDecision.Complete));
            Assert.That(_world.RelocatedSettlementId, Is.EqualTo("alternative"));
            Assert.That(_invasion.CapitalSettlementId, Is.EqualTo("alternative"));
            Assert.That(_invasion.CapitalObjective, Is.Null);
        }

        [Test]
        public void CapturedTargetAtTheExactGatherDeadlineWinsOverRetargeting()
        {
            _invasion.BeginCapitalAttack("alternative", 0d);
            _invasion.AssignCapitalObjectiveCommander("commander");
            _world.Observation = Observation(3d, ownsCapital: false, capitalBesieged: false,
                commanderEligible: true, targetOwned: true,
                attack: new CapitalAttackForces(0f, 100f, 100f));
            _world.Targets.Add(Viable("other", 1f));

            CapitalObjectiveUpdate update = _coordinator.UpdateHourly(_invasion);

            Assert.That(update.Decision, Is.EqualTo(CapitalObjectiveDecision.Complete));
            Assert.That(_world.RelocatedSettlementId, Is.EqualTo("alternative"));
            Assert.That(_world.TargetFactQueries, Is.Zero);
            Assert.That(_invasion.CapitalObjective, Is.Null);
        }

        [Test]
        public void EndedDefenceCompletesTheTemporaryObjective()
        {
            _invasion.BeginCapitalDefence("capital", 0d);
            _world.Observation = Observation(1d, ownsCapital: true, capitalBesieged: false,
                commanderEligible: false);

            CapitalObjectiveUpdate update = _coordinator.UpdateHourly(_invasion);

            Assert.That(update.Decision, Is.EqualTo(CapitalObjectiveDecision.Complete));
            Assert.That(_invasion.CapitalObjective, Is.Null);
        }

        private static InvasionAssaultRequirements Requirements() =>
            new(new[] { SiegeTargetKind.Town }, 1.5f, 1.2f, false,
                InvasionSupplyLineKind.Coastal);

        private static SiegeTargetFacts Viable(string settlementId, float distance) =>
            new(settlementId, SiegeTargetKind.Town, true, true, true, true, true, distance,
                80f, 50f);

        private static CapitalObjectiveObservation Observation(double nowDays, bool ownsCapital,
            bool capitalBesieged, bool commanderEligible, bool matchingSiege = false,
            bool targetOwned = false, CapitalDefenceForces defence = default,
            CapitalAttackForces attack = default, string? siegeEpisodeId = null)
        {
            return new CapitalObjectiveObservation(nowDays, ownsCapital, capitalBesieged,
                commanderEligible, matchingSiege, targetOwned, defence, attack, siegeEpisodeId);
        }

        private sealed class FakeConfigurationProvider : IInvasionAssaultConfigurationProvider
        {
            private readonly InvasionAssaultRequirements _requirements;

            public FakeConfigurationProvider(InvasionAssaultRequirements requirements)
            {
                _requirements = requirements;
            }

            public InvasionAssaultRequirements? Find(string spawnTemplateId) => _requirements;
        }

        private sealed class FakeWorld : IInvasionCapitalObjectiveWorld
        {
            public CapitalObjectiveObservation Observation;
            public readonly List<CapitalObjectiveCommanderCandidate> Commanders = new();
            public readonly List<SiegeTargetFacts> Targets = new();
            public string? GatheredCommander;
            public string? AdvancedCommander;
            public string? ExcludedSettlementId;
            public string? RelocatedSettlementId;
            public int TargetFactQueries;
            public int CommanderQueries;

            public CapitalObjectiveObservation Observe(string invasionId,
                string designatedCapitalSettlementId, string? targetSettlementId,
                string? commanderPartyId) => Observation;

            public IReadOnlyList<CapitalObjectiveCommanderCandidate> FindCommanderCandidates(
                string invasionId)
            {
                CommanderQueries++;
                return Commanders;
            }

            public IReadOnlyList<SiegeTargetFacts> FindTargetFacts(string invasionId,
                string commanderPartyId, InvasionAssaultRequirements requirements,
                string? excludedSettlementId)
            {
                TargetFactQueries++;
                ExcludedSettlementId = excludedSettlementId;
                return Targets;
            }

            public void BeginGathering(string commanderPartyId, string targetSettlementId)
            {
                GatheredCommander = commanderPartyId;
            }

            public void BeginAdvance(string commanderPartyId)
            {
                AdvancedCommander = commanderPartyId;
            }

            public void RelocateCapitalAfterObjectiveCapture(string invasionId, string settlementId)
            {
                RelocatedSettlementId = settlementId;
            }
        }
    }
}
