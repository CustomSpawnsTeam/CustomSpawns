using CustomSpawns.Invasions;
using Moq;
using NUnit.Framework;

namespace CustomSpawns.Tests.Invasions
{
    public class InvasionCoalitionWarsTests
    {
        private Mock<IInvasionCoalitionMembers> _invasionCoalitionMembers = null!;
        private Mock<IInvasionKingdomWars> _invasionKingdomWars = null!;

        [SetUp]
        public void SetUp()
        {
            _invasionCoalitionMembers = new Mock<IInvasionCoalitionMembers>();
            _invasionKingdomWars = new Mock<IInvasionKingdomWars>();
        }

        [Test]
        public void EveryKingdomOfACoalitionIsAtWarWithEveryRealmOfTheLine()
        {
            Members("sturgia", "battania");
            Invaders("humar", "humar_2");

            Wars().KeepWars();

            _invasionKingdomWars.Verify(w => w.DeclareWar("sturgia", "humar"), Times.Once);
            _invasionKingdomWars.Verify(w => w.DeclareWar("sturgia", "humar_2"), Times.Once);
            _invasionKingdomWars.Verify(w => w.DeclareWar("battania", "humar"), Times.Once);
            _invasionKingdomWars.Verify(w => w.DeclareWar("battania", "humar_2"), Times.Once);
            _invasionKingdomWars.VerifyNoOtherCalls();
        }

        [Test]
        public void NoWarIsDeclaredWhileNoCoalitionHolds()
        {
            Members();
            Invaders();

            Wars().KeepWars();

            _invasionKingdomWars.VerifyNoOtherCalls();
        }

        [Test]
        public void NoWarIsDeclaredOnceTheLineHasNoRealmLeft()
        {
            Members("sturgia");
            Invaders();

            Wars().KeepWars();

            _invasionKingdomWars.VerifyNoOtherCalls();
        }

        private InvasionCoalitionWars Wars() =>
            new(_invasionCoalitionMembers.Object, _invasionKingdomWars.Object);

        private void Members(params string[] members) =>
            _invasionCoalitionMembers.Setup(m => m.FindCoalitionMembers()).Returns(members);

        private void Invaders(params string[] invaders) =>
            _invasionCoalitionMembers.Setup(m => m.FindInvaders()).Returns(invaders);
    }
}
