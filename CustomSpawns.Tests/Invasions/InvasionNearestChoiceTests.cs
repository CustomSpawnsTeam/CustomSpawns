using CustomSpawns.Invasions;
using NUnit.Framework;

namespace CustomSpawns.Tests.Invasions
{
    public class InvasionNearestChoiceTests
    {
        private InvasionNearestChoice _invasionNearestChoice = new();

        [SetUp]
        public void SetUp()
        {
            _invasionNearestChoice = new InvasionNearestChoice();
        }

        [Test]
        public void TheNearestCandidateIsChosen()
        {
            Assert.That(_invasionNearestChoice.FindNearestId(new[]
            {
                new InvasionCandidateDistance("far", 30f), new InvasionCandidateDistance("near", 5f),
                new InvasionCandidateDistance("middle", 12f)
            }), Is.EqualTo("near"));
        }

        [Test]
        public void ATieGoesToTheFirstIdWhateverTheListedOrder()
        {
            Assert.That(_invasionNearestChoice.FindNearestId(new[]
            {
                new InvasionCandidateDistance("town_b", 5f), new InvasionCandidateDistance("town_a", 5f)
            }), Is.EqualTo("town_a"));
        }

        [Test]
        public void NoCandidateMeansNoChoice()
        {
            Assert.That(_invasionNearestChoice.FindNearestId(new InvasionCandidateDistance[0]), Is.Null);
        }
    }
}
