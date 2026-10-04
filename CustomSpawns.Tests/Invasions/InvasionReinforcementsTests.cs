using System.Collections.Generic;
using System.Linq;
using CustomSpawns.Invasions;
using CustomSpawns.Invasions.Adapters;
using NUnit.Framework;

namespace CustomSpawns.Tests.Invasions
{
    public class InvasionReinforcementsTests
    {
        private InvasionState _state = null!;
        private InvasionGroupState _group = null!;
        private CampaignStub _campaign = null!;
        private InvasionReinforcements _reinforcements = null!;

        [SetUp]
        public void SetUp()
        {
            _state = new InvasionState();
            _group = _state.RegisterMember("invasion", "clan", "capturer");
            _campaign = new CampaignStub();
            _reinforcements = new InvasionReinforcements(_state, _campaign, _ => 0);
        }

        [Test]
        public void OnlyTheFirstCoastalTownWindsThePersistentClock()
        {
            InvasionCapture capture = Conquer();
            _campaign.Now = 10;
            _campaign.Coastal = false;

            Assert.IsNull(_reinforcements.StartSchedule(capture.Id));
            Assert.That(_group.ReinforcementSchedules, Is.Empty);

            _campaign.Coastal = true;
            Assert.AreSame(_group, _reinforcements.StartSchedule(capture.Id));
            Assert.IsNull(_reinforcements.StartSchedule(capture.Id));
            Assert.AreEqual(17, Schedule(_group, "infantry").NextDeadlineDays);
            Assert.AreEqual(7, Schedule(_group, "infantry").IntervalDays);

            _state = RoundTrip();
            Assert.AreEqual(7, Schedule(_state.Groups.Single(), "infantry").IntervalDays);
            Assert.AreEqual(17, Schedule(_state.Groups.Single(), "infantry").NextDeadlineDays);
        }

        [Test]
        public void ADueClockDrawsOnceAndRearmsForTheNextInterval()
        {
            Wind();

            _campaign.Now = 16.9;
            Assert.That(_reinforcements.DrawDue(), Is.Empty);

            _campaign.Now = 17;
            InvasionReinforcementDraw draw = _reinforcements.DrawDue().Single();
            Assert.AreEqual(InvasionReinforcementOutcome.Landing, draw.Outcome);
            Assert.AreEqual("town", draw.LandingSettlementId);
            Assert.AreEqual(24, Schedule(_group, "infantry").NextDeadlineDays);
            Assert.That(_reinforcements.DrawDue(), Is.Empty);
        }

        [Test]
        public void AnOverdueClockDrawsOnlyOnceAndSchedulesOneFutureArrival()
        {
            Wind();

            _campaign.Now = 50;
            Assert.AreEqual(InvasionReinforcementOutcome.Landing, _reinforcements.DrawDue().Single().Outcome);
            Assert.AreEqual(57, Schedule(_group, "infantry").NextDeadlineDays);
            Assert.That(_reinforcements.DrawDue(), Is.Empty);
        }

        [Test]
        public void ARefusedDrawStillSpendsTheSlot()
        {
            Wind();

            _campaign.ActiveReinforcements["infantry"] = 2;
            _campaign.Now = 17;
            Assert.AreEqual(InvasionReinforcementOutcome.MaxConcurrentPartiesReached,
                _reinforcements.DrawDue().Single().Outcome);
            Assert.AreEqual(24, Schedule(_group, "infantry").NextDeadlineDays);

            _campaign.ActiveReinforcements["infantry"] = 0;
            _campaign.LandingSites.Clear();
            _campaign.Now = 24;
            Assert.AreEqual(InvasionReinforcementOutcome.NoLandingSite, _reinforcements.DrawDue().Single().Outcome);
            Assert.AreEqual(31, Schedule(_group, "infantry").NextDeadlineDays);
        }

        [Test]
        public void AnArrivalJoinsTheFirstInvasionClanThatStillHasRoomForAWarParty()
        {
            Wind();
            _group.AddClan("overflow-clan");
            _campaign.FullClans.Add("clan");

            _campaign.Now = 17;
            Assert.AreEqual("overflow-clan", _reinforcements.DrawDue().Single().ClanId);
        }

        [Test]
        public void AnArrivalWithEveryInvasionClanFullCarriesNoClanSoOneIsFounded()
        {
            Wind();
            _campaign.FullClans.Add("clan");

            _campaign.Now = 17;
            InvasionReinforcementDraw draw = _reinforcements.DrawDue().Single();
            Assert.AreEqual(InvasionReinforcementOutcome.Landing, draw.Outcome);
            Assert.IsNull(draw.ClanId);
        }

        [Test]
        public void EachRuleRunsItsOwnClockOnTheSameInvasion()
        {
            _campaign.Rules.Add(new InvasionReinforcementRule("cavalry", 20, 1));
            Wind();

            _campaign.Now = 17;
            Assert.AreEqual("infantry", _reinforcements.DrawDue().Single().RuleId);

            _campaign.Now = 24;
            Assert.AreEqual("infantry", _reinforcements.DrawDue().Single().RuleId);

            _campaign.Now = 30;
            Assert.AreEqual("cavalry", _reinforcements.DrawDue().Single().RuleId);
        }

