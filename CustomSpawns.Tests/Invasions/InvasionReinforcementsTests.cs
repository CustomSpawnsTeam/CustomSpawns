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
            InvasionCaptureContext capture = Conquer();
            _campaign.Now = 10;
            _campaign.Coastal = false;

            Assert.IsNull(_reinforcements.StartSchedule(capture.Id));
            Assert.AreEqual(0, _group.NextReinforcementDeadlineDays);

            _campaign.Coastal = true;
            Assert.AreSame(_group, _reinforcements.StartSchedule(capture.Id));
            Assert.IsNull(_reinforcements.StartSchedule(capture.Id));
            Assert.AreEqual(17, _group.NextReinforcementDeadlineDays);
            Assert.AreEqual(7, _group.ReinforcementIntervalDays);

            _state = RoundTrip();
            Assert.AreEqual(7, _state.Groups.Single().ReinforcementIntervalDays);
            Assert.AreEqual(17, _state.Groups.Single().NextReinforcementDeadlineDays);
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
            Assert.AreEqual(24, _group.NextReinforcementDeadlineDays);
            Assert.That(_reinforcements.DrawDue(), Is.Empty);
        }

        [Test]
        public void AnOverdueClockDrawsOnlyOnceAndSchedulesOneFutureArrival()
        {
            Wind();

            _campaign.Now = 50;
            Assert.AreEqual(InvasionReinforcementOutcome.Landing, _reinforcements.DrawDue().Single().Outcome);
            Assert.AreEqual(57, _group.NextReinforcementDeadlineDays);
            Assert.That(_reinforcements.DrawDue(), Is.Empty);
        }

        [Test]
        public void ARefusedDrawStillSpendsTheSlot()
        {
            Wind();

            _campaign.ActiveReinforcements = 2;
            _campaign.Now = 17;
            Assert.AreEqual(InvasionReinforcementOutcome.ActivePartyCapReached,
                _reinforcements.DrawDue().Single().Outcome);
            Assert.AreEqual(24, _group.NextReinforcementDeadlineDays);

            _campaign.ActiveReinforcements = 0;
            _campaign.LandingSites.Clear();
            _campaign.Now = 24;
            Assert.AreEqual(InvasionReinforcementOutcome.NoLandingSite, _reinforcements.DrawDue().Single().Outcome);
            Assert.AreEqual(31, _group.NextReinforcementDeadlineDays);
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

        private void Wind()
        {
            _campaign.Now = 10;
            _reinforcements.StartSchedule(Conquer().Id);
        }

        private InvasionCaptureContext Conquer()
        {
            var correlation = new InvasionCaptureCorrelation(_state);
            InvasionCaptureContext capture = _state.StartCapture("town", 100, "capturer");
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
            internal int ActiveReinforcements;
            internal readonly List<string> LandingSites = new() { "town" };
            internal readonly HashSet<string> FullClans = new();

            public double NowInDays => Now;
            public InvasionReinforcementSettings? GetSettings(InvasionGroupState group) => new(7, 2);
            public bool IsCoastalTown(string settlementId) => Coastal;
            public IReadOnlyList<string> GetLandingSites(InvasionGroupState group) => LandingSites;
            public int CountActiveReinforcements(InvasionGroupState group) => ActiveReinforcements;
            public bool HasPartySlot(string clanId) => !FullClans.Contains(clanId);
        }
    }
}
