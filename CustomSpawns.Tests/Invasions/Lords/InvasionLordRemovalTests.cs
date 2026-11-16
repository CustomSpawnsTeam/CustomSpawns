using System;
using CustomSpawns.Invasions;
using NUnit.Framework;

namespace CustomSpawns.Tests.Invasions.Lords
{
    public class InvasionLordRemovalTests
    {
        private InvasionState _state = null!;
        private Invasion _invasion = null!;

        [SetUp]
        public void SetUp()
        {
            _state = new InvasionState();
            _invasion = _state.RegisterMember("invasion", "clan", "lord-party");
        }

        [Test]
        public void ALordOfAClanFightingAnInvasionIsRemoved()
        {
            Assert.True(IsRemovable());
        }

        [Test]
        public void ALordOfAClanTheInvasionExpandedIntoIsRemoved()
        {
            _invasion.AddClan("overflow-clan");

            Assert.True(IsRemovable(clanId: "overflow-clan"));
        }

        [Test]
        public void ThePlayerIsNeverRemoved()
        {
            Assert.False(IsRemovable(isMainHero: true));
        }

        [Test]
        public void APlayerClanHeroIsNeverRemoved()
        {
            Assert.False(IsRemovable(belongsToPlayerClan: true));
        }

        [Test]
        public void AChildOfAnInvasionClanIsNeverRemoved()
        {
            Assert.False(IsRemovable(isChild: true));
        }

        [Test]
        public void AHeroOfAnInvasionClanThatIsNoLordIsNeverRemoved()
        {
            Assert.False(IsRemovable(isLord: false));
        }

        [Test]
        public void AnAlreadyDeadHeroIsNotRemovedAgain()
        {
            Assert.False(IsRemovable(isAlive: false));
        }

        [Test]
        public void ALordOfAClanThatFightsNoInvasionIsNotRemoved()
        {
            Assert.False(IsRemovable(clanId: "other-clan"));
        }

        [Test]
        public void AClanlessHeroIsNotRemoved()
        {
            Assert.False(IsRemovable(clanId: null));
        }

        [Test]
        public void ALordOfASuspendedInvasionIsNotRemoved()
        {
            _invasion.IsSuspended = true;

            Assert.False(IsRemovable());
        }

        [Test]
        public void ALordOfASpentInvasionIsNotRemoved()
        {
            _state.RemoveMember("lord-party");

            Assert.True(_state.IsInvasionSpent(_invasion));
            Assert.False(IsRemovable());
        }

        /**
         * The lord of the party whose loss spent the invasion: destroying the last party of an invasion is
         * what announces the defeat of its lord, so the ledger is already empty when the answer is wanted.
         */
        [Test]
        public void ALordThatJustLostTheLastPartyOfItsInvasionIsStillRemoved()
        {
            _state.RemoveMember("lord-party");

            Assert.True(_state.IsInvasionSpent(_invasion));
            Assert.True(IsRemovable(lostItsInvasionParty: true));
        }

        [Test]
        public void ThePlayerIsNeverRemovedEvenAfterLosingAnInvasionParty()
        {
            Assert.False(IsRemovable(isMainHero: true, lostItsInvasionParty: true));
        }

        /**
         * Losing an invasion party stands in for the answer the ledger can no longer give and for
         * nothing else, so every other guard still holds while it is true.
         */
        [Test]
        public void NoOtherGuardIsWaivedByTheLossOfAnInvasionParty()
        {
            Assert.False(IsRemovable(belongsToPlayerClan: true, lostItsInvasionParty: true));
            Assert.False(IsRemovable(isChild: true, lostItsInvasionParty: true));
            Assert.False(IsRemovable(isAlive: false, lostItsInvasionParty: true));
            Assert.False(IsRemovable(isLord: false, lostItsInvasionParty: true));
        }

        /**
         * The order the answer and the ledger are taken in: the forget here empties the invasion, which is
         * what destroying the last party of an invasion does, and the lord is still the answer because
         * it was asked for before that happened.
         */
        [Test]
        public void TheLordIsAnsweredForBeforeTheLedgerForgetsItsParty()
        {
            string? answer = _state.TakeLordOfRemovedInvasionParty("clan", "lord-party",
                "lord", () => _state.RemoveMember("lord-party"));

            Assert.AreEqual("lord", answer);
            Assert.True(_state.IsInvasionSpent(_invasion));
        }

