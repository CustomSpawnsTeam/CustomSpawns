using System.Collections.Generic;
using CustomSpawns.Invasions;
using Moq;
using NUnit.Framework;

namespace CustomSpawns.Tests.Invasions
{
    public class InvasionBankingUseCaseTests
    {
        private const string Capital = "town";
        private const int Floor = 50;

        private InvasionState _state = null!;
        private Invasion _invasion = null!;
        private Mock<IInvasionConfigurations> _invasionConfigurations = null!;
        private Mock<IInvasionWarbands> _invasionWarbands = null!;
        private Mock<IInvasionGarrisons> _invasionGarrisons = null!;
        private Mock<IInvasionGarrisonDeposits> _invasionGarrisonDeposits = null!;
        private Mock<IInvasionLords> _invasionLords = null!;
        private Mock<IInvasionTreasury> _invasionTreasury = null!;
        private Mock<IInvasionCommissariat> _invasionCommissariat = null!;
        private int _garrison;

        [SetUp]
        public void SetUp()
        {
            _state = new InvasionState();
            _invasion = _state.RegisterMember("invasion", "clan", "capturer");
            Conquer();

            _invasionConfigurations = new Mock<IInvasionConfigurations>();
            _invasionConfigurations.Setup(c => c.FindMinimumCapitalGarrison("invasion")).Returns(Floor);
            _invasionConfigurations.Setup(c => c.FindWave("invasion", "infantry"))
                .Returns(new InvasionWave("template", 7f, 2, 0, 0));

            // The garrison is a counter the withdrawals and deposits really move.
            _garrison = 100;
            _invasionGarrisons = new Mock<IInvasionGarrisons>();
            _invasionGarrisons.Setup(g => g.HasGarrison(Capital)).Returns(true);
            _invasionGarrisons.Setup(g => g.CountMen(Capital)).Returns(() => _garrison);
            _invasionGarrisonDeposits = new Mock<IInvasionGarrisonDeposits>();
            _invasionGarrisonDeposits.Setup(g => g.Deposit(Capital, "template", 0)).Returns(() =>
            {
                _garrison += 40;
                return 40;
            });

            _invasionWarbands = new Mock<IInvasionWarbands>();
            _invasionWarbands.Setup(w => w.DrawFromGarrison(It.IsAny<string>(), Capital, It.IsAny<int>()))
                .Returns((string _, string _, int men) =>
                {
                    _garrison -= men;
                    return men;
                });
            _invasionLords = new Mock<IInvasionLords>();
            _invasionTreasury = new Mock<IInvasionTreasury>();
            _invasionCommissariat = new Mock<IInvasionCommissariat>();
            _invasionCommissariat.Setup(c => c.FindMenPerFoodPerDay()).Returns(20);
        }

        [Test]
        public void ALordGivesBackHisFreeWarbandAndDrawsHisRoomFromTheBank()
        {
            _invasionWarbands.Setup(w => w.ReturnFreeTroops("party")).Returns(10);

            InvasionBankVisit visit = Bank().ReplaceFreeWarband(Warband(freeTroops: 10, room: 20))!.Value;

            Assert.IsFalse(visit.KeptFreeWarband);
            Assert.AreEqual(10, visit.Returned);
            Assert.AreEqual(30, visit.Drawn);
            Assert.AreEqual(70, visit.GarrisonLeft);
        }

        [Test]
        public void ALordKeepsHisFreeWarbandWhenTheBankCannotMatchIt()
        {
            _garrison = 55;

            InvasionBankVisit visit = Bank().ReplaceFreeWarband(Warband(freeTroops: 10, room: 20))!.Value;

            Assert.IsTrue(visit.KeptFreeWarband);
            Assert.AreEqual(0, visit.Drawn);
            _invasionWarbands.Verify(w => w.ReturnFreeTroops(It.IsAny<string>()), Times.Never);
            Assert.AreEqual(55, _garrison);
        }

        [Test]
        public void ALordWhoCameWithNoMenAndFindsTheBankShortIsLeftAlone()
        {
            _garrison = Floor - 1;

            Assert.IsNull(Bank().ReplaceFreeWarband(Warband(freeTroops: 0, room: 20)));
            _invasionWarbands.Verify(w => w.ReturnFreeTroops(It.IsAny<string>()), Times.Never);
        }

