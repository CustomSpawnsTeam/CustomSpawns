using System;
using System.Collections.Generic;
using NLog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace CustomSpawns.Invasions.Adapters
{
    // Bannerlord-specific map-face enumeration and lookup for the pure coastal-town policy.
    internal sealed class InvasionCoastalTownResolver : IInvasionCoastalMapFaceQuery
    {
        private const string CoastalDiagnosticTownId = "town_V7";

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
            var coastal = settlement.IsTown && (settlement.HasPort || maritimeCoast?.HasCoast == true);
            if (isDiagnosticTown && maritimeCoast?.WasCalculated == true)
                Logger.Info("Invasion coastal classification: town=" + CoastalDiagnosticTownId +
                            " coastal=" + coastal + " has_port=" + settlement.HasPort +
                            " nearest_maritime_face_distance=" + maritimeCoast.Value.NearestDistance +
                            " radius=" + InvasionCoastalTownClassification.MaritimeScanRadius +
                            " source=native_or_map");
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
            EnsureNavigationMeshFaceIndex();
            if (_nearbyMaritimeCoastBySettlementId.TryGetValue(settlement.StringId, out MaritimeCoastQuery cached))
                return cached.AsCached();

            var settlementPosition = ToCoastalPoint(settlement.Position.ToVec2());
            var gatePosition = ToCoastalPoint(settlement.GatePosition.ToVec2());
            bool hasCoast = InvasionCoastalTownClassification.IsSettlementAdjacentToSea(
                _navigationMeshFaces, this, settlementPosition, gatePosition, out float nearestDistanceSquared);
            var result = new MaritimeCoastQuery(hasCoast, float.IsPositiveInfinity(nearestDistanceSquared)
                ? float.PositiveInfinity
                : (float)Math.Sqrt(nearestDistanceSquared), wasCalculated: true);
            _nearbyMaritimeCoastBySettlementId[settlement.StringId] = result;
            return result;
        }

        private void EnsureNavigationMeshFaceIndex()
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

        InvasionCoastalMapFace? IInvasionCoastalMapFaceQuery.FindFaceAt(InvasionCoastalPoint position)
        {
            CampaignVec2 samplePosition = new CampaignVec2(new Vec2(position.X, position.Y), false);
            PathFaceRecord sampledFace = Campaign.Current.MapSceneWrapper.GetFaceIndex(in samplePosition);
            if (!sampledFace.IsValid())
                return null;

            InvasionCoastalFaceTerrain terrain = ToCoastalFaceTerrain(
                Campaign.Current.MapSceneWrapper.GetFaceTerrainType(sampledFace));
            return new InvasionCoastalMapFace(terrain, position, sampledFace.FaceIndex);
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