        /**
         * Forgetting the party is the point of the call and happens whoever led it, so the answer being
         * nothing is only half of what is asserted here.
         */
        [Test]
        public void TheLordOfAPartyOfNoLiveInvasionIsNotAnsweredForButItsPartyIsStillForgotten()
        {
            Assert.IsNull(_state.TakeLordOfRemovedInvasionParty("other-clan",
                "lord-party", "lord", () => _state.RemoveMember("lord-party")));
            Assert.True(_state.IsInvasionSpent(_invasion));
        }

        [TestCase(null, "lord-party", "lord")]
        [TestCase("clan", "unregistered-party", "lord")]
        [TestCase("clan", "lord-party", null)]
        public void RejectedPartiesStillInvokeForgettingExactlyOnce(string? clanId, string partyId, string? leaderId)
        {
            int forgotten = 0;

            Assert.IsNull(_state.TakeLordOfRemovedInvasionParty(clanId, partyId, leaderId, () =>
            {
                forgotten++;
                _state.RemoveMember(partyId);
            }));

            Assert.AreEqual(1, forgotten);
            Assert.That(_invasion.MemberPartyIds, Does.Not.Contain(partyId));
        }

        [Test]
        public void TheLordOfASuspendedInvasionIsNotKeptWhenItsPartyIsForgotten()
        {
            _invasion.IsSuspended = true;

            Assert.IsNull(_state.TakeLordOfRemovedInvasionParty("clan", "lord-party", "lord",
                () => _state.RemoveMember("lord-party")));
            Assert.True(_state.IsInvasionSpent(_invasion));
        }

        [Test]
        public void TheLordIsKeptEvenWhenForgettingReconcilesAwayItsInvasion()
        {
            string? answer = _state.TakeLordOfRemovedInvasionParty("clan", "lord-party", "lord", () =>
            {
                _state.RemoveMember("lord-party");
                _state.Reconcile(Array.Empty<string>(), _ => null, _ => true, _ => false);
            });

            Assert.AreEqual("lord", answer);
            Assert.IsNull(_state.FindInvasion("clan"));
            Assert.True(IsRemovable(lostItsInvasionParty: true));
            Assert.False(IsRemovable());
        }

        [Test]
        public void ALeaderWithNoOtherLordIsSucceededBeforeRemoval()
        {
            Assert.True(_state.NeedsSuccessor(true, new[] { (true, false, true, true) }));
        }

        [Test]
        public void ALeaderWithAnotherLordLeavesSuccessionToVanilla()
        {
            Assert.False(_state.NeedsSuccessor(true,
                new[] { (true, false, true, true), (false, false, true, true) }));
        }

        [Test]
        public void ALordThatDoesNotLeadItsClanNeedsNoSuccessor()
        {
            Assert.False(_state.NeedsSuccessor(false, Array.Empty<(bool, bool, bool, bool)>()));
        }

        [TestCase(true, false, true, true, true)]
        [TestCase(false, true, true, true, true)]
        [TestCase(false, false, false, true, true)]
        [TestCase(false, false, true, false, true)]
        [TestCase(false, false, true, true, false)]
        public void OnlyAnotherLivingAdultLordPreventsASuccessor(bool isDeparting, bool isChild,
            bool isAlive, bool isLord, bool needsSuccessor)
        {
            Assert.AreEqual(needsSuccessor,
                _state.NeedsSuccessor(true, new[] { (isDeparting, isChild, isAlive, isLord) }));
        }

        [Test]
        public void AClanLeaderWithNoHeroEntriesNeedsASuccessor()
        {
            Assert.True(_state.NeedsSuccessor(true, Array.Empty<(bool, bool, bool, bool)>()));
        }

        /**
         * A living adult lord of a clan fighting a live invasion, so that every test states the one fact
         * that makes it the case it is named for and no test can pass on a second one.
         */
        private bool IsRemovable(string? clanId = "clan", bool isMainHero = false, bool belongsToPlayerClan = false,
            bool isChild = false, bool isAlive = true, bool isLord = true, bool lostItsInvasionParty = false) =>
            _state.IsRemovableInvasionLord(clanId,
                isMainHero: isMainHero,
                belongsToPlayerClan: belongsToPlayerClan,
                isChild: isChild,
                isAlive: isAlive,
                isLord: isLord,
                lostItsInvasionParty: lostItsInvasionParty);
    }
}