        // The garrison can hold fewer men than it counted, heroes or not, so what moved is what is reported.
        [Test]
        public void ARefillReportsTheMenThatActuallyMoved()
        {
            _invasionWarbands.Setup(w => w.DrawFromGarrison("party", Capital, 30)).Returns(12);

            InvasionBankVisit visit = Bank().Refill(Warband(settlementId: Capital, room: 30))!.Value;

            Assert.AreEqual(12, visit.Drawn);
        }

        [Test]
        public void ARefillNeverDrawsTheGarrisonBelowItsFloor()
        {
            _garrison = 60;

            InvasionBankVisit visit = Bank().Refill(Warband(settlementId: Capital, room: 30))!.Value;

            Assert.AreEqual(10, visit.Drawn);
            Assert.AreEqual(Floor, visit.GarrisonLeft);
            Assert.IsNull(Bank().Refill(Warband(settlementId: Capital, room: 30)));
        }

        [Test]
        public void OnlyALordStandingInTheCapitalRefills()
        {
            Assert.IsNull(Bank().Refill(Warband(settlementId: "elsewhere", room: 30)));
            Assert.IsNull(Bank().Refill(Warband(settlementId: null, room: 30)));
            Assert.AreEqual(100, _garrison);
        }

        [Test]
        public void TheBankIsClosedToAnythingButALordOfAnInvasionHoldingAnOpenCapital()
        {
            Assert.IsNull(Bank().Refill(Warband(settlementId: Capital, room: 30, isLordParty: false)));
            Assert.IsNull(Bank().Refill(Warband(settlementId: Capital, room: 30, clanId: "calradian")));

            _invasionGarrisons.Setup(g => g.HasGarrison(Capital)).Returns(false);
            Assert.IsNull(Bank().Refill(Warband(settlementId: Capital, room: 30)));
            _invasionGarrisons.Setup(g => g.HasGarrison(Capital)).Returns(true);

            _invasion.IsSuspended = true;
            _invasion.HasSpentBank = true;
            Assert.IsNull(Bank().Refill(Warband(settlementId: Capital, room: 30)));
            _invasion.IsSuspended = false;
            _invasion.HasSpentBank = false;

            _invasionConfigurations.Setup(c => c.FindMinimumCapitalGarrison("invasion")).Returns((int?)null);
            Assert.IsNull(Bank().Refill(Warband(settlementId: Capital, room: 30)));

            Assert.AreEqual(100, _garrison);
        }

        [Test]
        public void TheLordsWaitingInTheCapitalAllRefill()
        {
            _invasionWarbands.Setup(w => w.FindIn(Capital)).Returns(new List<InvasionWarband>
            {
                Warband("first", Capital, room: 20),
                Warband("caravan", Capital, room: 20, isLordParty: false),
                Warband("second", Capital, room: 0)
            });

            IReadOnlyList<InvasionBankVisit> visits = Bank().RefillAtCapitals();

            Assert.AreEqual(1, visits.Count);
            Assert.AreEqual("first", visits[0].Warband.PartyId);
            Assert.AreEqual(80, _garrison);
        }

        [Test]
        public void ALevyStopsAtTheLordCap()
        {
            _invasionLords.Setup(l => l.CountTownsHeld(_invasion)).Returns(1);
            _invasionLords.Setup(l => l.CountLivingLords(_invasion)).Returns(4);
            _invasionLords.Setup(l => l.Field(_invasion, Capital)).Returns(true);

            int fielded = Levy().Levy(_invasion, new InvasionWave("template", 7f, 3, 4, 1), Capital);

            Assert.AreEqual(1, fielded);
            _invasionLords.Verify(l => l.Field(_invasion, Capital), Times.Once);
        }

        [Test]
        public void ALevyStopsWhenNoClanHasRoomForAnotherLord()
        {
            _invasionLords.SetupSequence(l => l.Field(_invasion, Capital)).Returns(true).Returns(false);

            int fielded = Levy().Levy(_invasion, new InvasionWave("template", 7f, 5, 0, 0), Capital);

            Assert.AreEqual(1, fielded);
            _invasionLords.Verify(l => l.Field(_invasion, Capital), Times.Exactly(2));
        }

        [Test]
        public void AWavePaysEachClanForItsWarbandsAtFullStrength()
        {
            _invasion.AddClan("minor");
            _invasionTreasury.Setup(t => t.FindGarrisonWagePerMan(Capital)).Returns(2f);
            _invasionTreasury.Setup(t => t.FindPurse("clan")).Returns(Purse("clan", 0, false, 0, Upkeep("a", 100)));
            _invasionTreasury.Setup(t => t.FindPurse("minor")).Returns(Purse("minor", 0, true, 0, Upkeep("b", 100)));

            int paid = Funding().Fund(_invasion, 7f);

            // A 200 wage limit needs no reserve, so each clan is paid the week's wages alone.
            _invasionTreasury.Verify(t => t.Give("clan", 1400), Times.Once);
            _invasionTreasury.Verify(t => t.Give("minor", 1400), Times.Once);
            Assert.AreEqual(2800, paid);
        }

