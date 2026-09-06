using System;
using System.Collections.Generic;
using NLog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace CustomSpawns.Invasions.Adapters
{
    // Bannerlord-specific map-face enumeration for the pure coastal-town policy.
    internal sealed class InvasionCoastalTownResolver
    {
        internal const float MaritimeScanRadius = 30f;
        private const string CoastalDiagnosticTownId = "town_V7";
        private const float FaceAdjacencyProbeStep = 6f;
        private const float FaceAdjacencyProbeMaxDistance = 24f;

        private static readonly TaleWorlds.Library.Vec2[] FaceAdjacencyProbeDirections = new[]
        {
            new TaleWorlds.Library.Vec2(1f, 0f),
            new TaleWorlds.Library.Vec2(-1f, 0f),
            new TaleWorlds.Library.Vec2(0f, 1f),
            new TaleWorlds.Library.Vec2(0f, -1f),
            new TaleWorlds.Library.Vec2(1f, 1f),
            new TaleWorlds.Library.Vec2(-1f, 1f),
            new TaleWorlds.Library.Vec2(1f, -1f),
            new TaleWorlds.Library.Vec2(-1f, -1f)
        };

        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly Dictionary<string, MaritimeCoastQuery> _nearbyMaritimeCoastBySettlementId = new();
        private IReadOnlyList<InvasionCoastalMapFace> _navigationMeshFaces = Array.Empty<InvasionCoastalMapFace>();
        private object? _mapScene;

        public bool IsCoastalTown(Settlement? settlement)
        {
            if (settlement == null) return false;
            bool isDiagnosticTown = settlement.StringId == CoastalDiagnosticTownId;
            bool requiresMapFallback = settlement.IsTown && !settlement.HasPort;
            MaritimeCoastQuery? maritimeCoast = requiresMapFallback || isDiagnosticTown
                ? GetNearbyMaritimeCoast(settlement)
                : null;
            bool coastal = InvasionCoastalTownClassification.IsCoastalTown(settlement.IsTown, settlement.HasPort,
                maritimeCoast?.HasCoast == true);
            if (isDiagnosticTown && maritimeCoast?.WasCalculated == true)
                Logger.Info("Invasion coastal classification: town=" + CoastalDiagnosticTownId +
                            " coastal=" + coastal + " has_port=" + settlement.HasPort +
                            " nearest_maritime_face_distance=" + maritimeCoast.Value.NearestDistance +
                            " radius=" + MaritimeScanRadius + " source=native_or_map");
            return coastal;
        }

        public bool IsCoastalTown(string settlementId)
        {
            return IsCoastalTown(Settlement.Find(settlementId));
        }

        public void ClearCache()
        {
            _mapScene = null;
            _nearbyMaritimeCoastBySettlementId.Clear();
            _navigationMeshFaces = Array.Empty<InvasionCoastalMapFace>();
        }

        private MaritimeCoastQuery GetNearbyMaritimeCoast(Settlement settlement)
        {
            EnsureMaritimeFaceIndex();
            if (_nearbyMaritimeCoastBySettlementId.TryGetValue(settlement.StringId, out MaritimeCoastQuery cached))
                return cached.AsCached();

            var settlementPosition = ToCoastalPoint(settlement.Position.ToVec2());
            var gatePosition = ToCoastalPoint(settlement.GatePosition.ToVec2());
            bool hasCoast = IsSettlementAdjacentToSeaByNavMesh(settlementPosition, gatePosition, out float nearestDistanceSquared);
            var result = new MaritimeCoastQuery(hasCoast, float.IsPositiveInfinity(nearestDistanceSquared)
                ? float.PositiveInfinity
                : (float)Math.Sqrt(nearestDistanceSquared), wasCalculated: true);
            _nearbyMaritimeCoastBySettlementId[settlement.StringId] = result;
            return result;
        }

        private void EnsureMaritimeFaceIndex()
        {
            object mapScene = Campaign.Current.MapSceneWrapper;
            if (ReferenceEquals(_mapScene, mapScene)) return;

            _mapScene = mapScene;
            _nearbyMaritimeCoastBySettlementId.Clear();
            var navigationMeshFaces = new List<InvasionCoastalMapFace>();
            int faceCount = Campaign.Current.MapSceneWrapper.GetNumberOfNavigationMeshFaces();
            for (int index = 0; index < faceCount; index++)
            {
                PathFaceRecord face = Campaign.Current.MapSceneWrapper.GetFaceAtIndex(index);
                if (!face.IsValid()) continue;
                InvasionCoastalFaceTerrain terrain = ToCoastalFaceTerrain(Campaign.Current.MapSceneWrapper.GetFaceTerrainType(face));
                Vec2 faceCenter = Campaign.Current.MapSceneWrapper.GetNavigationMeshCenterPosition(index);
                navigationMeshFaces.Add(new InvasionCoastalMapFace(terrain,
                    ToCoastalPoint(faceCenter),
                    face.FaceIndex));
            }
            _navigationMeshFaces = navigationMeshFaces;
        }

        private bool IsSettlementAdjacentToSeaByNavMesh(
            InvasionCoastalPoint settlementPosition, InvasionCoastalPoint gatePosition, out float nearestDistanceSquared)
        {
            nearestDistanceSquared = float.PositiveInfinity;
            bool hasCoast = false;
            float scanRadiusSquared = MaritimeScanRadius * MaritimeScanRadius;

            foreach (InvasionCoastalMapFace settlementFace in _navigationMeshFaces)
            {
                float nearestSettlementDistance = Math.Min(
                    settlementFace.Center.DistanceSquared(settlementPosition),
                    settlementFace.Center.DistanceSquared(gatePosition));

                if (nearestSettlementDistance > scanRadiusSquared)
                    continue;

                if (InvasionCoastalTownClassification.IsMaritimeFace(settlementFace.Terrain))
                {
                    nearestDistanceSquared = Math.Min(nearestDistanceSquared, nearestSettlementDistance);
                    hasCoast = true;
                    continue;
                }

                if (AdjacentFaceIsSea(settlementFace.FaceIndex, settlementFace.Center.X, settlementFace.Center.Y))
                {
                    nearestDistanceSquared = Math.Min(nearestDistanceSquared, nearestSettlementDistance);
                    hasCoast = true;
                }
            }

            return hasCoast;
        }

        private bool AdjacentFaceIsSea(int sourceFaceIndex, float sourceX, float sourceY)
        {
            foreach (TaleWorlds.Library.Vec2 direction in FaceAdjacencyProbeDirections)
            {
                for (float distance = FaceAdjacencyProbeStep;
                     distance <= FaceAdjacencyProbeMaxDistance;
                     distance += FaceAdjacencyProbeStep)
                {
                    float sampleX = sourceX + direction.x * distance;
                    float sampleY = sourceY + direction.y * distance;
                    CampaignVec2 samplePosition = new CampaignVec2(new Vec2(sampleX, sampleY), false);
                    PathFaceRecord sampledFace = Campaign.Current.MapSceneWrapper.GetFaceIndex(ref samplePosition);

                    if (!sampledFace.IsValid() || sampledFace.FaceIndex == sourceFaceIndex)
                        continue;

                    InvasionCoastalFaceTerrain terrain = ToCoastalFaceTerrain(
                        Campaign.Current.MapSceneWrapper.GetFaceTerrainType(sampledFace));
                    if (InvasionCoastalTownClassification.IsMaritimeFace(terrain))
                        return true;
                }
            }

            return false;
        }

        private static InvasionCoastalPoint ToCoastalPoint(Vec2 position) =>
            new(position.x, position.y);

        private static InvasionCoastalFaceTerrain ToCoastalFaceTerrain(TerrainType terrain)
        {
            return terrain == TerrainType.CoastalSea ? InvasionCoastalFaceTerrain.CoastalSea :
                terrain == TerrainType.OpenSea ? InvasionCoastalFaceTerrain.OpenSea :
                terrain == TerrainType.Lake ? InvasionCoastalFaceTerrain.Lake :
                terrain == TerrainType.River ? InvasionCoastalFaceTerrain.River :
                InvasionCoastalFaceTerrain.Land;
        }

        private readonly struct MaritimeCoastQuery
        {
            public MaritimeCoastQuery(bool hasCoast, float nearestDistance, bool wasCalculated)
            {
                HasCoast = hasCoast;
                NearestDistance = nearestDistance;
                WasCalculated = wasCalculated;
            }

            public bool HasCoast { get; }
            public float NearestDistance { get; }
            public bool WasCalculated { get; }

            public MaritimeCoastQuery AsCached() => new(HasCoast, NearestDistance, wasCalculated: false);
        }
    }
}
