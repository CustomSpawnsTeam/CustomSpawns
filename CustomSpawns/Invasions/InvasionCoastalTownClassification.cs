using System;
using System.Collections.Generic;

namespace CustomSpawns.Invasions
{
    public enum InvasionCoastalFaceTerrain
    {
        Land,
        Lake,
        River,
        CoastalSea,
        OpenSea
    }

    public readonly struct InvasionCoastalPoint
    {
        public InvasionCoastalPoint(float x, float y)
        {
            X = x;
            Y = y;
        }

        public float X { get; }
        public float Y { get; }

        public float DistanceSquared(InvasionCoastalPoint other)
        {
            float deltaX = X - other.X;
            float deltaY = Y - other.Y;
            return deltaX * deltaX + deltaY * deltaY;
        }
    }

    public readonly struct InvasionCoastalMapFace
    {
        public InvasionCoastalMapFace(InvasionCoastalFaceTerrain terrain, InvasionCoastalPoint center)
            : this(terrain, center, -1)
        {
        }

        public InvasionCoastalMapFace(InvasionCoastalFaceTerrain terrain, InvasionCoastalPoint center, int faceIndex)
        {
            Terrain = terrain;
            Center = center;
            FaceIndex = faceIndex;
        }

        public InvasionCoastalFaceTerrain Terrain { get; }
        public InvasionCoastalPoint Center { get; }
        public int FaceIndex { get; }
    }

    internal interface IInvasionCoastalMapFaceQuery
    {
        InvasionCoastalMapFace? FindFaceAt(InvasionCoastalPoint position);
    }

    // Pure coastal rules. Bannerlord map access stays behind IInvasionCoastalMapFaceQuery.
    public static class InvasionCoastalTownClassification
    {
        internal const float MaritimeScanRadius = 30f;
        private const float FaceAdjacencyProbeStep = 6f;
        private const float FaceAdjacencyProbeMaxDistance = 24f;

        private static readonly InvasionCoastalPoint[] FaceAdjacencyProbeDirections =
        {
            new(1f, 0f),
            new(-1f, 0f),
            new(0f, 1f),
            new(0f, -1f),
            new(1f, 1f),
            new(-1f, 1f),
            new(1f, -1f),
            new(-1f, -1f)
        };

        public static bool IsMaritimeFace(InvasionCoastalFaceTerrain terrain)
        {
            return terrain == InvasionCoastalFaceTerrain.CoastalSea || terrain == InvasionCoastalFaceTerrain.OpenSea;
        }

        internal static bool IsSettlementAdjacentToSea(
            IReadOnlyList<InvasionCoastalMapFace> navigationMeshFaces,
            IInvasionCoastalMapFaceQuery faceQuery,
            InvasionCoastalPoint settlementPosition,
            InvasionCoastalPoint gatePosition,
            out float nearestDistanceSquared)
        {
            nearestDistanceSquared = float.PositiveInfinity;
            bool hasCoast = false;
            float scanRadiusSquared = MaritimeScanRadius * MaritimeScanRadius;

            foreach (InvasionCoastalMapFace settlementFace in navigationMeshFaces)
            {
                float nearestSettlementDistance = Math.Min(
                    settlementFace.Center.DistanceSquared(settlementPosition),
                    settlementFace.Center.DistanceSquared(gatePosition));

                if (nearestSettlementDistance > scanRadiusSquared)
                    continue;

                if (!IsMaritimeFace(settlementFace.Terrain) &&
                    !HasAdjacentMaritimeFace(settlementFace, faceQuery))
                    continue;

                nearestDistanceSquared = Math.Min(nearestDistanceSquared, nearestSettlementDistance);
                hasCoast = true;
            }

            return hasCoast;
        }

        private static bool HasAdjacentMaritimeFace(InvasionCoastalMapFace sourceFace,
            IInvasionCoastalMapFaceQuery faceQuery)
        {
            foreach (InvasionCoastalPoint direction in FaceAdjacencyProbeDirections)
            {
                for (float distance = FaceAdjacencyProbeStep;
                     distance <= FaceAdjacencyProbeMaxDistance;
                     distance += FaceAdjacencyProbeStep)
                {
                    var samplePosition = new InvasionCoastalPoint(
                        sourceFace.Center.X + direction.X * distance,
                        sourceFace.Center.Y + direction.Y * distance);
                    InvasionCoastalMapFace? sampledFace = faceQuery.FindFaceAt(samplePosition);

                    if (!sampledFace.HasValue || sampledFace.Value.FaceIndex == sourceFace.FaceIndex)
                        continue;

                    if (IsMaritimeFace(sampledFace.Value.Terrain))
                        return true;
                }
            }

            return false;
        }
    }
}