        [Test]
        public void AMinorClanNeedsASmallerReserveForTheSameWarband()
        {
            _invasion.AddClan("minor");
            _invasionTreasury.Setup(t => t.FindGarrisonWagePerMan(Capital)).Returns(6f);
            _invasionTreasury.Setup(t => t.FindPurse("clan")).Returns(Purse("clan", 0, false, 0, Upkeep("a", 100)));
            _invasionTreasury.Setup(t => t.FindPurse("minor")).Returns(Purse("minor", 0, true, 0, Upkeep("b", 100)));

            Funding().Fund(_invasion, 1f);

            _invasionTreasury.Verify(t => t.Give("clan", 30000 + 600), Times.Once);
            _invasionTreasury.Verify(t => t.Give("minor", 10000 + 600), Times.Once);
        }

        [Test]
        public void AGarrisonOfHeroesAloneIsPricedAtTheCampaignsAverageWage()
        {
            _invasionTreasury.Setup(t => t.FindGarrisonWagePerMan(Capital)).Returns((float?)null);
            _invasionTreasury.Setup(t => t.FindAverageWage()).Returns(3f);
            _invasionTreasury.Setup(t => t.FindPurse("clan")).Returns(Purse("clan", 0, false, 0, Upkeep("a", 100)));

            Funding().Fund(_invasion, 1f);

            // 100 men at 3 a day is a 300 wage limit, 15000 of reserve under the square-root band.
            _invasionTreasury.Verify(t => t.Give("clan", 15000 + 300), Times.Once);
        }

        [Test]
        public void ARichOrVanishedClanIsPaidNothing()
        {
            _invasion.AddClan("gone");
            _invasionTreasury.Setup(t => t.FindGarrisonWagePerMan(Capital)).Returns(2f);
            _invasionTreasury.Setup(t => t.FindPurse("clan"))
                .Returns(Purse("clan", 100000, false, 0, Upkeep("a", 100)));
            _invasionTreasury.Setup(t => t.FindPurse("gone")).Returns((InvasionClanPurse?)null);

            Assert.AreEqual(0, Funding().Fund(_invasion, 7f));
            _invasionTreasury.Verify(t => t.Give(It.IsAny<string>(), It.IsAny<int>()), Times.Never);
        }

        [Test]
        public void AWarbandIsFedForTheIntervalOrTheArmyMinimumWhicheverIsLonger()
        {
            _invasionCommissariat.Setup(c => c.FindMinimumFoodDaysForArmy()).Returns(9f);
            _invasionTreasury.Setup(t => t.FindPurse("clan"))
                .Returns(Purse("clan", 0, false, 0, Upkeep("hungry", 100, 5), Upkeep("fed", 100, 500)));

            Funding().Fund(_invasion, 7f);

            // 100 men at 20 a food eat 5 a day, for 9 + 1 days.
            _invasionCommissariat.Verify(c => c.AddFood("hungry", 45), Times.Once);
            _invasionCommissariat.Verify(c => c.AddFood("fed", It.IsAny<int>()), Times.Never);
        }

        [Test]
        public void AWaveIsDepositedThenLeviedThenFunded()
        {
            var calls = new List<string>();
            _invasionGarrisonDeposits.Setup(g => g.Deposit(Capital, "template", 0)).Returns(40)
                .Callback(() => calls.Add("deposit"));
            _invasionLords.Setup(l => l.Field(_invasion, Capital)).Returns(true).Callback(() => calls.Add("field"));
            _invasionCommissariat.Setup(c => c.FindMinimumFoodDaysForArmy()).Callback(() => calls.Add("fund"));

            InvasionWaveReceipt receipt = WaveBanking().Bank(Draw())!.Value;

            CollectionAssert.AreEqual(new[] { "deposit", "field", "field", "fund" }, calls);
            Assert.AreEqual(Capital, receipt.CapitalId);
            Assert.AreEqual(40, receipt.Men);
            Assert.AreEqual(2, receipt.Lords);
        }

