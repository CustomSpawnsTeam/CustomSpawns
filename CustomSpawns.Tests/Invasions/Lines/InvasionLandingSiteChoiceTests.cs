using CustomSpawns.Invasions.Lines;
using NUnit.Framework;

namespace CustomSpawns.Tests.Invasions.Lines
{
    public class InvasionLandingSiteChoiceTests
    {
        private InvasionLandingSiteChoice _invasionLandingSiteChoice = new();

        [SetUp]
        public void SetUp()
        {
            _invasionLandingSiteChoice = new InvasionLandingSiteChoice();
        }

        [Test]
        public void AFollowerLandsInTheCoastalTownNearestTheFirstRealm()
        {
            InvasionLandingSite? site = _invasionLandingSiteChoice.FindLandingSite(new[]
            {
                new InvasionLandingSite("town_S1", "sturgia", 410f),
                new InvasionLandingSite("town_V1", "vlandia", 120f),
                new InvasionLandingSite("town_A1", "aserai", 260f)
            });

            Assert.That(site?.SettlementId, Is.EqualTo("town_V1"));
        }

        [Test]
        public void AFollowerHasNowhereToLandWithoutACoastalTown()
        {
            Assert.That(_invasionLandingSiteChoice.FindLandingSite(new InvasionLandingSite[0]), Is.Null);
        }
    }
}
