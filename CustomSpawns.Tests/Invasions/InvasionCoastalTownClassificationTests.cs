using CustomSpawns.Invasions;
using NUnit.Framework;

namespace CustomSpawns.Tests.Invasions
{
    public class InvasionCoastalTownClassificationTests
    {
        [TestCase(InvasionCoastalFaceTerrain.CoastalSea)]
        [TestCase(InvasionCoastalFaceTerrain.OpenSea)]
        public void SeaTerrainIsMaritime(InvasionCoastalFaceTerrain terrain)
        {
            Assert.True(InvasionCoastalTownClassification.IsMaritimeFace(terrain));
        }

        [TestCase(InvasionCoastalFaceTerrain.Land)]
        [TestCase(InvasionCoastalFaceTerrain.Lake)]
        [TestCase(InvasionCoastalFaceTerrain.River)]
        public void NonSeaTerrainIsNotMaritime(InvasionCoastalFaceTerrain terrain)
        {
            Assert.False(InvasionCoastalTownClassification.IsMaritimeFace(terrain));
        }

        [Test]
        public void PointDistanceSquaredUsesBothCoordinates()
        {
            var first = new InvasionCoastalPoint(1f, 2f);
            var second = new InvasionCoastalPoint(4f, 6f);

            Assert.That(first.DistanceSquared(second), Is.EqualTo(25f));
        }
    }
}