        [Test]
        public void AWaveBanksNoMoreThanItsRuleAllows()
        {
            _invasionConfigurations.Setup(c => c.FindWave("invasion", "infantry"))
                .Returns(new InvasionWave("template", 7f, 0, 0, 0, 600));

            WaveBanking().Bank(Draw());

            _invasionGarrisonDeposits.Verify(g => g.Deposit(Capital, "template", 600), Times.Once);
        }

        [Test]
        public void AWaveIsNotBankedIntoAMissingRuleOrAClosedCapital()
        {
            _invasionGarrisons.Setup(g => g.HasGarrison(Capital)).Returns(false);
            Assert.IsNull(WaveBanking().Bank(Draw()));

            _invasionGarrisons.Setup(g => g.HasGarrison(Capital)).Returns(true);
            _invasionConfigurations.Setup(c => c.FindWave("invasion", "infantry")).Returns((InvasionWave?)null);
            Assert.IsNull(WaveBanking().Bank(Draw()));
            Assert.IsNull(WaveBanking().PayOnly(Draw(InvasionReinforcementOutcome.BankFull)));

            _invasionGarrisonDeposits.Verify(g => g.Deposit(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()), Times.Never);
        }

        [Test]
        public void AnInvasionWithAFullBankIsStillSentTheWavesLordsButNoMen()
        {
            _invasionLords.Setup(l => l.Field(_invasion, Capital)).Returns(true);

            Assert.AreEqual(2, WaveBanking().LevyLordsOnly(Draw(InvasionReinforcementOutcome.BankFull)));
            _invasionLords.Verify(l => l.Field(_invasion, Capital), Times.Exactly(2));
            _invasionGarrisonDeposits.Verify(g => g.Deposit(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()), Times.Never);
        }

        [Test]
        public void AnInvasionWithAFullBankIsStillHeldToItsLordCap()
        {
            _invasionConfigurations.Setup(c => c.FindWave("invasion", "infantry"))
                .Returns(new InvasionWave("template", 7f, 2, 3, 0));
            _invasionLords.Setup(l => l.CountLivingLords(_invasion)).Returns(3);
            _invasionLords.Setup(l => l.Field(_invasion, Capital)).Returns(true);

            Assert.AreEqual(0, WaveBanking().LevyLordsOnly(Draw(InvasionReinforcementOutcome.BankFull)));
            _invasionLords.Verify(l => l.Field(It.IsAny<Invasion>(), It.IsAny<string>()), Times.Never);
        }

        [TestCase(InvasionReinforcementOutcome.AllWavesSent)]
        [TestCase(InvasionReinforcementOutcome.CapitalUnavailable)]
        [TestCase(InvasionReinforcementOutcome.Unavailable)]
        [TestCase(InvasionReinforcementOutcome.Banking)]
        public void OnlyAnInvasionWithAFullBankIsSentLordsWithoutMen(InvasionReinforcementOutcome outcome)
        {
            _invasionLords.Setup(l => l.Field(_invasion, Capital)).Returns(true);

            Assert.AreEqual(0, WaveBanking().LevyLordsOnly(Draw(outcome)));
            _invasionLords.Verify(l => l.Field(It.IsAny<Invasion>(), It.IsAny<string>()), Times.Never);
        }

        [TestCase(InvasionReinforcementOutcome.BankFull)]
        [TestCase(InvasionReinforcementOutcome.AllWavesSent)]
        public void AFullOrExhaustedInvasionIsPaidButSentNoMenOrLords(InvasionReinforcementOutcome outcome)
        {
            _invasionTreasury.Setup(t => t.FindGarrisonWagePerMan(Capital)).Returns(2f);
            _invasionTreasury.Setup(t => t.FindPurse("clan")).Returns(Purse("clan", 0, false, 0, Upkeep("a", 100)));

            Assert.AreEqual(1400, WaveBanking().PayOnly(Draw(outcome)));
            _invasionTreasury.Verify(t => t.Give("clan", 1400), Times.Once);
            _invasionGarrisonDeposits.Verify(g => g.Deposit(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()), Times.Never);
            _invasionLords.Verify(l => l.Field(It.IsAny<Invasion>(), It.IsAny<string>()), Times.Never);
        }

