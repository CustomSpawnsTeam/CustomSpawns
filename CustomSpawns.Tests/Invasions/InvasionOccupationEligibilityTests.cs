using System.Collections.Generic;
using CustomSpawns.Invasions;
using NUnit.Framework;

namespace CustomSpawns.Tests.Invasions
{
    public class InvasionOccupationEligibilityTests
    {
        private InvasionState _invasionState = null!;
        private Invasion _invasion = null!;
        private InvasionCapture _invasionCapture = null!;

        [SetUp]
        public void SetUp()
        {
            _invasionState = new InvasionState();
            _invasion = _invasionState.RegisterMember("invasion", "clan", "capturer");
            _invasionState.RegisterMember("invasion", "clan", "escort");
            _invasionCapture = _invasionState.StartCapture("town", 100, "capturer");
            _invasionState.ObserveCaptureOwnership(_invasionCapture.Id, "clan", "capturer", "enemy", "clan", true);
            _invasionState.ObserveCaptureAftermath(_invasionCapture.Id, "capturer", "enemy",
                new Dictionary<string, string>
                {
                    ["capturer"] = "clan",
                    ["escort"] = "clan"
                });
        }

        [Test]
        public void AConfirmedCaptureWithSafeSettlementAndParticipantsIsReadyWithoutBeingApplied()
        {
            Assert.True(_invasionState.IsOccupationReady(_invasionCapture.Id, "clan", (true, false, false),
                new[] { ("capturer", true, false), ("escort", true, false) }));

            Assert.False(_invasionCapture.Cancelled);
            Assert.False(_invasionCapture.OccupationApplied);
        }

        [TestCase(false, false, false)]
        [TestCase(true, true, false)]
        [TestCase(true, false, true)]
        public void MissingOrInactiveGarrisonBattleAndSiegeWaitBeforeObservingOwnershipOrParties(
            bool hasActiveGarrison, bool inBattle, bool underSiege)
        {
            Assert.False(_invasionState.IsOccupationReady(_invasionCapture.Id, "enemy",
                (hasActiveGarrison, inBattle, underSiege), UnexpectedParties()));
            Assert.False(_invasionCapture.Cancelled);
            Assert.IsNotNull(_invasionState.FindFoothold("town"));

            Assert.False(_invasionState.IsOccupationReady(_invasionCapture.Id, "enemy", (true, false, false),
                UnexpectedParties()));
            Assert.True(_invasionCapture.Cancelled);
            Assert.IsNull(_invasionState.FindFoothold("town"));
        }

        [TestCase("missing")]
        [TestCase("pending")]
        [TestCase("cancelled")]
        [TestCase("applied")]
        [TestCase("suspended")]
        public void AnIneligibleCaptureDoesNotEnumerateParties(string reason)
        {
            long captureId = _invasionCapture.Id;

            switch (reason)
            {
                case "missing":
                    captureId = long.MaxValue;
                    break;
                case "pending":
                    captureId = _invasionState.StartCapture("town", 200, "capturer").Id;
                    break;
                case "cancelled":
                    _invasionCapture.Cancelled = true;
                    break;
                case "applied":
                    _invasionState.CompleteOccupation(captureId, "clan");
                    break;
                case "suspended":
                    _invasion.IsSuspended = true;
                    break;
            }

            Assert.False(_invasionState.IsOccupationReady(captureId, "clan", (true, false, false),
                UnexpectedParties()));
        }

        [TestCase("capturer")]
        [TestCase("escort")]
        public void AnyActiveCopiedParticipantInBattleBlocksOccupationNotJustAnEscort(string partyId)
        {
            Assert.False(_invasionState.IsOccupationReady(_invasionCapture.Id, "clan", (true, false, false),
                new[] { (partyId, true, true) }));
        }

        [TestCase("escort", false)]
        [TestCase("unrelated", true)]
        [TestCase("unrelated", false)]
        [TestCase("late-member", true)]
        public void InactiveOrUncopiedPartiesDoNotBlockOccupationEvenWhenInBattle(string partyId, bool isActive)
        {
            _invasionState.RegisterMember("invasion", "clan", "late-member");

            Assert.True(_invasionState.IsOccupationReady(_invasionCapture.Id, "clan", (true, false, false),
                new[] { (partyId, isActive, true) }));
        }

        [Test]
        public void AParticipantThatIsOnlyBesiegingDoesNotBlockOccupation()
        {
            var party = (PartyId: "escort", IsActive: true, InBattle: false, UnderSiege: true);

            Assert.True(_invasionState.IsOccupationReady(_invasionCapture.Id, "clan", (true, false, false),
                new[] { (party.PartyId, party.IsActive, party.InBattle) }));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void AnActiveCopiedParticipantMustHaveTheEscortRoleToBeAbsorbed(bool isEscort)
        {
            int roleQueries = 0;

            bool eligible = _invasionCapture.CanAbsorbEscort("escort", true, () =>
            {
                roleQueries++;
                return isEscort;
            });

            Assert.AreEqual(isEscort, eligible);
            Assert.AreEqual(1, roleQueries);
        }

        [TestCase("escort", false)]
        [TestCase("unrelated", true)]
        [TestCase("unrelated", false)]
        public void InactiveOrUncopiedPartiesNeverQueryTheirRole(string partyId, bool isActive)
        {
            Assert.False(_invasionCapture.CanAbsorbEscort(partyId, isActive,
                () => throw new AssertionException("An ineligible party must not query its role.")));
        }

        [Test]
        public void EscortAbsorptionAddsNoCaptureOrInvasionEligibilityGates()
        {
            _invasionCapture.IsConfirmed = false;
            _invasionCapture.HasParticipants = false;
            _invasionCapture.Cancelled = true;
            _invasionCapture.OccupationApplied = true;
            _invasion.IsSuspended = true;

            Assert.True(_invasionCapture.CanAbsorbEscort("escort", true, () => true));
        }

        private static IEnumerable<(string PartyId, bool IsActive, bool IsInBattle)> UnexpectedParties()
        {
            Assert.Fail("Parties must not be enumerated before settlement and capture eligibility pass.");
            yield break;
        }
    }
}