        [Test]
        public void EachRuleCountsOnlyItsOwnPartiesAgainstItsOwnCeiling()
        {
            _campaign.Rules.Add(new InvasionReinforcementRule("cavalry", 7, 1));
            Wind();
            _campaign.ActiveReinforcements["infantry"] = 2;

            _campaign.Now = 17;
            IReadOnlyList<InvasionReinforcementDraw> draws = _reinforcements.DrawDue();

            Assert.AreEqual(new[] { "infantry", "cavalry" }, draws.Select(draw => draw.RuleId).ToArray());
            Assert.AreEqual(InvasionReinforcementOutcome.MaxConcurrentPartiesReached, draws[0].Outcome);
            Assert.AreEqual(InvasionReinforcementOutcome.Landing, draws[1].Outcome);
        }

        [Test]
        public void RulesDrawInConfigurationOrderWhateverOrderTheirClocksWereWound()
        {
            Wind();
            // A rule added to a running invasion winds later, yet still draws in the order it is declared.
            _campaign.Rules.Insert(0, new InvasionReinforcementRule("vanguard", 7, 1));
            _campaign.Now = 12;
            _reinforcements.StartSchedule(Conquer().Id);

            _campaign.Now = 19;
            Assert.AreEqual(new[] { "vanguard", "infantry" },
                _reinforcements.DrawDue().Select(draw => draw.RuleId).ToArray());
        }

        [Test]
        public void AClockWhoseRuleTheConfigurationDroppedSpendsItsSlotWithoutLanding()
        {
            Wind();
            _campaign.Rules.Clear();
            _campaign.Rules.Add(new InvasionReinforcementRule("cavalry", 7, 1));
            _reinforcements.StartSchedule(Conquer().Id);

            _campaign.Now = 17;
            IReadOnlyList<InvasionReinforcementDraw> draws = _reinforcements.DrawDue();

            Assert.AreEqual(new[] { "cavalry", "infantry" }, draws.Select(draw => draw.RuleId).ToArray());
            Assert.AreEqual(InvasionReinforcementOutcome.Landing, draws[0].Outcome);
            Assert.AreEqual(InvasionReinforcementOutcome.Unavailable, draws[1].Outcome);
            Assert.AreEqual(24, Schedule(_group, "infantry").NextDeadlineDays);
        }

        [Test]
        public void EveryRuleClockSurvivesASaveRoundTrip()
        {
            _campaign.Rules.Add(new InvasionReinforcementRule("cavalry", 20, 1));
            Wind();

            InvasionGroupState group = RoundTrip().Groups.Single();

            Assert.AreEqual(new[] { "infantry", "cavalry" },
                group.ReinforcementSchedules.Select(schedule => schedule.RuleId).ToArray());
            Assert.AreEqual(new[] { 7d, 20d },
                group.ReinforcementSchedules.Select(schedule => schedule.IntervalDays).ToArray());
            Assert.AreEqual(new[] { 17d, 30d },
                group.ReinforcementSchedules.Select(schedule => schedule.NextDeadlineDays).ToArray());
        }

        private void Wind()
        {
            _campaign.Now = 10;
            _reinforcements.StartSchedule(Conquer().Id);
        }

        private static InvasionReinforcementSchedule Schedule(InvasionGroupState group, string ruleId) =>
            group.ReinforcementSchedules.Single(schedule => schedule.RuleId == ruleId);

        private InvasionCapture Conquer()
        {
            var correlation = new InvasionCaptureCorrelation(_state);
            InvasionCapture capture = _state.StartCapture("town", 100, "capturer");
            correlation.ObserveOwnership(capture.Id, _group.Id, "capturer", "enemy", "clan", true);
            correlation.ObserveAftermath(capture.Id, "capturer", "enemy", new Dictionary<string, string>
            {
                ["capturer"] = _group.Id
            });
            return capture;
        }

        private InvasionState RoundTrip() => InvasionSaveData.FromState(_state).ToState();

        private sealed class CampaignStub : IInvasionReinforcementQuery
        {
            internal double Now;
            internal bool Coastal = true;
            internal readonly Dictionary<string, int> ActiveReinforcements = new();
            internal readonly List<InvasionReinforcementRule> Rules =
                new() { new InvasionReinforcementRule("infantry", 7, 2) };
            internal readonly List<string> LandingSites = new() { "town" };
            internal readonly HashSet<string> FullClans = new();

            public double NowInDays => Now;
            public IReadOnlyList<InvasionReinforcementRule>? GetRules(InvasionGroupState group) => Rules;
            public bool IsCoastalTown(string settlementId) => Coastal;
            public IReadOnlyList<string> GetLandingSites(InvasionGroupState group) => LandingSites;

            public int CountActiveReinforcements(InvasionGroupState group, string ruleId) =>
                ActiveReinforcements.TryGetValue(ruleId, out int count) ? count : 0;

            public bool HasPartySlot(string clanId) => !FullClans.Contains(clanId);
        }
    }
}
