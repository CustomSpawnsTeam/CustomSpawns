using System.Collections.Generic;

namespace CustomSpawns.Invasions
{
    public enum InvasionCoastalFaceTerrain
    {
        Land,
        RuralArea,
        Lake,
        River,
        Water,
        CoastalSea,
        OpenSea
    }

    public readonly struct InvasionCoastalMapFace
    {
        public InvasionCoastalMapFace(InvasionCoastalFaceTerrain terrain, int faceIndex)
        {
            Terrain = terrain;
            FaceIndex = faceIndex;
        }

        public InvasionCoastalFaceTerrain Terrain { get; }
        public int FaceIndex { get; }
    }

    internal interface IInvasionCoastalMapFaceQuery
    {
        IReadOnlyCollection<InvasionCoastalMapFace> GetAdjacentFaces(InvasionCoastalMapFace face);
    }

    // Pure coastal rules. Bannerlord map access stays behind IInvasionCoastalMapFaceQuery.
    public static class InvasionCoastalTownClassification
    {
        public static bool IsMaritimeFace(InvasionCoastalFaceTerrain terrain)
        {
            return terrain == InvasionCoastalFaceTerrain.Water ||
                   terrain == InvasionCoastalFaceTerrain.CoastalSea ||
                   terrain == InvasionCoastalFaceTerrain.OpenSea;
        }

        internal static bool IsSettlementAdjacentToSea(
            InvasionCoastalMapFace settlementFace,
            IInvasionCoastalMapFaceQuery faceQuery)
        {
            if (settlementFace.Terrain != InvasionCoastalFaceTerrain.RuralArea)
                return false;

            var pendingSettlementFaces = new Queue<InvasionCoastalMapFace>();
            var visitedFaceIndices = new HashSet<int> { settlementFace.FaceIndex };
            pendingSettlementFaces.Enqueue(settlementFace);

            while (pendingSettlementFaces.Count > 0)
            {
                InvasionCoastalMapFace currentSettlementFace = pendingSettlementFaces.Dequeue();
                foreach (InvasionCoastalMapFace adjacentFace in faceQuery.GetAdjacentFaces(currentSettlementFace))
                {
                    if (IsMaritimeFace(adjacentFace.Terrain))
                        return true;

                    if (adjacentFace.Terrain == InvasionCoastalFaceTerrain.RuralArea &&
                        visitedFaceIndices.Add(adjacentFace.FaceIndex))
                        pendingSettlementFaces.Enqueue(adjacentFace);
                }
            }

            return false;
        }
    }
}
