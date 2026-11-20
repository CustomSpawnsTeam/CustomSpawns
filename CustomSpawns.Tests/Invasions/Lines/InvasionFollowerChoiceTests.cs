using CustomSpawns.Invasions.Lines;
using NUnit.Framework;

namespace CustomSpawns.Tests.Invasions.Lines
{
    public class InvasionFollowerChoiceTests
    {
        private InvasionFollowerChoice _invasionFollowerChoice = new();

        [SetUp]
        public void SetUp()
        {
            _invasionFollowerChoice = new InvasionFollowerChoice();
        }

        [Test]
        public void TheFirstConfiguredFollowerNotInTheFieldIsLaunched()
        {
            Assert.That(_invasionFollowerChoice.FindConfiguredFollower(new[]
            {
                new InvasionFollowerCandidate("cs_humar_2", true, false),
                new InvasionFollowerCandidate("cs_humar_3", false, false),
                new InvasionFollowerCandidate("cs_humar_4", false, false)
            }), Is.EqualTo("cs_humar_3"));
        }

        [Test]
        public void AFollowerWhoseClanIsGoneIsPassedOver()
        {
            Assert.That(_invasionFollowerChoice.FindConfiguredFollower(new[]
            {
                new InvasionFollowerCandidate("cs_humar_2", false, true),
                new InvasionFollowerCandidate("cs_humar_3", false, false)
            }), Is.EqualTo("cs_humar_3"));
        }

        [Test]
        public void ALineWithEveryFollowerInTheFieldSendsACopy()
        {
            Assert.That(_invasionFollowerChoice.FindConfiguredFollower(new[]
            {
                new InvasionFollowerCandidate("cs_humar_2", true, false)
            }), Is.Null);
        }

        [Test]
        public void ALineWithNoConfiguredFollowerSendsACopy()
        {
            Assert.That(_invasionFollowerChoice.FindConfiguredFollower(new InvasionFollowerCandidate[0]), Is.Null);
        }
    }
}
