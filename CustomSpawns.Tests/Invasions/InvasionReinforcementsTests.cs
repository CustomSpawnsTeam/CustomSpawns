using System;
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
        private Invasion _invasion = null!;
        private CampaignStub _campaign = null!;
        private InvasionReinforcements _reinforcements = null!;

        [SetUp]
        public void SetUp()
        {
            _state = new InvasionState();
            _invasion = _state.RegisterMember("invasion", "clan", "capturer");
            _campaign = new CampaignStub();
            _reinforcements = new InvasionReinforcements(_state, _campaign, _ => 0);
        }

        [Test]
        public void OnlyTheFirstSupplyBaseWindsThePersistentClock()
        {
            InvasionCapture capture = Conquer();
            _campaign.Now = 10;
            _campaign.SupplyBase = false;

            Assert.IsNull(_reinforcements.StartSchedule(capture.Id));
            Assert.That(_invasion.ReinforcementSchedules, Is.Empty);

            _campaign.SupplyBase = true;
            Assert.AreSame(_invasion, _reinforcements.StartSchedule(capture.Id));
            Assert.IsNull(_reinforcements.StartSchedule(capture.Id));
            Assert.AreEqual(17, Schedule(_invasion, "infantry").NextDeadlineDays);
            Assert.AreEqual(7, Schedule(_invasion, "infantry").IntervalDays);

            _state = RoundTrip();
            Assert.AreEqual(7, Schedule(_state.Invasions.Single(), "infantry").IntervalDays);
            Assert.AreEqual(17, Schedule(_state.Invasions.Single(), "infantry").NextDeadlineDays);
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
            Assert.AreEqual(24, Schedule(_invasion, "infantry").NextDeadlineDays);
            Assert.That(_reinforcements.DrawDue(), Is.Empty);
        }

        [Test]
        public void AnOverdueClockDrawsOnlyOnceAndSchedulesOneFutureArrival()
        {
            Wind();

            _campaign.Now = 50;
            Assert.AreEqual(InvasionReinforcementOutcome.Landing, _reinforcements.DrawDue().Single().Outcome);
            Assert.AreEqual(57, Schedule(_invasion, "infantry").NextDeadlineDays);
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
            Assert.AreEqual(24, Schedule(_invasion, "infantry").NextDeadlineDays);

            _campaign.ActiveReinforcements["infantry"] = 0;
            _campaign.LandingSites.Clear();
            _campaign.Now = 24;
            Assert.AreEqual(InvasionReinforcementOutcome.NoLandingSite, _reinforcements.DrawDue().Single().Outcome);
            Assert.AreEqual(31, Schedule(_invasion, "infantry").NextDeadlineDays);
        }

        [Test]
        public void AnArrivalJoinsTheFirstInvasionClanThatStillHasRoomForAWarParty()
        {
            Wind();
            _invasion.AddClan("overflow-clan");
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
            IReadOnlyList<InvasionReinforcementDraw> draws = _reinforcements.DrawDue().ToList();

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
            IReadOnlyList<InvasionReinforcementDraw> draws = _reinforcements.DrawDue().ToList();

            Assert.AreEqual(new[] { "cavalry", "infantry" }, draws.Select(draw => draw.RuleId).ToArray());
            Assert.AreEqual(InvasionReinforcementOutcome.Landing, draws[0].Outcome);
            Assert.AreEqual(InvasionReinforcementOutcome.Unavailable, draws[1].Outcome);
            Assert.AreEqual(24, Schedule(_invasion, "infantry").NextDeadlineDays);
        }

        [Test]
        public void EveryRuleClockSurvivesASaveRoundTrip()
        {
            _campaign.Rules.Add(new InvasionReinforcementRule("cavalry", 20, 1));
            Wind();

            Invasion invasion = RoundTrip().Invasions.Single();

            Assert.AreEqual(new[] { "infantry", "cavalry" },
                invasion.ReinforcementSchedules.Select(schedule => schedule.RuleId).ToArray());
            Assert.AreEqual(new[] { 7d, 20d },
                invasion.ReinforcementSchedules.Select(schedule => schedule.IntervalDays).ToArray());
            Assert.AreEqual(new[] { 17d, 30d },
                invasion.ReinforcementSchedules.Select(schedule => schedule.NextDeadlineDays).ToArray());
        }

        [Test]
        public void TheCeilingRisesWithEveryTownHeldAndFallsAgainWhenOneIsLost()
        {
            // No configured ceiling at all leaves the invasion unbounded, whatever it already fields.
            Assert.AreEqual(double.PositiveInfinity,
                InvasionReinforcements.Headroom(new InvasionStrengthBudget(0, 300, 2, 5000)));
            Assert.AreEqual(900, InvasionReinforcements.Headroom(new InvasionStrengthBudget(900, 300, 0, 0)));
            Assert.AreEqual(900, InvasionReinforcements.Headroom(new InvasionStrengthBudget(900, 300, 2, 600)));
            Assert.AreEqual(600, InvasionReinforcements.Headroom(new InvasionStrengthBudget(900, 300, 1, 600)));
        }

        [Test]
        public void AnArrivalWorthMoreThanTheHeadroomIsCutBackToItInsteadOfBeingCancelled()
        {
            Assert.AreEqual(60, InvasionReinforcements.PermittedMemberCount(60, 600, 600));
            Assert.AreEqual(60, InvasionReinforcements.PermittedMemberCount(60, 600, double.PositiveInfinity));
            Assert.AreEqual(30, InvasionReinforcements.PermittedMemberCount(60, 600, 300));
            // Never cut to nothing: what barely fits still lands a man.
            Assert.AreEqual(1, InvasionReinforcements.PermittedMemberCount(60, 600, 1));
        }

        [Test]
        public void AnInvasionAtItsStrengthCeilingSpendsTheSlotWithoutLanding()
        {
            Wind();
            _campaign.MaxFactionStrength = 900;
            _campaign.CurrentStrength = 900;

            _campaign.Now = 17;
            Assert.AreEqual(InvasionReinforcementOutcome.FactionStrengthReached,
                _reinforcements.DrawDue().Single().Outcome);
            Assert.AreEqual(24, Schedule(_invasion, "infantry").NextDeadlineDays);
        }

        [Test]
        public void RulesDueTheSameDayDrawAgainstOneSharedHeadroomInConfigurationOrder()
        {
            _campaign.Rules.Clear();
            _campaign.Rules.Add(new InvasionReinforcementRule("first", 7, 1));
            _campaign.Rules.Add(new InvasionReinforcementRule("second", 7, 1));
            _campaign.Rules.Add(new InvasionReinforcementRule("third", 7, 1));
            Wind();
            // 500 of ceiling and one town held at 100 the town: 600 to share between the three rules.
            _campaign.MaxFactionStrength = 500;
            _campaign.StrengthPerTownHeld = 100;

            _campaign.Now = 17;
            var landed = new List<string>();

            foreach (InvasionReinforcementDraw draw in _reinforcements.DrawDue())
            {
                if (draw.Outcome != InvasionReinforcementOutcome.Landing)
                {
                    landed.Add(draw.RuleId + "=" + draw.Outcome);
                    continue;
                }

                // Each stream wants 400, trimmed to its share, and what it landed with is what the next
                // stream of the same day has to draw against.
                _campaign.CurrentStrength += Math.Min(400, draw.PermittedStrength);
                landed.Add(draw.RuleId + "=" + draw.PermittedStrength);
            }

            Assert.AreEqual(new[] { "first=600", "second=200", "third=FactionStrengthReached" }, landed.ToArray());
            Assert.AreEqual(24, Schedule(_invasion, "third").NextDeadlineDays);
        }

        private void Wind()
        {
            _campaign.Now = 10;
            _reinforcements.StartSchedule(Conquer().Id);
        }

        private static InvasionReinforcementSchedule Schedule(Invasion invasion, string ruleId) =>
            invasion.ReinforcementSchedules.Single(schedule => schedule.RuleId == ruleId);

        private InvasionCapture Conquer()
        {
            var correlation = new InvasionCaptureCorrelation(_state);
            InvasionCapture capture = _state.StartCapture("town", 100, "capturer");
            correlation.ObserveOwnership(capture.Id, _invasion.ClanId, "capturer", "enemy", "clan", true);
            correlation.ObserveAftermath(capture.Id, "capturer", "enemy", new Dictionary<string, string>
            {
                ["capturer"] = _invasion.ClanId
            });
            return capture;
        }

        private InvasionState RoundTrip() => InvasionStateSaveData.FromState(_state).ToState();

        private sealed class CampaignStub : IInvasionReinforcementQuery
        {
            internal double Now;
            internal bool SupplyBase = true;
            internal readonly Dictionary<string, int> ActiveReinforcements = new();
            internal readonly List<InvasionReinforcementRule> Rules =
                new() { new InvasionReinforcementRule("infantry", 7, 2) };
            internal readonly List<string> LandingSites = new() { "town" };
            internal readonly HashSet<string> FullClans = new();
            internal double MaxFactionStrength;
            internal double StrengthPerTownHeld;
            internal int TownsHeld = 1;
            internal double CurrentStrength;

            public double NowInDays => Now;

            public InvasionStrengthBudget GetStrengthBudget(Invasion invasion) =>
                new(MaxFactionStrength, StrengthPerTownHeld, TownsHeld, CurrentStrength);

            public IReadOnlyList<InvasionReinforcementRule>? GetRules(Invasion invasion) => Rules;
            public bool IsSupplyBase(string settlementId) => SupplyBase;
            public IReadOnlyList<string> GetLandingSites(Invasion invasion) => LandingSites;

            public int CountActiveReinforcements(Invasion invasion, string ruleId) =>
                ActiveReinforcements.TryGetValue(ruleId, out int count) ? count : 0;

            public bool HasPartySlot(string clanId) => !FullClans.Contains(clanId);
        }
    }
}
