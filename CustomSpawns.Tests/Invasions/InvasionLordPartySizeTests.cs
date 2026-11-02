using CustomSpawns.Invasions;
using Moq;
using NUnit.Framework;

namespace CustomSpawns.Tests.Invasions
{
    public class InvasionLordPartySizeTests
    {
        private const int Bonus = 50;
        private InvasionState _state = null!;
        private InvasionLordPartySize _invasionLordPartySize = null!;

        [SetUp]
        public void SetUp()
        {
            _state = new InvasionState();
            _state.RegisterMember("invasion", "clan", "party");
            Mock<IInvasionLordPartySizes> invasionLordPartySizes = new();
            invasionLordPartySizes.Setup(sizes => sizes.FindLordPartySizeBonus("invasion")).Returns(Bonus);
            _invasionLordPartySize = new InvasionLordPartySize(() => _state, invasionLordPartySizes.Object);
        }

        [Test]
        public void AnInvadingLordLeadsTheBonusOnTopOfVanilla()
        {
            Assert.That(_invasionLordPartySize.FindBonus("clan", true), Is.EqualTo(Bonus));
        }

        [Test]
        public void AnInvadingClansCaravanKeepsVanillasLimit()
        {
            Assert.That(_invasionLordPartySize.FindBonus("clan", false), Is.Zero);
        }

        [Test]
        public void AKingdomsLordKeepsVanillasLimit()
        {
            Assert.That(_invasionLordPartySize.FindBonus("kingdom_clan", true), Is.Zero);
        }

        [Test]
        public void ASettledInvasionsLordKeepsTheBonus()
        {
            _state.FindInvasion("clan")!.IsSuspended = true;

            Assert.That(_invasionLordPartySize.FindBonus("clan", true), Is.EqualTo(Bonus));
        }

        [Test]
        public void ASpentInvasionsLordGoesBackToVanillasLimit()
        {
            _state.RemoveMember("party");

            Assert.That(_invasionLordPartySize.FindBonus("clan", true), Is.Zero);
        }
    }
}
