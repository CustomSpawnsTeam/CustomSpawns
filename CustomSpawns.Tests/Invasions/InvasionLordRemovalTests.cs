using System.Runtime.Serialization;
using CustomSpawns.Invasions;
using CustomSpawns.Invasions.Adapters;
using NUnit.Framework;
using TaleWorlds.CampaignSystem;

namespace CustomSpawns.Tests.Invasions
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
            var lord = (Hero)FormatterServices.GetUninitializedObject(typeof(Hero));

            Hero? answer = InvasionLordRemoval.TakeLordOfRemovedInvasionParty(_state, "clan", "lord-party",
                lord, () => _state.RemoveMember("lord-party"));

            Assert.AreSame(lord, answer);
            Assert.True(_state.IsInvasionSpent(_invasion));
        }

        /**
         * Forgetting the party is the point of the call and happens whoever led it, so the answer being
         * nothing is only half of what is asserted here.
         */
        [Test]
        public void TheLordOfAPartyOfNoLiveInvasionIsNotAnsweredForButItsPartyIsStillForgotten()
        {
            var lord = (Hero)FormatterServices.GetUninitializedObject(typeof(Hero));

            Assert.IsNull(InvasionLordRemoval.TakeLordOfRemovedInvasionParty(_state, "other-clan",
                "lord-party", lord, () => _state.RemoveMember("lord-party")));
            Assert.True(_state.IsInvasionSpent(_invasion));
        }

        [Test]
        public void ALeaderWithNoOtherLordIsSucceededBeforeRemoval()
        {
            Assert.True(InvasionLordRemoval.NeedsSuccessor(isClanLeader: true, hasAnotherLivingAdultLord: false));
        }

        [Test]
        public void ALeaderWithAnotherLordLeavesSuccessionToVanilla()
        {
            Assert.False(InvasionLordRemoval.NeedsSuccessor(isClanLeader: true, hasAnotherLivingAdultLord: true));
        }

        [Test]
        public void ALordThatDoesNotLeadItsClanNeedsNoSuccessor()
        {
            Assert.False(InvasionLordRemoval.NeedsSuccessor(isClanLeader: false, hasAnotherLivingAdultLord: false));
        }

        /**
         * A living adult lord of a clan fighting a live invasion, so that every test states the one fact
         * that makes it the case it is named for and no test can pass on a second one.
         */
        private bool IsRemovable(string? clanId = "clan", bool isMainHero = false, bool belongsToPlayerClan = false,
            bool isChild = false, bool isAlive = true, bool isLord = true, bool lostItsInvasionParty = false) =>
            InvasionLordRemoval.IsRemovableInvasionLord(_state, clanId,
                isMainHero: isMainHero,
                belongsToPlayerClan: belongsToPlayerClan,
                isChild: isChild,
                isAlive: isAlive,
                isLord: isLord,
                lostItsInvasionParty: lostItsInvasionParty);
    }
}
