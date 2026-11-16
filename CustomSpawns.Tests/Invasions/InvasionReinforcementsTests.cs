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
            _reinforcements = new InvasionReinforcements(_state, _campaign);
        }

        [Test]
        public void OnlyTheCaptureOfTheCapitalWindsThePersistentClock()
        {
            InvasionCapture capital = Conquer();
            _campaign.Now = 10;

            // Whatever else the invasion takes is held like any other foothold and winds no clock of its own.
            Assert.IsNull(_reinforcements.StartSchedule(Conquer("castle", 200).Id));
            Assert.That(_invasion.ReinforcementSchedules, Is.Empty);

            Assert.AreSame(_invasion, _reinforcements.StartSchedule(capital.Id));
            Assert.IsNull(_reinforcements.StartSchedule(capital.Id));
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
            Assert.AreEqual(InvasionReinforcementOutcome.Banking, _reinforcements.DrawDue().Single().Outcome);
            Assert.AreEqual(24, Schedule(_invasion, "infantry").NextDeadlineDays);
            Assert.That(_reinforcements.DrawDue(), Is.Empty);
        }

        [Test]
        public void AnOverdueClockDrawsOnlyOnceAndSchedulesOneFutureArrival()
        {
            Wind();

            _campaign.Now = 50;
            Assert.AreEqual(InvasionReinforcementOutcome.Banking, _reinforcements.DrawDue().Single().Outcome);
            Assert.AreEqual(57, Schedule(_invasion, "infantry").NextDeadlineDays);
            Assert.That(_reinforcements.DrawDue(), Is.Empty);
        }

        [Test]
        public void ARefusedDrawStillSpendsTheSlot()
        {
            Wind();

            _state.ObserveOwner("town", "enemy");
            _campaign.Now = 17;
            Assert.AreEqual(InvasionReinforcementOutcome.CapitalUnavailable,
                _reinforcements.DrawDue().Single().Outcome);
            Assert.AreEqual(24, Schedule(_invasion, "infantry").NextDeadlineDays);

            Conquer();
            _campaign.MaxBankedTroops = 900;
            _campaign.BankedTroops = 900;
            _campaign.Now = 24;
            Assert.AreEqual(InvasionReinforcementOutcome.BankFull,
                _reinforcements.DrawDue().Single().Outcome);
            Assert.AreEqual(31, Schedule(_invasion, "infantry").NextDeadlineDays);
        }

        [Test]
        public void ABesiegedCapitalIsSentNothingUntilTheSiegeIsLifted()
        {
            Wind();
            _campaign.CapitalBesieged = true;

            _campaign.Now = 17;
            Assert.AreEqual(InvasionReinforcementOutcome.CapitalBesieged, Deliver().Single().Outcome);
            Assert.AreEqual(24, Schedule(_invasion, "infantry").NextDeadlineDays);
            Assert.AreEqual(0, Schedule(_invasion, "infantry").WavesSent);

            _campaign.CapitalBesieged = false;
            _campaign.Now = 24;
            Assert.AreEqual(InvasionReinforcementOutcome.Banking, Deliver().Single().Outcome);
        }

        // Nothing arrives for an invasion that has no capital to send it to, or that has lost the one it took.
        [Test]
        public void LosingTheCapitalStopsTheWavesUntilItIsRetaken()
        {
            Wind();
            _state.ObserveOwner("town", "enemy");

            _campaign.Now = 17;
            Assert.AreEqual(InvasionReinforcementOutcome.CapitalUnavailable,
                _reinforcements.DrawDue().Single().Outcome);

            // Retaking it is a capture of its own, and the waves resume with it.
            Conquer();
            _campaign.Now = 24;
            Assert.AreEqual(InvasionReinforcementOutcome.Banking, _reinforcements.DrawDue().Single().Outcome);
        }

        [Test]
        public void EachRuleRunsItsOwnClockOnTheSameInvasion()
        {
            _campaign.Rules.Add(new InvasionReinforcementRule("cavalry", 20));
            Wind();

            _campaign.Now = 17;
            Assert.AreEqual("infantry", _reinforcements.DrawDue().Single().RuleId);

            _campaign.Now = 24;
            Assert.AreEqual("infantry", _reinforcements.DrawDue().Single().RuleId);

            _campaign.Now = 30;
            Assert.AreEqual("cavalry", _reinforcements.DrawDue().Single().RuleId);
        }

        [Test]
        public void RulesDrawInConfigurationOrderWhateverOrderTheirClocksWereWound()
        {
            Wind();
            // A rule added to a running invasion winds later, yet still draws in the order it is declared.
            _campaign.Rules.Insert(0, new InvasionReinforcementRule("vanguard", 7));
            _campaign.Now = 12;
            _reinforcements.StartSchedule(Conquer().Id);

            _campaign.Now = 19;
            Assert.AreEqual(new[] { "vanguard", "infantry" },
                _reinforcements.DrawDue().Select(draw => draw.RuleId).ToArray());
        }

        [Test]
        public void AClockWhoseRuleTheConfigurationDroppedSpendsItsSlotWithoutBanking()
        {
            Wind();
            _campaign.Rules.Clear();
            _campaign.Rules.Add(new InvasionReinforcementRule("cavalry", 7));
            _reinforcements.StartSchedule(Conquer().Id);

            _campaign.Now = 17;
            IReadOnlyList<InvasionReinforcementDraw> draws = _reinforcements.DrawDue().ToList();

            Assert.AreEqual(new[] { "cavalry", "infantry" }, draws.Select(draw => draw.RuleId).ToArray());
            Assert.AreEqual(InvasionReinforcementOutcome.Banking, draws[0].Outcome);
            Assert.AreEqual(InvasionReinforcementOutcome.Unavailable, draws[1].Outcome);
            Assert.AreEqual(24, Schedule(_invasion, "infantry").NextDeadlineDays);
        }

        [Test]
        public void EveryRuleClockSurvivesASaveRoundTrip()
        {
            _campaign.Rules.Add(new InvasionReinforcementRule("cavalry", 20));
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
        public void TheBankLimitRisesWithEveryTownHeldAndFallsAgainWhenOneIsLost()
        {
            // No configured limit at all leaves the bank bottomless, whatever it already holds.
            Assert.AreEqual(int.MaxValue, new InvasionBankLimit(0, 300, 2, 5000).Headroom);
            Assert.AreEqual(1700, new InvasionBankLimit(1700, 300, 0, 0).Headroom);
            Assert.AreEqual(2000, new InvasionBankLimit(1700, 300, 1, 0).Limit);
            Assert.AreEqual(800, new InvasionBankLimit(1700, 300, 2, 1500).Headroom);
            Assert.AreEqual(500, new InvasionBankLimit(1700, 300, 1, 1500).Headroom);
            Assert.AreEqual(-200, new InvasionBankLimit(1700, 300, 0, 1900).Headroom);
        }

        [Test]
        public void AnInvasionWithAFullBankSpendsTheSlotWithoutBanking()
        {
            Wind();
            _campaign.MaxBankedTroops = 900;
            _campaign.BankedTroops = 1200;

            _campaign.Now = 17;
            Assert.AreEqual(InvasionReinforcementOutcome.BankFull,
                _reinforcements.DrawDue().Single().Outcome);
            Assert.AreEqual(24, Schedule(_invasion, "infantry").NextDeadlineDays);
        }

        [Test]
        public void AWaveIsDrawnWithTheRoomLeftInTheBank()
        {
            Wind();
            _campaign.MaxBankedTroops = 900;
            _campaign.BankedTroops = 600;

            _campaign.Now = 17;
            Assert.AreEqual(300, _reinforcements.DrawDue().Single().BankHeadroom);
        }

        /**
         * The rules of one invasion share one bank, and each is decided only after the wave before it has
         * been banked, so the first rule of a day takes the room and a later one can find none left.
         */
        [Test]
        public void RulesDueTheSameDayDrawAgainstOneSharedBankInConfigurationOrder()
        {
            _campaign.Rules.Add(new InvasionReinforcementRule("cavalry", 7));
            Wind();
            _campaign.MaxBankedTroops = 500;
            _campaign.BankedTroopsPerTownHeld = 100;

            _campaign.Now = 17;
            var drawn = new List<string>();

            foreach (InvasionReinforcementDraw draw in _reinforcements.DrawDue())
            {
                drawn.Add(draw.RuleId + "=" + draw.Outcome);

                // What the first wave adds to the invasion is what the next one of the same day draws against.
                if (draw.Outcome == InvasionReinforcementOutcome.Banking)
                {
                    _campaign.BankedTroops += 600;
                }
            }

            Assert.AreEqual(new[] { "infantry=Banking", "cavalry=BankFull" }, drawn.ToArray());
            Assert.AreEqual(24, Schedule(_invasion, "cavalry").NextDeadlineDays);
        }

        // Losing the capital is the only thing that stops the waves; a siege does not.
        [Test]
        public void ABesiegedCapitalIsStillReinforced()
        {
            Wind();

            _campaign.Now = 17;
            Assert.AreEqual(InvasionReinforcementOutcome.Banking, _reinforcements.DrawDue().Single().Outcome);
        }

        /**
         * A rule allowed one wave is the one-off reinforcement: the town falls, a day later the wave
         * arrives, and no man comes after it however long the invasion lives. Its slots still come due, so
         * that the army it brought is paid for as long as the capital stands.
         */
        [Test]
        public void ARuleAllowedOneWaveSendsItADayAfterTheCaptureAndThenOnlyPays()
        {
            _campaign.Rules.Clear();
            _campaign.Rules.Add(new InvasionReinforcementRule("landing", 1, 1));
            Wind();

            _campaign.Now = 11;
            Assert.AreEqual(InvasionReinforcementOutcome.Banking, Deliver().Single().Outcome);

            _campaign.Now = 12;
            Assert.AreEqual(InvasionReinforcementOutcome.AllWavesSent, Deliver().Single().Outcome);
            _campaign.Now = 400;
            Assert.AreEqual(InvasionReinforcementOutcome.AllWavesSent, Deliver().Single().Outcome);
        }

        // Only waves that arrive are counted, so the one wave survives a day it could not be delivered on.
        [Test]
        public void ARefusedWaveDoesNotSpendTheRulesWaveCount()
        {
            _campaign.Rules.Clear();
            _campaign.Rules.Add(new InvasionReinforcementRule("landing", 1, 1));
            Wind();
            _state.ObserveOwner("town", "enemy");

            _campaign.Now = 11;
            Assert.AreEqual(InvasionReinforcementOutcome.CapitalUnavailable, Deliver().Single().Outcome);

            Conquer();
            _campaign.Now = 12;
            Assert.AreEqual(InvasionReinforcementOutcome.Banking, Deliver().Single().Outcome);
        }

        /**
         * A wave the capital turned out not to be able to take -- its garrison party gone by the time the
         * men were paid in -- is not counted either, so the one-off wave is still owed.
         */
        [Test]
        public void AWaveThatWasNeverPaidInDoesNotSpendTheRulesWaveCount()
        {
            _campaign.Rules.Clear();
            _campaign.Rules.Add(new InvasionReinforcementRule("landing", 1, 1));
            Wind();

            _campaign.Now = 11;
            Assert.AreEqual(InvasionReinforcementOutcome.Banking, _reinforcements.DrawDue().Single().Outcome);
            Assert.AreEqual(0, Schedule(_invasion, "landing").WavesSent);

            _campaign.Now = 12;
            Assert.AreEqual(InvasionReinforcementOutcome.Banking, Deliver().Single().Outcome);
        }

        [Test]
        public void WhatARuleHasAlreadySentSurvivesASaveRoundTrip()
        {
            _campaign.Rules.Clear();
            _campaign.Rules.Add(new InvasionReinforcementRule("landing", 1, 2));
            Wind();

            _campaign.Now = 11;
            Deliver();

            Assert.AreEqual(1, Schedule(RoundTrip().Invasions.Single(), "landing").WavesSent);
        }

        /**
         * What the adapter does with a day's draws: it walks them once, and tells the rules which of them
         * were actually paid into the capital's garrison.
         */
        private IReadOnlyList<InvasionReinforcementDraw> Deliver()
        {
            List<InvasionReinforcementDraw> draws = _reinforcements.DrawDue().ToList();

            foreach (InvasionReinforcementDraw draw in draws.Where(draw =>
                         draw.Outcome == InvasionReinforcementOutcome.Banking))
            {
                _reinforcements.RecordDelivered(draw);
            }

            return draws;
        }

        private void Wind()
        {
            _campaign.Now = 10;
            _reinforcements.StartSchedule(Conquer().Id);
        }

        private static InvasionReinforcementSchedule Schedule(Invasion invasion, string ruleId) =>
            invasion.ReinforcementSchedules.Single(schedule => schedule.RuleId == ruleId);

        private InvasionCapture Conquer(string settlementId = "town", int settlementIndex = 100)
        {
            InvasionCapture capture = _state.StartCapture(settlementId, settlementIndex, "capturer");
            _state.ObserveCaptureOwnership(capture.Id, _invasion.ClanId, "capturer", "enemy", "clan", true);
            _state.ObserveCaptureAftermath(capture.Id, "capturer", "enemy", new Dictionary<string, string>
            {
                ["capturer"] = _invasion.ClanId
            });
            return capture;
        }

        private InvasionState RoundTrip() => InvasionStateSaveData.FromState(_state).ToState();

        private sealed class CampaignStub : IInvasionReinforcementQuery
        {
            public double Now;
            public readonly List<InvasionReinforcementRule> Rules =
                new() { new InvasionReinforcementRule("infantry", 7) };
            public int MaxBankedTroops;
            public int BankedTroopsPerTownHeld;
            public int TownsHeld = 1;
            public int BankedTroops;
            public bool CapitalBesieged;

            public double NowInDays => Now;

            public bool IsCapitalBesieged(Invasion invasion) => CapitalBesieged;

            public InvasionBankLimit GetBankLimit(Invasion invasion) =>
                new(MaxBankedTroops, BankedTroopsPerTownHeld, TownsHeld, BankedTroops);

            public IReadOnlyList<InvasionReinforcementRule>? GetRules(Invasion invasion) => Rules;
        }
    }
}
