using CustomSpawns.Invasions;
using NUnit.Framework;

namespace CustomSpawns.Tests.Invasions
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
        public void AFollowerLandsInTheCoastalTownFarthestFromItsAllies()
        {
            InvasionLandingSite? site = _invasionLandingSiteChoice.FindLandingSite(new[]
            {
                new InvasionLandingSite("town_V1", "vlandia", 120f),
                new InvasionLandingSite("town_S1", "sturgia", 410f),
                new InvasionLandingSite("town_A1", "aserai", 260f)
            }, new[] { "khuzait" });

            Assert.That(site?.SettlementId, Is.EqualTo("town_S1"));
        }

        [Test]
        public void AFollowerPassesOverAKingdomItsAlliesAlreadyConquer()
        {
            InvasionLandingSite? site = _invasionLandingSiteChoice.FindLandingSite(new[]
            {
                new InvasionLandingSite("town_V1", "vlandia", 120f),
                new InvasionLandingSite("town_S1", "sturgia", 410f)
            }, new[] { "sturgia" });

            Assert.That(site?.KingdomId, Is.EqualTo("vlandia"));
        }

        [Test]
        public void AFollowerLandsInAnAlliedTargetWhenNoOtherKingdomHasACoast()
        {
            InvasionLandingSite? site = _invasionLandingSiteChoice.FindLandingSite(
                new[] { new InvasionLandingSite("town_S1", "sturgia", 410f) }, new[] { "sturgia" });

            Assert.That(site?.KingdomId, Is.EqualTo("sturgia"));
        }

        [Test]
        public void AFollowerHasNowhereToLandWithoutACoastalTown()
        {
            Assert.That(_invasionLandingSiteChoice.FindLandingSite(new InvasionLandingSite[0], new[] { "sturgia" }),
                Is.Null);
        }
    }
}
