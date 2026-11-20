using CustomSpawns.Invasions.Bank;
using NUnit.Framework;

namespace CustomSpawns.Tests.Invasions.Bank
{
    public class InvasionGarrisonDesertionTests
    {
        private const int MoraleThreshold = 10;
        private readonly InvasionGarrisonDesertion _invasionGarrisonDesertion = new();

        [Test]
        public void AnInvadersSteadyGarrisonKeepsItsTroops()
        {
            Assert.True(_invasionGarrisonDesertion.KeepsTroops(true, true, 48f, 0, MoraleThreshold));
        }

        [Test]
        public void AnInvadersBrokenGarrisonDesertsAsAnyOther()
        {
            Assert.False(_invasionGarrisonDesertion.KeepsTroops(true, true, 5f, 0, MoraleThreshold));
        }

        [Test]
        public void AKingdomsGarrisonKeepsVanillasDesertion()
        {
            Assert.False(_invasionGarrisonDesertion.KeepsTroops(true, false, 48f, 0, MoraleThreshold));
        }

        [Test]
        public void AnInvadersFieldPartyKeepsVanillasDesertion()
        {
            Assert.False(_invasionGarrisonDesertion.KeepsTroops(false, true, 48f, 0, MoraleThreshold));
        }

        [Test]
        public void ACrowdedBankIsNotBrokenByItsOwnCrowding()
        {
            // 1,495 men against a limit of 573: vanilla takes about 30 morale for the 922 over it.
            Assert.True(_invasionGarrisonDesertion.KeepsTroops(true, true, 0f, 922, MoraleThreshold));
        }

        [Test]
        public void ACrowdedBankBrokenBeyondItsCrowdingStillDeserts()
        {
            Assert.False(_invasionGarrisonDesertion.KeepsTroops(true, true, 0f, 16, MoraleThreshold));
        }

        [Test]
        public void APaidGarrisonLosesNobody()
        {
            Assert.That(_invasionGarrisonDesertion.UnpaidDesertions(0f, 200, 200), Is.Zero);
        }

        [Test]
        public void AnUnpaidGarrisonLosesFiveADayAsInVanilla()
        {
            Assert.That(_invasionGarrisonDesertion.UnpaidDesertions(350f, 200, 200), Is.EqualTo(5));
        }

        [Test]
        public void AnUnpaidGarrisonCannotLoseMoreThanItHas()
        {
            Assert.That(_invasionGarrisonDesertion.UnpaidDesertions(350f, 3, 2), Is.EqualTo(2));
        }
    }
}
