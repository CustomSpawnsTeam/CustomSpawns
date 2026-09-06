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

    // Pure terrain classification used by the campaign-map adapter.
    public static class InvasionCoastalTownClassification
    {
        public static bool IsMaritimeFace(InvasionCoastalFaceTerrain terrain)
        {
            return terrain == InvasionCoastalFaceTerrain.CoastalSea || terrain == InvasionCoastalFaceTerrain.OpenSea;
        }
    }
}
