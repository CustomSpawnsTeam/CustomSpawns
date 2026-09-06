using System;
using CustomSpawns.Invasions;
using NUnit.Framework;

namespace CustomSpawns.Tests.Invasions
{
    public class InvasionCoastalTownClassificationTests
    {
        [Test]
        public void AnyCoveredNavMeshFaceAdjacentToSeaMakesSettlementCoastal()
        {
            var faces = new[]
            {
                Face(InvasionCoastalFaceTerrain.Land, -10f, 0f, 1),
                Face(InvasionCoastalFaceTerrain.Land, 10f, 0f, 2)
            };
            var query = new StubFaceQuery(position => position.X == 16f && position.Y == 0f
                ? Face(InvasionCoastalFaceTerrain.CoastalSea, position.X, position.Y, 3)
                : null);

            bool isCoastal = InvasionCoastalTownClassification.IsSettlementAdjacentToSea(
                faces, query, Point(0f, 0f), Point(0f, 0f), out float nearestDistanceSquared);

            Assert.True(isCoastal);
            Assert.That(nearestDistanceSquared, Is.EqualTo(100f));
        }

        [Test]
        public void FaceCoveredByTheTownGateIsConsidered()
        {
            var faces = new[] { Face(InvasionCoastalFaceTerrain.Land, 100f, 0f, 1) };
            var query = new StubFaceQuery(position =>
                Face(InvasionCoastalFaceTerrain.OpenSea, position.X, position.Y, 2));

            bool isCoastal = InvasionCoastalTownClassification.IsSettlementAdjacentToSea(
                faces, query, Point(0f, 0f), Point(100f, 0f), out float nearestDistanceSquared);

            Assert.True(isCoastal);
            Assert.That(nearestDistanceSquared, Is.Zero);
        }

        [TestCase(InvasionCoastalFaceTerrain.Land)]
        [TestCase(InvasionCoastalFaceTerrain.Lake)]
        [TestCase(InvasionCoastalFaceTerrain.River)]
        public void NonSeaAdjacentFacesDoNotMakeSettlementCoastal(InvasionCoastalFaceTerrain terrain)
        {
            var faces = new[] { Face(InvasionCoastalFaceTerrain.Land, 0f, 0f, 1) };
            var query = new StubFaceQuery(position => Face(terrain, position.X, position.Y, 2));

            bool isCoastal = InvasionCoastalTownClassification.IsSettlementAdjacentToSea(
                faces, query, Point(0f, 0f), Point(0f, 0f), out float nearestDistanceSquared);

            Assert.False(isCoastal);
            Assert.That(nearestDistanceSquared, Is.EqualTo(float.PositiveInfinity));
        }

        [Test]
        public void AdjacentSeaOutsideTownCoverageDoesNotCount()
        {
            var faces = new[] { Face(InvasionCoastalFaceTerrain.Land, 31f, 0f, 1) };
            var query = new StubFaceQuery(position =>
                Face(InvasionCoastalFaceTerrain.CoastalSea, position.X, position.Y, 2));

            bool isCoastal = InvasionCoastalTownClassification.IsSettlementAdjacentToSea(
                faces, query, Point(0f, 0f), Point(0f, 0f), out float nearestDistanceSquared);

            Assert.False(isCoastal);
            Assert.That(nearestDistanceSquared, Is.EqualTo(float.PositiveInfinity));
            Assert.That(query.CallCount, Is.Zero);
        }

        [TestCase(InvasionCoastalFaceTerrain.CoastalSea)]
        [TestCase(InvasionCoastalFaceTerrain.OpenSea)]
        public void CoveredMaritimeFaceCountsWithoutProbing(InvasionCoastalFaceTerrain terrain)
        {
            var faces = new[] { Face(terrain, 3f, 4f, 1) };
            var query = new StubFaceQuery(_ => null);

            bool isCoastal = InvasionCoastalTownClassification.IsSettlementAdjacentToSea(
                faces, query, Point(0f, 0f), Point(0f, 0f), out float nearestDistanceSquared);

            Assert.True(isCoastal);
            Assert.That(nearestDistanceSquared, Is.EqualTo(25f));
            Assert.That(query.CallCount, Is.Zero);
        }

        [Test]
        public void SourceFaceDoesNotCountAsItsOwnAdjacentSeaFace()
        {
            var faces = new[] { Face(InvasionCoastalFaceTerrain.Land, 0f, 0f, 1) };
            var query = new StubFaceQuery(position =>
                Face(InvasionCoastalFaceTerrain.CoastalSea, position.X, position.Y, 1));

            bool isCoastal = InvasionCoastalTownClassification.IsSettlementAdjacentToSea(
                faces, query, Point(0f, 0f), Point(0f, 0f), out _);

            Assert.False(isCoastal);
        }

        private static InvasionCoastalPoint Point(float x, float y) => new(x, y);

        private static InvasionCoastalMapFace Face(InvasionCoastalFaceTerrain terrain, float x, float y,
            int faceIndex) => new(terrain, Point(x, y), faceIndex);

        private sealed class StubFaceQuery : IInvasionCoastalMapFaceQuery
        {
            private readonly Func<InvasionCoastalPoint, InvasionCoastalMapFace?> _findFace;

            public StubFaceQuery(Func<InvasionCoastalPoint, InvasionCoastalMapFace?> findFace)
            {
                _findFace = findFace;
            }

            public int CallCount { get; private set; }

            public InvasionCoastalMapFace? FindFaceAt(InvasionCoastalPoint position)
            {
                CallCount++;
                return _findFace(position);
            }
        }
    }
}
