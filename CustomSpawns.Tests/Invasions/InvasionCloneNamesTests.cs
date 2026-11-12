using CustomSpawns.Invasions;
using NUnit.Framework;

namespace CustomSpawns.Tests.Invasions
{
    public class InvasionCloneNamesTests
    {
        private readonly InvasionCloneNames _invasionCloneNames = new();

        [Test]
        public void TheFirstCopyIsTheSecondHorde()
        {
            Assert.That(_invasionCloneNames.Name("Humar", 1), Is.EqualTo("Second Humar Horde"));
        }

        [Test]
        public void TheSecondCopyIsTheThirdHorde()
        {
            Assert.That(_invasionCloneNames.Name("Humar", 2), Is.EqualTo("Third Humar Horde"));
        }

        [Test]
        public void TheEleventhCopyIsTheTwelfthHorde()
        {
            Assert.That(_invasionCloneNames.Name("Humar", 11), Is.EqualTo("Twelfth Humar Horde"));
        }

        [TestCase(12, "13th Humar Horde")]
        [TestCase(20, "21st Humar Horde")]
        [TestCase(21, "22nd Humar Horde")]
        [TestCase(22, "23rd Humar Horde")]
        [TestCase(110, "111th Humar Horde")]
        public void LaterCopiesAreNumbered(int cloneNumber, string expected)
        {
            Assert.That(_invasionCloneNames.Name("Humar", cloneNumber), Is.EqualTo(expected));
        }
    }
}
