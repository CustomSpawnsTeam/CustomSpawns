using System.Collections.Generic;
using CustomSpawns.Invasions;
using CustomSpawns.Invasions.Bank;
using CustomSpawns.Invasions.Reinforcements;
using Moq;
using NUnit.Framework;

namespace CustomSpawns.Tests.Invasions.Bank
{
    public sealed class InvasionResupplyTests
    {
        private const string Capital = "capital";
        private const int Floor = 400;

        private InvasionState _invasionState = null!;
        private Invasion _invasion = null!;
        private Mock<IInvasionConfigurations> _invasionConfigurations = null!;
        private Mock<IInvasionGarrisons> _invasionGarrisons = null!;
        private Mock<IInvasionResupplyRoutes> _invasionResupplyRoutes = null!;
        private InvasionResupply _invasionResupply = null!;

        [SetUp]
        public void SetUp()
        {
            _invasionState = new InvasionState();
            _invasion = _invasionState.RegisterMember("template", "founder", "landing");
            _invasion.CapitalSettlementId = Capital;
            _invasionState.FootholdIndex.Add(Capital, new InvasionFoothold(Capital, "founder", 1));
            _invasion.AddClan("daughter");

            _invasionConfigurations = new Mock<IInvasionConfigurations>(MockBehavior.Strict);
            _invasionConfigurations.Setup(c => c.FindMinimumCapitalGarrison("template")).Returns(Floor);
            _invasionGarrisons = new Mock<IInvasionGarrisons>(MockBehavior.Strict);
            _invasionGarrisons.Setup(g => g.HasGarrison(Capital)).Returns(true);
            _invasionGarrisons.Setup(g => g.CountMen(Capital)).Returns(600);
            _invasionResupplyRoutes = new Mock<IInvasionResupplyRoutes>(MockBehavior.Strict);
            _invasionResupplyRoutes.Setup(r => r.IsUnderSiege(Capital)).Returns(false);
            _invasionResupplyRoutes.Setup(r => r.CanReach(It.IsAny<string>(), Capital)).Returns(true);

            _invasionResupply = CreatePolicy();
        }

        [TestCase(0f, true)]
        [TestCase(0.5999f, true)]
        [TestCase(0.6f, true)]
        [TestCase(0.6001f, false)]
        [TestCase(0.79f, false)]
        [TestCase(0.8f, false)]
        [TestCase(1f, false)]
        [TestCase(1.2f, false)]
        public void ANewMissionStartsOnlyAtOrBelowTheArmyEligibilityRatio(float ratio, bool starts)
        {
            Assert.AreEqual(starts ? Capital : null, _invasionResupply.FindDestination(Party(ratio), 0.6f));
            Assert.AreEqual(starts ? 1 : 0, _invasionResupply.FindReturningParties().Count);

            if (!starts)
            {
                _invasionResupplyRoutes.VerifyNoOtherCalls();
            }
        }

        [TestCase(0.6f, 0.7999f, true)]
        [TestCase(0.6f, 0.8f, false)]
        [TestCase(0.6f, 0.8001f, false)]
        [TestCase(0.6f, 1f, false)]
        [TestCase(0.2f, 0.7999f, true)]
        [TestCase(0.2f, 0.8f, false)]
        [TestCase(0.75f, 0.8499f, true)]
        [TestCase(0.75f, 0.85f, false)]
        [TestCase(0.95f, 0.9999f, true)]
        [TestCase(0.95f, 1f, false)]
        public void AReturningLordContinuesUntilTheResumeRatio(float minimum, float ratio, bool continues)
        {
            Assert.AreEqual(Capital, _invasionResupply.FindDestination(Party(0.1f), minimum));

            Assert.AreEqual(continues ? Capital : null,
                _invasionResupply.FindDestination(Party(ratio), minimum));
            Assert.AreEqual(continues ? 1 : 0, _invasionResupply.FindReturningParties().Count);
        }

        [Test]
        public void ACompletedMissionDoesNotRestartWithinTheHysteresisBand()
        {
            _invasionResupply.FindDestination(Party(0.5f), 0.6f);
            Assert.IsNull(_invasionResupply.FindDestination(Party(0.8f), 0.6f));
            Assert.IsNull(_invasionResupply.FindDestination(Party(0.7f), 0.6f));
            Assert.AreEqual(Capital, _invasionResupply.FindDestination(Party(0.6f), 0.6f));
        }

        [Test]
        public void SavingAndRestoringKeepsAMissionThroughTheHysteresisBand()
        {
            _invasionResupply.FindDestination(Party(0.5f), 0.6f);
            InvasionResupply loaded = CreatePolicy();
            loaded.Restore(_invasionResupply.FindReturningParties());

            Assert.AreEqual(Capital, loaded.FindDestination(Party(0.7f), 0.6f));
            Assert.IsNull(loaded.FindDestination(Party(0.8f), 0.6f));
            Assert.IsEmpty(loaded.FindReturningParties());
        }

