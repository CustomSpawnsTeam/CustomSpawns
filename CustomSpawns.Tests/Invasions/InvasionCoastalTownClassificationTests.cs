using System.Collections.Generic;
using CustomSpawns.Invasions;
using NUnit.Framework;

namespace CustomSpawns.Tests.Invasions
{
    public class InvasionCoastalTownClassificationTests
    {
        [TestCase(InvasionCoastalFaceTerrain.Water)]
        [TestCase(InvasionCoastalFaceTerrain.CoastalSea)]
        [TestCase(InvasionCoastalFaceTerrain.OpenSea)]
        public void SeaAdjacentToCurrentSettlementFaceMakesTownCoastal(InvasionCoastalFaceTerrain seaTerrain)
        {
            InvasionCoastalMapFace settlementFace = Face(InvasionCoastalFaceTerrain.RuralArea, 1);
            var query = new StubFaceQuery().AdjacentTo(1, Face(seaTerrain, 2));

            bool isCoastal = InvasionCoastalTownClassification.IsSettlementAdjacentToSea(settlementFace, query);

            Assert.True(isCoastal);
        }

        [Test]
        public void TraversesEveryConnectedSettlementFaceBeforeCheckingItsBoundary()
        {
            InvasionCoastalMapFace firstSettlementFace = Face(InvasionCoastalFaceTerrain.RuralArea, 1);
            var query = new StubFaceQuery()
                .AdjacentTo(1, Face(InvasionCoastalFaceTerrain.Land, 10), Face(InvasionCoastalFaceTerrain.RuralArea, 2))
                .AdjacentTo(2, Face(InvasionCoastalFaceTerrain.RuralArea, 1), Face(InvasionCoastalFaceTerrain.RuralArea, 3))
                .AdjacentTo(3, Face(InvasionCoastalFaceTerrain.Water, 11));

            bool isCoastal = InvasionCoastalTownClassification.IsSettlementAdjacentToSea(firstSettlementFace, query);

            Assert.True(isCoastal);
            Assert.That(query.QueriedFaceIndices, Is.EquivalentTo(new[] { 1, 2, 3 }));
        }

        [TestCase(InvasionCoastalFaceTerrain.Land)]
        [TestCase(InvasionCoastalFaceTerrain.Lake)]
        [TestCase(InvasionCoastalFaceTerrain.River)]
        public void NonSeaBoundaryDoesNotMakeTownCoastal(InvasionCoastalFaceTerrain terrain)
        {
            InvasionCoastalMapFace settlementFace = Face(InvasionCoastalFaceTerrain.RuralArea, 1);
            var query = new StubFaceQuery().AdjacentTo(1, Face(terrain, 2));

            bool isCoastal = InvasionCoastalTownClassification.IsSettlementAdjacentToSea(settlementFace, query);

            Assert.False(isCoastal);
        }

        [Test]
        public void DisconnectedSettlementFaceIsNotPartOfTheTown()
        {
            InvasionCoastalMapFace settlementFace = Face(InvasionCoastalFaceTerrain.RuralArea, 1);
            var query = new StubFaceQuery()
                .AdjacentTo(1, Face(InvasionCoastalFaceTerrain.Land, 10))
                .AdjacentTo(2, Face(InvasionCoastalFaceTerrain.Water, 11));

            bool isCoastal = InvasionCoastalTownClassification.IsSettlementAdjacentToSea(settlementFace, query);

            Assert.False(isCoastal);
            Assert.That(query.QueriedFaceIndices, Is.EqualTo(new[] { 1 }));
        }

        [Test]
        public void CyclicSettlementFacesAreVisitedOnce()
        {
            InvasionCoastalMapFace settlementFace = Face(InvasionCoastalFaceTerrain.RuralArea, 1);
            var query = new StubFaceQuery()
                .AdjacentTo(1, Face(InvasionCoastalFaceTerrain.RuralArea, 2))
                .AdjacentTo(2, Face(InvasionCoastalFaceTerrain.RuralArea, 1));

            bool isCoastal = InvasionCoastalTownClassification.IsSettlementAdjacentToSea(settlementFace, query);

            Assert.False(isCoastal);
            Assert.That(query.QueriedFaceIndices, Is.EquivalentTo(new[] { 1, 2 }));
        }

        [Test]
        public void NonSettlementSeedIsRejectedWithoutQueryingAdjacentFaces()
        {
            var query = new StubFaceQuery();

            bool isCoastal = InvasionCoastalTownClassification.IsSettlementAdjacentToSea(
                Face(InvasionCoastalFaceTerrain.Land, 1), query);

            Assert.False(isCoastal);
            Assert.That(query.QueriedFaceIndices, Is.Empty);
        }

        private static InvasionCoastalMapFace Face(InvasionCoastalFaceTerrain terrain, int faceIndex) =>
            new(terrain, faceIndex);

        private sealed class StubFaceQuery : IInvasionCoastalMapFaceQuery
        {
            private readonly Dictionary<int, IReadOnlyCollection<InvasionCoastalMapFace>> _adjacentFaces = new();

            public List<int> QueriedFaceIndices { get; } = new();

            public StubFaceQuery AdjacentTo(int faceIndex, params InvasionCoastalMapFace[] adjacentFaces)
            {
                _adjacentFaces[faceIndex] = adjacentFaces;
                return this;
            }

            public IReadOnlyCollection<InvasionCoastalMapFace> GetAdjacentFaces(InvasionCoastalMapFace face)
            {
                QueriedFaceIndices.Add(face.FaceIndex);
                return _adjacentFaces.TryGetValue(face.FaceIndex,
                    out IReadOnlyCollection<InvasionCoastalMapFace>? adjacentFaces)
                    ? adjacentFaces
                    : new InvasionCoastalMapFace[0];
            }
        }
    }
}
