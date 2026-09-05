using System;
using System.Collections.Generic;
using System.Linq;

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
        {
            Terrain = terrain;
            Center = center;
        }

        public InvasionCoastalFaceTerrain Terrain { get; }
        public InvasionCoastalPoint Center { get; }
    }

    // Pure policy shared by the campaign-map adapter and invasion lifecycle rules.
    public static class InvasionCoastalTownClassification
    {
        public static bool IsCoastalTown(bool isTown, bool hasPort, bool hasNearbyMaritimeCoast)
        {
            return isTown && (hasPort || hasNearbyMaritimeCoast);
        }

        public static IReadOnlyList<InvasionCoastalPoint> IndexMaritimeFaceCenters(
            IEnumerable<InvasionCoastalMapFace> faces)
        {
            return faces.Where(face => IsMaritimeFace(face.Terrain)).Select(face => face.Center).ToList();
        }

        public static bool IsMaritimeFace(InvasionCoastalFaceTerrain terrain)
        {
            return terrain == InvasionCoastalFaceTerrain.CoastalSea || terrain == InvasionCoastalFaceTerrain.OpenSea;
        }

        public static bool HasMaritimeFaceWithinRadius(IEnumerable<InvasionCoastalPoint> maritimeFaceCenters,
            InvasionCoastalPoint settlementPosition, InvasionCoastalPoint gatePosition, float radius,
            out float nearestDistanceSquared)
        {
            nearestDistanceSquared = float.PositiveInfinity;
            foreach (InvasionCoastalPoint faceCenter in maritimeFaceCenters)
            {
                nearestDistanceSquared = Math.Min(nearestDistanceSquared,
                    Math.Min(faceCenter.DistanceSquared(settlementPosition), faceCenter.DistanceSquared(gatePosition)));
            }
            return nearestDistanceSquared <= radius * radius;
        }
    }
}
