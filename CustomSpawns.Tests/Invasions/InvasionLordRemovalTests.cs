using CustomSpawns.Invasions;
using CustomSpawns.Invasions.Adapters;
using NUnit.Framework;

namespace CustomSpawns.Tests.Invasions
{
    public class InvasionLordRemovalTests
    {
        private InvasionState _state = null!;
        private InvasionGroupState _group = null!;

        [SetUp]
        public void SetUp()
        {
            _state = new InvasionState();
            _group = _state.RegisterMember("invasion", "clan", "lord-party");
        }

        [Test]
        public void ALordOfAClanFightingAnInvasionIsRemoved()
        {
            Assert.True(IsRemovable());
        }

        [Test]
        public void ALordOfAClanTheInvasionExpandedIntoIsRemoved()
        {
            _group.AddClan("overflow-clan");

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
            _group.IsSuspended = true;

            Assert.False(IsRemovable());
        }

        [Test]
        public void ALordOfASpentInvasionIsNotRemoved()
        {
            _state.RemoveMember("lord-party");

            Assert.True(_state.IsGroupSpent(_group));
            Assert.False(IsRemovable());
        }

        /**
         * A living adult lord of a clan fighting a live invasion, so that every test states the one fact
         * that makes it the case it is named for and no test can pass on a second one.
         */
        private bool IsRemovable(string? clanId = "clan", bool isMainHero = false, bool belongsToPlayerClan = false,
            bool isChild = false, bool isAlive = true, bool isLord = true) =>
            InvasionLordRemoval.IsRemovableInvasionLord(_state, clanId,
                isMainHero: isMainHero,
                belongsToPlayerClan: belongsToPlayerClan,
                isChild: isChild,
                isAlive: isAlive,
                isLord: isLord);
    }
}
