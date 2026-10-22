using CustomSpawns.Invasions;
using NUnit.Framework;

namespace CustomSpawns.Tests.Invasions
{
    public class InvasionMembershipAdoptionTests
    {
        private InvasionState _state = null!;
        private Invasion _invasion = null!;

        [SetUp]
        public void SetUp()
        {
            _state = new InvasionState();
            _invasion = _state.RegisterMember("invasion", "clan", "invasion_party_1");
        }

        [Test]
        public void ARebuiltLordPartyOfAnInvadingClanIsAdoptedUnderItsInvasionTemplate()
        {
            Assert.AreEqual("invasion", _state.FindAdoptingTemplate("clan", false, true));
        }

        [Test]
        public void ALordOfAnUninvolvedClanIsNotAdopted()
        {
            Assert.IsNull(_state.FindAdoptingTemplate("other-clan", false, true));
            Assert.IsNull(_state.FindAdoptingTemplate(null, false, true));
        }

        [Test]
        public void APartyThisModAlreadyKnowsKeepsTheIdentityItWasRegisteredWith()
        {
            Assert.IsNull(_state.FindAdoptingTemplate("clan", true, true));
        }

        [Test]
        public void AGarrisonOrCaravanOfAnInvadingClanIsNotALordToAdopt()
        {
            Assert.IsNull(_state.FindAdoptingTemplate("clan", false, false));
        }

        [Test]
        public void AnExpandedClanTakesBackItsRebuiltLord()
        {
            _invasion.AddClan("expanded-clan");

            Assert.AreEqual("invasion", _state.FindAdoptingTemplate("expanded-clan", false, true));
        }

        [Test]
        public void ASuspendedInvasionDoesNotAdopt()
        {
            _invasion.IsSuspended = true;

            Assert.IsNull(_state.FindAdoptingTemplate("clan", false, true));
        }

        [Test]
        public void ASpentInvasionDoesNotAdopt()
        {
            _state.RemoveMember("invasion_party_1");

            Assert.IsNull(_state.FindAdoptingTemplate("clan", false, true));
        }

        [Test]
        public void AnInvasionHoldingAFootholdCanAdoptAfterLosingItsLastParty()
        {
            _state.RemoveMember("invasion_party_1");
            _state.FootholdIndex.Add("town", new InvasionFoothold("town", "clan", 1));

            Assert.AreEqual("invasion", _state.FindAdoptingTemplate("clan", false, true));
        }
    }
}