        [Test]
        public void RestoringReplacesAndDeduplicatesSavedMissions()
        {
            _invasionResupply.FindDestination(Party(0.5f), 0.6f);
            _invasionResupply.Restore(new[] { "replacement", "replacement", "second" });

            CollectionAssert.AreEquivalent(new[] { "replacement", "second" },
                _invasionResupply.FindReturningParties());
            Assert.IsNull(_invasionResupply.FindDestination(Party(0.7f), 0.6f));

            _invasionResupply.Restore(new string[0]);
            Assert.IsEmpty(_invasionResupply.FindReturningParties());
        }

        [Test]
        public void ReturnedMissionCollectionsAreSnapshots()
        {
            _invasionResupply.FindDestination(Party(0.5f), 0.6f);
            IReadOnlyCollection<string> saved = _invasionResupply.FindReturningParties();
            _invasionResupply.Remove("party");

            CollectionAssert.AreEqual(new[] { "party" }, saved);
            Assert.IsEmpty(_invasionResupply.FindReturningParties());
        }

        [Test]
        public void RemovingOneMissionLeavesOtherLordsReturning()
        {
            _invasionResupply.FindDestination(Party(0.5f), 0.6f);
            _invasionResupply.FindDestination(Party(0.5f, partyId: "second"), 0.6f);
            _invasionResupply.Remove("party");
            _invasionResupply.Remove("unknown");

            Assert.IsNull(_invasionResupply.FindDestination(Party(0.7f), 0.6f));
            Assert.AreEqual(Capital, _invasionResupply.FindDestination(Party(0.7f, partyId: "second"), 0.6f));
        }

        [TestCase("founder")]
        [TestCase("daughter")]
        public void EveryRecordedInvasionClanCanReturnWithoutRegisteringTheIndividualParty(string clanId)
        {
            Assert.AreEqual(Capital, _invasionResupply.FindDestination(Party(0.5f, clanId: clanId), 0.6f));
        }

        [TestCase("unrelated-kingdom-clan")]
        [TestCase(null)]
        public void AClanOutsideTheInvasionCannotUseItsBank(string? clanId)
        {
            Assert.IsNull(_invasionResupply.FindDestination(Party(0.5f, clanId: clanId), 0.6f));
            _invasionConfigurations.VerifyNoOtherCalls();
            _invasionGarrisons.VerifyNoOtherCalls();
            _invasionResupplyRoutes.VerifyNoOtherCalls();
        }

        [TestCase("not-lord")]
        [TestCase("no-room")]
        [TestCase("negative-room")]
        [TestCase("main")]
        [TestCase("army")]
        [TestCase("encounter")]
        [TestCase("disbanding")]
        [TestCase("locked")]
        [TestCase("invalid-ratio")]
        public void AnUnavailablePartyNeverStartsAndLosesAnExistingMission(string exclusion)
        {
            InvasionResupplyParty party = Party(0.5f, isLordParty: exclusion != "not-lord",
                room: exclusion == "no-room" ? 0 : exclusion == "negative-room" ? -1 : 20,
                isMainParty: exclusion == "main", isInArmy: exclusion == "army",
                isInEncounter: exclusion == "encounter", isDisbanding: exclusion == "disbanding",
                isMovementLocked: exclusion == "locked");

            if (exclusion == "invalid-ratio")
            {
                party = Party(float.NaN);
            }

            Assert.IsNull(_invasionResupply.FindDestination(party, 0.6f));
            _invasionResupply.Restore(new[] { "party" });
            Assert.IsNull(_invasionResupply.FindDestination(party, 0.6f));
            Assert.IsEmpty(_invasionResupply.FindReturningParties());
            _invasionConfigurations.VerifyNoOtherCalls();
            _invasionGarrisons.VerifyNoOtherCalls();
            _invasionResupplyRoutes.VerifyNoOtherCalls();
        }

