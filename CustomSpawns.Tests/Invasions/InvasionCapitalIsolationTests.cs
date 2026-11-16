using CustomSpawns.Invasions;
using NUnit.Framework;

namespace CustomSpawns.Tests.Invasions
{
    public class InvasionCapitalIsolationTests
    {
        private InvasionCapitalIsolation _invasionCapitalIsolation = new();

        [SetUp]
        public void SetUp()
        {
            _invasionCapitalIsolation = new InvasionCapitalIsolation();
        }

        [Test]
        public void ACapitalWhoseNearestFiefsTheLineLostIsCutOff()
        {
            Assert.That(_invasionCapitalIsolation.IsIsolated(new[] { false, false, false, true }, 3), Is.True);
        }

        [Test]
        public void ACapitalWithOneOfThreeNearFiefsStillHeldIsCutOff()
        {
            Assert.That(_invasionCapitalIsolation.IsIsolated(new[] { false, true, false }, 3), Is.True);
        }

        [Test]
        public void ACapitalWithTwoOfThreeNearFiefsStillHeldIsNotCutOff()
        {
            Assert.That(_invasionCapitalIsolation.IsIsolated(new[] { true, false, true }, 3), Is.False);
        }

        [Test]
        public void ACapitalWithHalfOfItsNearFiefsStillHeldIsNotCutOff()
        {
            Assert.That(_invasionCapitalIsolation.IsIsolated(new[] { true, false }, 2), Is.False);
        }

        [Test]
        public void ARingOfOneCutsACapitalOffOnlyOnceItsNearestFiefIsLost()
        {
            Assert.That(_invasionCapitalIsolation.IsIsolated(new[] { true, false }, 1), Is.False);
        }

        [Test]
        public void FiefsBeyondTheRingAreNotCounted()
        {
            Assert.That(_invasionCapitalIsolation.IsIsolated(new[] { false, false, false, true, true }, 3), Is.True);
        }

        [Test]
        public void ARingOfZeroNeverCutsACapitalOff()
        {
            Assert.That(_invasionCapitalIsolation.IsIsolated(new[] { false, false, false }, 0), Is.False);
        }

        [Test]
        public void ACapitalWithFewerFiefsAroundThanTheRingIsCutOffOnceItHoldsNone()
        {
            Assert.That(_invasionCapitalIsolation.IsIsolated(new[] { false }, 3), Is.True);
        }
    }
}
