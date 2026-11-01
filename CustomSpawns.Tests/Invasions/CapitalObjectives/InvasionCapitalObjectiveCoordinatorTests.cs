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
            _world.Candidates.Add(new CapitalArmyPartyCandidate("commander", 100f, true,
                false, true));
            _world.Candidates.Add(new CapitalArmyPartyCandidate("follower", 50f, true,
                false, false));
            _world.MobilizationResult = Success("commander", "staging", "commander",
                "follower");

            var ratios = new ForceRatioRules();
            _coordinator = new InvasionCapitalObjectiveCoordinator(
                new FakeConfigurationProvider(Requirements()), _world,
                new CapitalObjectiveStrengthRules(ratios),
                new CapitalArmyMobilizationPlanner(), new SiegeTargetSelector(ratios));
        }

        [Test]
        public void ReadyDefenceMobilizesUnifiedArmyThenAdvancesImmediately()
        {
            _world.Observation = Observation(1d, ownsCapital: true, capitalBesieged: true,
                commanderEligible: true, defence: new CapitalDefenceForces(40f, 20f, 50f),
                siegeEpisodeId: "siege-a");

            CapitalObjectiveUpdate update = _coordinator.UpdateHourly(_invasion);

            Assert.That(update.Decision, Is.EqualTo(CapitalObjectiveDecision.Advance));
            Assert.That(_invasion.CapitalObjective!.Phase,
                Is.EqualTo(InvasionCapitalObjectivePhase.Advancing));
            Assert.That(_world.MobilizationCalls, Is.EqualTo(1));
            Assert.That(_world.LastPlan!.PartyIds,
                Is.EqualTo(new[] { "commander", "follower" }));
            Assert.That(_world.AdvancedCommander, Is.EqualTo("commander"));
            Assert.That(update.MobilizationResult, Is.SameAs(_world.MobilizationResult));
        }

        [Test]
        public void FailedNativeMobilizationLeavesObjectiveWithoutGatherDeadline()
        {
            _world.MobilizationResult = new CapitalArmyMobilizationResult(false, null, null, null,
                new[] { new CapitalArmyPartyDeferral("follower", "in_battle") },
                "native_creation_failed");
            _world.Observation = Observation(1d, ownsCapital: true, capitalBesieged: true,
                commanderEligible: false, siegeEpisodeId: "siege-a");

            CapitalObjectiveUpdate update = _coordinator.UpdateHourly(_invasion);

            Assert.That(update.Decision,
                Is.EqualTo(CapitalObjectiveDecision.MobilizationFailed));
            Assert.That(_invasion.CapitalObjective!.Phase,
                Is.EqualTo(InvasionCapitalObjectivePhase.Mobilizing));
            Assert.That(_invasion.CapitalObjective.GatherDeadlineDays, Is.Null);
            Assert.That(update.MobilizationResult, Is.SameAs(_world.MobilizationResult));
            Assert.That(update.MobilizationResult!.FailureReason,
                Is.EqualTo("native_creation_failed"));
            Assert.That(update.MobilizationResult.DeferredParties[0].Reason,
                Is.EqualTo("in_battle"));
        }

        [Test]
        public void MissingCommanderReportsPlannerFailureAndCandidateDeferrals()
        {
            _world.Candidates.Clear();
            _world.Candidates.Add(new CapitalArmyPartyCandidate("busy-party", 50f, false,
                false, false, "in_battle"));
            _world.Observation = Observation(1d, ownsCapital: true, capitalBesieged: true,
                commanderEligible: false, siegeEpisodeId: "siege-a");

            CapitalObjectiveUpdate update = _coordinator.UpdateHourly(_invasion);

            Assert.That(update.Decision,
                Is.EqualTo(CapitalObjectiveDecision.MobilizationFailed));
            Assert.That(update.MobilizationResult!.FailureReason,
                Is.EqualTo("no_mobilizable_commander"));
            Assert.That(update.MobilizationResult.DeferredParties[0].PartyId,
                Is.EqualTo("busy-party"));
            Assert.That(update.MobilizationResult.DeferredParties[0].Reason,
                Is.EqualTo("in_battle"));
            Assert.That(_world.MobilizationCalls, Is.Zero);
        }

        [Test]
        public void FailedMobilizationCannotOutliveTheSevenDayAttackDeadline()
        {
            _invasion.BeginCapitalAttack("capital", 0d);
            _world.MobilizationResult = new CapitalArmyMobilizationResult(false, null, null, null,
                failureReason: "native_creation_failed");
            _world.Observation = Observation(7d, ownsCapital: false, capitalBesieged: false,
                commanderEligible: false);

            CapitalObjectiveUpdate update = _coordinator.UpdateHourly(_invasion);

            Assert.That(update.Decision, Is.EqualTo(CapitalObjectiveDecision.AbandonTimeout));
            Assert.That(_invasion.CapitalObjective!.Phase,
                Is.EqualTo(InvasionCapitalObjectivePhase.Dormant));
            Assert.That(_invasion.CapitalObjective.DormantReason,
                Is.EqualTo(InvasionCapitalObjectiveDormantReason.AttackTimedOut));
            Assert.That(_world.MobilizationCalls, Is.Zero);
        }

        [Test]
        public void GatherDeadlineStartsWhenNativeMobilizationSucceeds()
        {
            _world.Observation = Observation(10d, ownsCapital: true, capitalBesieged: true,
                commanderEligible: true, defence: new CapitalDefenceForces(10f, 0f, 100f),
                siegeEpisodeId: "siege-a");

            CapitalObjectiveUpdate update = _coordinator.UpdateHourly(_invasion);

            Assert.That(_invasion.CapitalObjective!.Phase,
                Is.EqualTo(InvasionCapitalObjectivePhase.Gathering));
            Assert.That(_invasion.CapitalObjective.GatherDeadlineDays, Is.EqualTo(13d));
            Assert.That(_invasion.CapitalObjective.StagingSettlementId, Is.EqualTo("staging"));
            Assert.That(update.MobilizationResult, Is.SameAs(_world.MobilizationResult));
        }

        [TestCase(InvasionCapitalObjectiveKind.Defence, 4d)]
        [TestCase(InvasionCapitalObjectiveKind.Attack, 1d)]
        public void GatheringInvitesEveryNewEligiblePartyWithoutCountingUnarrivedStrength(
            InvasionCapitalObjectiveKind kind, double nowDays)
        {
            BeginAndMobilize(kind, 0d, "siege-a");
            _world.Candidates.Add(new CapitalArmyPartyCandidate("reinforcement-a", 1000f,
                true, false, false));
            _world.Candidates.Add(new CapitalArmyPartyCandidate("reinforcement-b", 1000f,
                true, false, false));
            _world.Candidates.Add(new CapitalArmyPartyCandidate("trapped", 1000f,
                false, false, false, "inside_besieged_settlement"));
            bool defence = kind == InvasionCapitalObjectiveKind.Defence;
            _world.Observation = Observation(nowDays, ownsCapital: defence, capitalBesieged: defence,
                commanderEligible: true, defence: new CapitalDefenceForces(10f, 0f, 100f),
                attack: new CapitalAttackForces(10f, 100f, 100f),
                siegeEpisodeId: "siege-a");

            CapitalObjectiveUpdate update = _coordinator.UpdateHourly(_invasion);

            Assert.That(_world.LastInvitations,
                Is.EqualTo(new[] { "reinforcement-a", "reinforcement-b" }));
            Assert.That(_invasion.CapitalObjective!.MobilizedPartyIds,
                Is.EqualTo(new[] { "commander", "follower", "reinforcement-a", "reinforcement-b" }));
            Assert.That(_invasion.CapitalObjective.Phase,
                Is.EqualTo(InvasionCapitalObjectivePhase.Gathering));
            Assert.That(_invasion.CapitalObjective.GatherDeadlineDays, Is.EqualTo(3d));
            Assert.That(_world.MobilizationCalls, Is.Zero);
            Assert.That(_world.AdvancedCommander, Is.Null);
            Assert.That(update.MobilizationResult!.DeferredParties[0].Reason,
                Is.EqualTo("inside_besieged_settlement"));

            _coordinator.UpdateHourly(_invasion);

            Assert.That(_world.LastInvitations, Is.Empty);
        }

        [Test]
        public void DeferredDefencePartyIsInvitedWhenAvailableThenArmyAdvancesWhenAssembled()
        {
            BeginAndMobilize(InvasionCapitalObjectiveKind.Defence, 0d, "siege-a");
            _world.Candidates.Add(new CapitalArmyPartyCandidate("reinforcement", 200f,
                false, false, false, "in_battle"));
            _world.Observation = Observation(1d, ownsCapital: true, capitalBesieged: true,
                commanderEligible: true, defence: new CapitalDefenceForces(10f, 0f, 100f),
                siegeEpisodeId: "siege-a");
            _coordinator.UpdateHourly(_invasion);
            Assert.That(_world.LastInvitations, Is.Empty);

            _world.Candidates.RemoveAt(_world.Candidates.Count - 1);
            _world.Candidates.Add(new CapitalArmyPartyCandidate("reinforcement", 200f,
                true, false, false));
            _coordinator.UpdateHourly(_invasion);
            Assert.That(_world.LastInvitations, Is.EqualTo(new[] { "reinforcement" }));
            Assert.That(_world.AdvancedCommander, Is.Null);

            _world.Observation = Observation(2d, ownsCapital: true, capitalBesieged: true,
                commanderEligible: true, defence: new CapitalDefenceForces(120f, 0f, 100f),
                siegeEpisodeId: "siege-a");
            CapitalObjectiveUpdate update = _coordinator.UpdateHourly(_invasion);

            Assert.That(update.Decision, Is.EqualTo(CapitalObjectiveDecision.Advance));
            Assert.That(update.TargetSettlementId, Is.EqualTo("capital"));
            Assert.That(_world.AdvancedCommander, Is.EqualTo("commander"));
            Assert.That(_world.MobilizationCalls, Is.Zero);
        }

        [Test]
        public void FailedInvitationIsNotPersistedAndIsRetried()
        {
            BeginAndMobilize(InvasionCapitalObjectiveKind.Defence, 0d, "siege-a");
            _world.Candidates.Add(new CapitalArmyPartyCandidate("reinforcement", 200f,
                true, false, false));
            _world.InvitationResult = new string[0];
            _world.Observation = Observation(1d, ownsCapital: true, capitalBesieged: true,
                commanderEligible: true, defence: new CapitalDefenceForces(10f, 0f, 100f),
                siegeEpisodeId: "siege-a");

            _coordinator.UpdateHourly(_invasion);
            Assert.That(_invasion.CapitalObjective!.MobilizedPartyIds,
                Does.Not.Contain("reinforcement"));

            _world.InvitationResult = null;
            _coordinator.UpdateHourly(_invasion);

            Assert.That(_world.LastInvitations, Is.EqualTo(new[] { "reinforcement" }));
            Assert.That(_invasion.CapitalObjective.MobilizedPartyIds,
                Does.Contain("reinforcement"));
        }

        [Test]
        public void PlannedButUnassembledStrengthDoesNotMakeAttackReady()
        {
            BeginAndMobilize(InvasionCapitalObjectiveKind.Attack, 0d);
            _world.Candidates.Clear();
            _world.Candidates.Add(new CapitalArmyPartyCandidate("commander", 100f, true,
                false, true));
            _world.Candidates.Add(new CapitalArmyPartyCandidate("follower", 1000f, true,
                false, false));
            _world.Observation = Observation(1d, ownsCapital: false, capitalBesieged: false,
                commanderEligible: true, attack: new CapitalAttackForces(100f, 100f, 10f));

            CapitalObjectiveUpdate update = _coordinator.UpdateHourly(_invasion);

            Assert.That(update.Decision, Is.EqualTo(CapitalObjectiveDecision.None));
            Assert.That(_invasion.CapitalObjective!.Phase,
                Is.EqualTo(InvasionCapitalObjectivePhase.Gathering));
            Assert.That(_world.AdvancedCommander, Is.Null);
            Assert.That(_world.LastInvitations, Is.Empty);
        }

        [Test]
        public void SufficientAssembledAttackAdvancesBeforeGatherDeadline()
        {
            BeginAndMobilize(InvasionCapitalObjectiveKind.Attack, 0d);
            _world.Observation = Observation(1d, ownsCapital: false, capitalBesieged: false,
                commanderEligible: true, attack: new CapitalAttackForces(150f, 100f, 10f));

            CapitalObjectiveUpdate update = _coordinator.UpdateHourly(_invasion);

            Assert.That(update.Decision, Is.EqualTo(CapitalObjectiveDecision.Advance));
            Assert.That(_invasion.CapitalObjective!.Phase,
                Is.EqualTo(InvasionCapitalObjectivePhase.Advancing));
            Assert.That(_world.AdvancedCommander, Is.EqualTo("commander"));
        }

        [Test]
        public void FragmentedGatheringArmyIsRemobilized()
        {
            BeginAndMobilize(InvasionCapitalObjectiveKind.Defence, 0d, "siege-a");
            _world.Observation = Observation(1d, ownsCapital: true, capitalBesieged: true,
                commanderEligible: true, defence: new CapitalDefenceForces(10f, 0f, 100f),
                siegeEpisodeId: "siege-a", unifiedArmy: false);

            _coordinator.UpdateHourly(_invasion);

            Assert.That(_world.MobilizationCalls, Is.EqualTo(1));
            Assert.That(_invasion.CapitalObjective!.Phase,
                Is.EqualTo(InvasionCapitalObjectivePhase.Gathering));
            Assert.That(_invasion.CapitalObjective.GatherDeadlineDays, Is.EqualTo(4d));
        }

        [Test]
        public void DefenceDoesNotBecomeAnUnrelatedAttackAtThreeDayDeadline()
        {
            BeginAndMobilize(InvasionCapitalObjectiveKind.Defence, 0d, "siege-a");
            _world.Observation = Observation(3d, ownsCapital: true, capitalBesieged: true,
                commanderEligible: true, defence: new CapitalDefenceForces(10f, 10f, 100f),
                siegeEpisodeId: "siege-a");
            _world.Targets.Add(Viable("coastal-city", 10f));

            CapitalObjectiveUpdate update = _coordinator.UpdateHourly(_invasion);

            Assert.That(update.Decision, Is.EqualTo(CapitalObjectiveDecision.None));
            Assert.That(_invasion.CapitalObjective!.Kind,
                Is.EqualTo(InvasionCapitalObjectiveKind.Defence));
            Assert.That(_invasion.CapitalObjective.Phase,
                Is.EqualTo(InvasionCapitalObjectivePhase.Gathering));
            Assert.That(_invasion.CapitalObjective.TargetSettlementId, Is.EqualTo("capital"));
            Assert.That(_world.TargetFactQueries, Is.Zero);
        }

        [Test]
        public void AttackGatheringRetargetsAtThreeDaysUsingConfiguredRequirements()
        {
            BeginAndMobilize(InvasionCapitalObjectiveKind.Attack, 0d);
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
        public void AttackWithoutAlternativeAtThreeDaysWaitsForDailyRecovery()
        {
            BeginAndMobilize(InvasionCapitalObjectiveKind.Attack, 0d);
            _world.Observation = Observation(3d, ownsCapital: false, capitalBesieged: false,
                commanderEligible: true, attack: new CapitalAttackForces(50f, 200f, 10f));

            CapitalObjectiveUpdate update = _coordinator.UpdateHourly(_invasion);

            Assert.That(update.Decision, Is.EqualTo(CapitalObjectiveDecision.AbandonNoTarget));
            Assert.That(_invasion.CapitalObjective!.DormantReason,
                Is.EqualTo(InvasionCapitalObjectiveDormantReason.NoViableTarget));
        }

        [Test]
        public void CapitalLossConvertsDefenceToFreshAttackMobilization()
        {
            BeginAndMobilize(InvasionCapitalObjectiveKind.Defence, 1d, "siege-a");
            _world.Observation = Observation(2d, ownsCapital: false, capitalBesieged: false,
                commanderEligible: true, attack: new CapitalAttackForces(0f, 100f, 100f));

            CapitalObjectiveUpdate update = _coordinator.UpdateHourly(_invasion);

            Assert.That(update.Decision, Is.EqualTo(CapitalObjectiveDecision.Gather));
            Assert.That(_invasion.CapitalObjective!.Kind,
                Is.EqualTo(InvasionCapitalObjectiveKind.Attack));
            Assert.That(_invasion.CapitalObjective.Phase,
                Is.EqualTo(InvasionCapitalObjectivePhase.Gathering));
            Assert.That(_invasion.CapitalObjective.GatherDeadlineDays, Is.EqualTo(5d));
            Assert.That(_invasion.CapitalObjective.AttackDeadlineDays, Is.EqualTo(9d));
            Assert.That(_world.MobilizationCalls, Is.EqualTo(1));
        }

        [Test]
        public void EndedSiegeBecomesDormantAndIsNotRecreatedNextHour()
        {
            BeginAndMobilize(InvasionCapitalObjectiveKind.Attack, 0d);
            _invasion.AdvanceCapitalObjective();
            _invasion.MarkCapitalSiegeStarted();
            _world.Observation = Observation(5d, ownsCapital: false, capitalBesieged: false,
                commanderEligible: true, matchingSiege: false);

            CapitalObjectiveUpdate ended = _coordinator.UpdateHourly(_invasion);
            CapitalObjectiveUpdate followingHour = _coordinator.UpdateHourly(_invasion);

            Assert.That(ended.Decision,
                Is.EqualTo(CapitalObjectiveDecision.AbandonSiegeAborted));
            Assert.That(followingHour.Decision, Is.EqualTo(CapitalObjectiveDecision.None));
            Assert.That(_invasion.CapitalObjective!.DormantReason,
                Is.EqualTo(InvasionCapitalObjectiveDormantReason.SiegeAborted));
        }

        [Test]
        public void MatchingSiegeAtExactAttackDeadlineWinsOverTimeout()
        {
            BeginAndMobilize(InvasionCapitalObjectiveKind.Attack, 0d);
            _invasion.AdvanceCapitalObjective();
            _world.Observation = Observation(7d, ownsCapital: false, capitalBesieged: false,
                commanderEligible: true, matchingSiege: true);

            CapitalObjectiveUpdate update = _coordinator.UpdateHourly(_invasion);

            Assert.That(update.Decision, Is.EqualTo(CapitalObjectiveDecision.SiegeStarted));
            Assert.That(_invasion.CapitalObjective!.Phase,
                Is.EqualTo(InvasionCapitalObjectivePhase.Besieging));
            Assert.That(_invasion.CapitalObjective.AttackDeadlineDays, Is.EqualTo(7d));
        }

        [Test]
        public void MatchingSiegeAtAttackDeadlineWinsBeforeMobilization()
        {
            _invasion.BeginCapitalAttack("capital", 0d);
            _world.Observation = Observation(7d, ownsCapital: false, capitalBesieged: false,
                commanderEligible: false, matchingSiege: true);

            CapitalObjectiveUpdate update = _coordinator.UpdateHourly(_invasion);

            Assert.That(update.Decision, Is.EqualTo(CapitalObjectiveDecision.SiegeStarted));
            Assert.That(_invasion.CapitalObjective!.Phase,
                Is.EqualTo(InvasionCapitalObjectivePhase.Besieging));
            Assert.That(_invasion.CapitalObjective.CommanderPartyId, Is.Null);
            Assert.That(_world.MobilizationCalls, Is.Zero);
        }

        [Test]
        public void MatchingSiegeAtGatherDeadlineWinsBeforeRetargeting()
        {
            BeginAndMobilize(InvasionCapitalObjectiveKind.Attack, 0d);
            _world.Targets.Add(Viable("alternative", 10f));
            _world.Observation = Observation(3d, ownsCapital: false, capitalBesieged: false,
                commanderEligible: true, matchingSiege: true,
                attack: new CapitalAttackForces(10f, 100f, 100f));

            CapitalObjectiveUpdate update = _coordinator.UpdateHourly(_invasion);

            Assert.That(update.Decision, Is.EqualTo(CapitalObjectiveDecision.SiegeStarted));
            Assert.That(_invasion.CapitalObjective!.Phase,
                Is.EqualTo(InvasionCapitalObjectivePhase.Besieging));
            Assert.That(_invasion.CapitalObjective.TargetSettlementId, Is.EqualTo("capital"));
            Assert.That(_world.TargetFactQueries, Is.Zero);
        }

        [Test]
        public void AttackTimesOutAtExactlySevenDaysAndWaitsUntilDailyRecoveryFindsAViableTarget()
        {
            BeginAndMobilize(InvasionCapitalObjectiveKind.Attack, 0d);
            _invasion.AdvanceCapitalObjective();
            _world.Observation = Observation(7d, ownsCapital: false, capitalBesieged: false,
                commanderEligible: true);

            CapitalObjectiveUpdate hourly = _coordinator.UpdateHourly(_invasion);
            CapitalObjectiveUpdate followingHour = _coordinator.UpdateHourly(_invasion);
            CapitalObjectiveUpdate daily = _coordinator.UpdateDaily(_invasion);

            Assert.That(hourly.Decision, Is.EqualTo(CapitalObjectiveDecision.AbandonTimeout));
            Assert.That(followingHour.Decision, Is.EqualTo(CapitalObjectiveDecision.None));
            Assert.That(daily.Decision, Is.EqualTo(CapitalObjectiveDecision.None));
            Assert.That(_invasion.CapitalObjective!.DormantReason,
                Is.EqualTo(InvasionCapitalObjectiveDormantReason.AttackTimedOut));
            Assert.That(_world.MobilizationCalls, Is.Zero);
        }

        [Test]
        public void CapturedAlternativeBecomesCapitalWhenTheDesignatedCapitalIsLost()
        {
            BeginAndMobilize(InvasionCapitalObjectiveKind.Attack, 0d);
            _invasion.RetargetCapitalAttack("alternative", 1d);
            _world.Observation = Observation(2d, ownsCapital: false, capitalBesieged: false,
                commanderEligible: true, targetOwned: true);

            CapitalObjectiveUpdate update = _coordinator.UpdateHourly(_invasion);

            Assert.That(update.Decision, Is.EqualTo(CapitalObjectiveDecision.Complete));
            Assert.That(update.TargetSettlementId, Is.EqualTo("alternative"));
            Assert.That(_world.RelocatedSettlementId, Is.EqualTo("alternative"));
            Assert.That(_invasion.CapitalSettlementId, Is.EqualTo("alternative"));
            Assert.That(_invasion.CapitalObjective, Is.Null);
        }

        [Test]
        public void RegainedDesignatedCapitalCompletesAlternativeAttackWithoutRelocation()
        {
            BeginAndMobilize(InvasionCapitalObjectiveKind.Attack, 0d);
            _invasion.RetargetCapitalAttack("alternative", 1d);
            _world.Observation = Observation(2d, ownsCapital: true, capitalBesieged: false,
                commanderEligible: true, targetOwned: true);

            CapitalObjectiveUpdate update = _coordinator.UpdateHourly(_invasion);

            Assert.That(update.Decision, Is.EqualTo(CapitalObjectiveDecision.Complete));
            Assert.That(_world.RelocatedSettlementId, Is.Null);
            Assert.That(_invasion.CapitalSettlementId, Is.EqualTo("capital"));
            Assert.That(_invasion.CapitalObjective, Is.Null);
        }

        [Test]
        public void DailyRecoveryKeepsWaitingUntilChangedForcesMakeATargetViable()
        {
            _invasion.BeginCapitalAttack("capital", 0d);
            _invasion.WaitForViableCapitalTarget();
            _world.Targets.Add(Viable("alternative", 10f));
            _world.Observation = Observation(8d, ownsCapital: false, capitalBesieged: false,
                commanderEligible: true, attack: new CapitalAttackForces(100f, 0f, 0f));

            CapitalObjectiveUpdate insufficient = _coordinator.UpdateDaily(_invasion);

            Assert.That(insufficient.Decision, Is.EqualTo(CapitalObjectiveDecision.None));
            Assert.That(_invasion.CapitalObjective!.DormantReason,
                Is.EqualTo(InvasionCapitalObjectiveDormantReason.NoViableTarget));
            Assert.That(_invasion.CapitalObjective.TargetSettlementId, Is.Null);
            Assert.That(_world.MobilizationCalls, Is.Zero);

            _world.Observation = Observation(9d, ownsCapital: false, capitalBesieged: false,
                commanderEligible: true, attack: new CapitalAttackForces(150f, 0f, 0f));

            CapitalObjectiveUpdate sufficient = _coordinator.UpdateDaily(_invasion);

            Assert.That(sufficient.Decision, Is.EqualTo(CapitalObjectiveDecision.Retarget));
            Assert.That(_invasion.CapitalObjective.Phase,
                Is.EqualTo(InvasionCapitalObjectivePhase.Mobilizing));
            Assert.That(_invasion.CapitalObjective.TargetSettlementId, Is.EqualTo("alternative"));
            Assert.That(_invasion.CapitalObjective.AttackDeadlineDays, Is.EqualTo(16d));
            Assert.That(_world.TargetFactQueries, Is.EqualTo(2));
        }

        [TestCase(InvasionCapitalObjectiveDormantReason.NoViableTarget)]
        [TestCase(InvasionCapitalObjectiveDormantReason.AttackTimedOut)]
        [TestCase(InvasionCapitalObjectiveDormantReason.SiegeAborted)]
        public void DailyRecoverySelectsTargetThenRequiresMobilization(
            InvasionCapitalObjectiveDormantReason reason)
        {
            _invasion.RestoreCapitalObjective(new InvasionCapitalObjective(
                InvasionCapitalObjectiveKind.Attack, InvasionCapitalObjectivePhase.Dormant,
                null, null, null, reason, null));
            _world.Observation = Observation(8d, ownsCapital: false, capitalBesieged: false,
                commanderEligible: true, attack: new CapitalAttackForces(150f, 0f, 0f));
            _world.Targets.Add(Viable("alternative", 10f));

            CapitalObjectiveUpdate update = _coordinator.UpdateDaily(_invasion);

            Assert.That(update.Decision, Is.EqualTo(CapitalObjectiveDecision.Retarget));
            Assert.That(_invasion.CapitalObjective!.Phase,
                Is.EqualTo(InvasionCapitalObjectivePhase.Mobilizing));
            Assert.That(_invasion.CapitalObjective.TargetSettlementId, Is.EqualTo("alternative"));
            Assert.That(_invasion.CapitalObjective.GatherDeadlineDays, Is.Null);
            Assert.That(_invasion.CapitalObjective.AttackDeadlineDays, Is.EqualTo(15d));
            Assert.That(_world.MobilizationCalls, Is.Zero);
        }

        [Test]
        public void EndedDefenceCompletesTemporaryObjectiveBeforeMobilization()
        {
            _invasion.BeginCapitalDefence("capital", 0d);
            _world.Observation = Observation(1d, ownsCapital: true, capitalBesieged: false,
                commanderEligible: false);

            CapitalObjectiveUpdate update = _coordinator.UpdateHourly(_invasion);

            Assert.That(update.Decision, Is.EqualTo(CapitalObjectiveDecision.Complete));
            Assert.That(_invasion.CapitalObjective, Is.Null);
            Assert.That(_world.MobilizationCalls, Is.Zero);
        }

        private void BeginAndMobilize(InvasionCapitalObjectiveKind kind, double nowDays,
            string? siegeEpisodeId = null)
        {
            if (kind == InvasionCapitalObjectiveKind.Defence)
            {
                _invasion.BeginCapitalDefence("capital", nowDays, siegeEpisodeId);
            }
            else
            {
                _invasion.BeginCapitalAttack("capital", nowDays);
            }

            _invasion.CapitalObjective!.MarkMobilized(
                Success("commander", "staging", "commander", "follower"), nowDays);
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
            CapitalAttackForces attack = default, string? siegeEpisodeId = null,
            bool unifiedArmy = true) => new(nowDays, ownsCapital, capitalBesieged,
            commanderEligible, matchingSiege, targetOwned, defence, attack, siegeEpisodeId,
            unifiedArmy);

        private static CapitalArmyMobilizationResult Success(string commanderPartyId,
            string stagingSettlementId, params string[] partyIds) =>
            new(true, commanderPartyId, partyIds, stagingSettlementId);

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
            public readonly List<CapitalArmyPartyCandidate> Candidates = new();
            public readonly List<SiegeTargetFacts> Targets = new();
            public CapitalArmyMobilizationResult MobilizationResult = null!;
            public CapitalArmyMobilizationPlan? LastPlan;
            public string? AdvancedCommander;
            public string? ExcludedSettlementId;
            public string? RelocatedSettlementId;
            public int TargetFactQueries;
            public int MobilizationCalls;
            public IReadOnlyList<string>? LastInvitations;
            public IReadOnlyList<string>? InvitationResult;

            public CapitalObjectiveObservation Observe(string invasionId,
                string designatedCapitalSettlementId, string? targetSettlementId,
                string? commanderPartyId) => Observation;

            public IReadOnlyList<CapitalArmyPartyCandidate> FindMobilizationCandidates(
                string invasionId) => Candidates;

            public IReadOnlyList<SiegeTargetFacts> FindTargetFacts(string invasionId,
                string commanderPartyId, InvasionAssaultRequirements requirements,
                string? excludedSettlementId)
            {
                TargetFactQueries++;
                ExcludedSettlementId = excludedSettlementId;
                return Targets;
            }

            public CapitalArmyMobilizationResult MobilizeArmy(CapitalArmyMobilizationPlan plan)
            {
                MobilizationCalls++;
                LastPlan = plan;
                return MobilizationResult;
            }

            public IReadOnlyList<string> InviteToArmy(string invasionId, string commanderPartyId,
                IReadOnlyList<string> partyIds)
            {
                Assert.That(invasionId, Is.EqualTo("clan"));
                Assert.That(commanderPartyId, Is.EqualTo("commander"));
                LastInvitations = partyIds;
                return InvitationResult ?? partyIds;
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