        [TestCase("suspended")]
        [TestCase("capital-lost")]
        [TestCase("capital-unassigned")]
        [TestCase("wrong-capital-owner")]
        [TestCase("configuration-missing")]
        [TestCase("garrison-missing")]
        [TestCase("at-floor")]
        [TestCase("below-floor")]
        public void AClosedBankCancelsTheMissionBeforeAskingForARoute(string reason)
        {
            Assert.AreEqual(Capital, _invasionResupply.FindDestination(Party(0.5f), 0.6f));
            _invasionResupplyRoutes.Invocations.Clear();

            switch (reason)
            {
                case "suspended":
                    _invasion.IsSuspended = true;
                    break;
                case "capital-lost":
                    _invasionState.FootholdIndex.Remove(Capital);
                    break;
                case "capital-unassigned":
                    _invasion.CapitalSettlementId = null;
                    break;
                case "wrong-capital-owner":
                    _invasionState.FootholdIndex[Capital] = new InvasionFoothold(Capital, "other", 2);
                    break;
                case "configuration-missing":
                    _invasionConfigurations.Setup(c => c.FindMinimumCapitalGarrison("template"))
                        .Returns((int?)null);
                    break;
                case "garrison-missing":
                    _invasionGarrisons.Setup(g => g.HasGarrison(Capital)).Returns(false);
                    break;
                case "at-floor":
                    _invasionGarrisons.Setup(g => g.CountMen(Capital)).Returns(Floor);
                    break;
                case "below-floor":
                    _invasionGarrisons.Setup(g => g.CountMen(Capital)).Returns(Floor - 1);
                    break;
            }

            Assert.IsNull(_invasionResupply.FindDestination(Party(0.7f), 0.6f));
            Assert.IsEmpty(_invasionResupply.FindReturningParties());
            _invasionResupplyRoutes.VerifyNoOtherCalls();
            Assert.IsNull(_invasionResupply.FindDestination(Party(0.5f), 0.6f));
            _invasionResupplyRoutes.VerifyNoOtherCalls();
        }

        [Test]
        public void OneManAboveTheGarrisonFloorIsEnoughToMakeAUsefulJourney()
        {
            _invasionGarrisons.Setup(g => g.CountMen(Capital)).Returns(Floor + 1);

            Assert.AreEqual(Capital, _invasionResupply.FindDestination(Party(0.5f), 0.6f));
        }

        [Test]
        public void NoAvailableStockEndsTheMissionInsteadOfWaitingForTheNextWave()
        {
            _invasionResupply.FindDestination(Party(0.5f), 0.6f);
            _invasionGarrisons.Setup(g => g.CountMen(Capital)).Returns(Floor);
            Assert.IsNull(_invasionResupply.FindDestination(Party(0.7f), 0.6f));

            _invasionGarrisons.Setup(g => g.CountMen(Capital)).Returns(600);
            Assert.IsNull(_invasionResupply.FindDestination(Party(0.7f), 0.6f));
            Assert.AreEqual(Capital, _invasionResupply.FindDestination(Party(0.6f), 0.6f));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void ASiegedOrUnreachableCapitalCancelsTheJourney(bool underSiege)
        {
            _invasionResupply.FindDestination(Party(0.5f), 0.6f);
            _invasionResupplyRoutes.Invocations.Clear();
            _invasionResupplyRoutes.Setup(r => r.IsUnderSiege(Capital)).Returns(underSiege);
            _invasionResupplyRoutes.Setup(r => r.CanReach("party", Capital)).Returns(false);

            Assert.IsNull(_invasionResupply.FindDestination(Party(0.7f), 0.6f));
            Assert.IsEmpty(_invasionResupply.FindReturningParties());
            _invasionResupplyRoutes.Verify(r => r.CanReach("party", Capital),
                underSiege ? Times.Never() : Times.Once());

            Assert.IsNull(_invasionResupply.FindDestination(Party(0.5f), 0.6f));
        }

        [Test]
        public void ALordAlreadyInTheCapitalMayContinueRefillingUntilReady()
        {
            Assert.AreEqual(Capital,
                _invasionResupply.FindDestination(Party(0.5f, settlementId: Capital), 0.6f));
            Assert.AreEqual(Capital,
                _invasionResupply.FindDestination(Party(0.7f, settlementId: Capital), 0.6f));
            Assert.IsNull(_invasionResupply.FindDestination(Party(0.8f, settlementId: Capital), 0.6f));
        }

        [Test]
        public void AFullPartyNeverStartsEvenWithAnArmyThresholdOfOne()
        {
            Assert.IsNull(_invasionResupply.FindDestination(Party(1f), 1f));
            _invasionResupplyRoutes.VerifyNoOtherCalls();
        }

        private InvasionResupply CreatePolicy() => new(_invasionState, _invasionConfigurations.Object,
            _invasionGarrisons.Object, _invasionResupplyRoutes.Object);

        private InvasionResupplyParty Party(float ratio, string partyId = "party", string? clanId = "founder",
            bool isLordParty = true, int room = 20, string? settlementId = null, bool isMainParty = false,
            bool isInArmy = false, bool isInEncounter = false, bool isDisbanding = false,
            bool isMovementLocked = false) =>
            new(new InvasionWarband(partyId, clanId, isLordParty, settlementId, 50, room), ratio,
                isMainParty, isInArmy, isInEncounter, isDisbanding, isMovementLocked);
    }
}