        [TestCase(InvasionReinforcementOutcome.Unavailable)]
        [TestCase(InvasionReinforcementOutcome.CapitalUnavailable)]
        [TestCase(InvasionReinforcementOutcome.Banking)]
        public void OtherReinforcementOutcomesDoNotReceivePayOnlyFunding(InvasionReinforcementOutcome outcome)
        {
            _invasionTreasury.Setup(t => t.FindGarrisonWagePerMan(Capital)).Returns(2f);
            _invasionTreasury.Setup(t => t.FindPurse("clan")).Returns(Purse("clan", 0, false, 0, Upkeep("a", 100)));

            Assert.IsNull(WaveBanking().PayOnly(Draw(outcome)));
            _invasionTreasury.Verify(t => t.Give(It.IsAny<string>(), It.IsAny<int>()), Times.Never);
            _invasionCommissariat.Verify(c => c.AddFood(It.IsAny<string>(), It.IsAny<int>()), Times.Never);
            _invasionGarrisonDeposits.Verify(g => g.Deposit(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()), Times.Never);
            _invasionLords.Verify(l => l.Field(It.IsAny<Invasion>(), It.IsAny<string>()), Times.Never);
        }

        [Test]
        public void ASettledInvasionsLordsKeepSpendingItsBank()
        {
            _invasion.IsSuspended = true;

            InvasionBankVisit visit = Bank().Refill(Warband(settlementId: Capital, room: 30))!.Value;

            Assert.AreEqual(30, visit.Drawn);
            Assert.IsEmpty(Bank().CloseSpentBanks());
            Assert.IsFalse(_invasion.HasSpentBank);
        }

        [Test]
        public void ASettledInvasionsBankClosesForGoodOnceDrawnDownToTheMinimumGarrison()
        {
            _invasion.IsSuspended = true;
            _garrison = Floor;

            IReadOnlyList<InvasionBankClosure> closures = Bank().CloseSpentBanks();

            Assert.AreEqual(1, closures.Count);
            Assert.AreEqual(Floor, closures[0].GarrisonLeft);
            Assert.IsTrue(_invasion.HasSpentBank);

            _garrison = 100;
            Assert.IsNull(Bank().Refill(Warband(settlementId: Capital, room: 30)));
            Assert.IsEmpty(Bank().CloseSpentBanks());
        }

        [Test]
        public void ASettledInvasionThatLosesItsCapitalLosesItsBank()
        {
            _invasion.IsSuspended = true;
            _state.FootholdIndex.Remove(Capital);

            IReadOnlyList<InvasionBankClosure> closures = Bank().CloseSpentBanks();

            Assert.AreEqual(1, closures.Count);
            Assert.IsNull(closures[0].GarrisonLeft);
            Assert.IsTrue(_invasion.HasSpentBank);
        }

        [Test]
        public void AFightingInvasionsBankNeverCloses()
        {
            _garrison = Floor;

            Assert.IsEmpty(Bank().CloseSpentBanks());
            Assert.IsFalse(_invasion.HasSpentBank);
        }

        private InvasionWarbandBank Bank() => new(_state, _invasionConfigurations.Object, _invasionWarbands.Object,
            _invasionGarrisons.Object, new InvasionTroopBank());

        private InvasionLordLevy Levy() => new(_invasionLords.Object, new InvasionTroopBank());

        private InvasionWaveFunding Funding() =>
            new(_invasionTreasury.Object, _invasionCommissariat.Object, new InvasionWarChest());

        private InvasionWaveBanking WaveBanking() => new(_invasionConfigurations.Object, _invasionGarrisons.Object,
            _invasionGarrisonDeposits.Object, Levy(), Funding());

        private InvasionReinforcementDraw Draw(InvasionReinforcementOutcome outcome = InvasionReinforcementOutcome.Banking) =>
            new(_invasion, "infantry", outcome);

        private static InvasionWarband Warband(string partyId = "party", string? settlementId = null,
            int freeTroops = 0, int room = 0, bool isLordParty = true, string clanId = "clan") =>
            new(partyId, clanId, isLordParty, settlementId, freeTroops, room);

        private static InvasionWarband Warband(int freeTroops, int room) =>
            Warband("party", Capital, freeTroops, room);

        private static InvasionClanPurse Purse(string clanId, int gold, bool isMinorClan, int garrisonWages,
            params InvasionWarbandUpkeep[] warbands) =>
            new(clanId, gold, isMinorClan, garrisonWages, warbands);

        private static InvasionWarbandUpkeep Upkeep(string partyId, int sizeLimit, int food = 1000) =>
            new(partyId, sizeLimit, food);

        // The invasion holds the town and makes it its capital.
        private void Conquer()
        {
            _state.FootholdIndex[Capital] = new InvasionFoothold(Capital, _invasion.ClanId, 1);
            _invasion.CapitalSettlementId = Capital;
            Assume.That(_state.HoldsCapital(_invasion));
        }
    }
}
