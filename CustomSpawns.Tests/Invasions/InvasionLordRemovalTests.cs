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
            Assert.True(InvasionLordRemoval.IsRemovableInvasionLord(_state, "clan", false, false));
        }

        [Test]
        public void ALordOfAClanTheInvasionExpandedIntoIsRemoved()
        {
            _group.AddClan("overflow-clan");

            Assert.True(InvasionLordRemoval.IsRemovableInvasionLord(_state, "overflow-clan", false, false));
        }

        [Test]
        public void ThePlayerIsNeverRemoved()
        {
            Assert.False(InvasionLordRemoval.IsRemovableInvasionLord(_state, "clan", true, true));
        }

        [Test]
        public void APlayerClanHeroIsNeverRemoved()
        {
            Assert.False(InvasionLordRemoval.IsRemovableInvasionLord(_state, "clan", false, true));
        }

        [Test]
        public void ALordOfAClanThatFightsNoInvasionIsNotRemoved()
        {
            Assert.False(InvasionLordRemoval.IsRemovableInvasionLord(_state, "other-clan", false, false));
        }

        [Test]
        public void AClanlessHeroIsNotRemoved()
        {
            Assert.False(InvasionLordRemoval.IsRemovableInvasionLord(_state, null, false, false));
        }

        [Test]
        public void ALordOfASuspendedInvasionIsNotRemoved()
        {
            _group.IsSuspended = true;

            Assert.False(InvasionLordRemoval.IsRemovableInvasionLord(_state, "clan", false, false));
        }

        [Test]
        public void ALordOfASpentInvasionIsNotRemoved()
        {
            _state.RemoveMember("lord-party");

            Assert.True(_state.IsGroupSpent(_group));
            Assert.False(InvasionLordRemoval.IsRemovableInvasionLord(_state, "clan", false, false));
        }
    }
}
