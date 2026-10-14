using System;
using System.Collections.Generic;
using NLog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace CustomSpawns.Invasions.Adapters
{
    // Bannerlord-specific navmesh access for the pure coastal-town policy.
    public sealed class InvasionCoastalTownResolver : IInvasionCoastalMapFaceQuery
    {
        private const string CoastalDiagnosticTownId = "town_V7";
        private const int AdjacencyProbeCount = 360;
        private const float AdjacencyProbeLength = 100f;
        private const float BoundaryCrossingDistance = 0.05f;

        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly Dictionary<string, bool> _coastalBySettlementId = new();
        private readonly Dictionary<int, IReadOnlyCollection<InvasionCoastalMapFace>> _adjacentFacesByFaceIndex = new();
        private object? _mapScene;

        public bool IsCoastalTown(Settlement? settlement)
        {
            if (settlement == null || !settlement.IsTown)
                return false;

            EnsureMapSceneCache();
            if (settlement.HasPort)
                return true;

            if (!_coastalBySettlementId.TryGetValue(settlement.StringId, out bool coastal))
            {
                PathFaceRecord currentFace = settlement.CurrentNavigationFace;
                coastal = currentFace.IsValid() && InvasionCoastalTownClassification.IsSettlementAdjacentToSea(
                    ToCoastalMapFace(currentFace), this);
                _coastalBySettlementId[settlement.StringId] = coastal;

                if (settlement.StringId == CoastalDiagnosticTownId)
                    Logger.Info("Invasion coastal classification: town=" + CoastalDiagnosticTownId +
                                " coastal=" + coastal + " has_port=" + settlement.HasPort +
                                " source=current_navigation_face");
            }

            return coastal;
        }

        public void ClearCache()
        {
            _mapScene = null;
            _coastalBySettlementId.Clear();
            _adjacentFacesByFaceIndex.Clear();
        }

        IReadOnlyCollection<InvasionCoastalMapFace> IInvasionCoastalMapFaceQuery.GetAdjacentFaces(
            InvasionCoastalMapFace face)
        {
            if (_adjacentFacesByFaceIndex.TryGetValue(face.FaceIndex,
                    out IReadOnlyCollection<InvasionCoastalMapFace>? cached))
                return cached;

            var adjacentFaces = new Dictionary<int, InvasionCoastalMapFace>();
            PathFaceRecord sourceFace = Campaign.Current.MapSceneWrapper.GetFaceAtIndex(face.FaceIndex);
            Vec2 sourceCenter = Campaign.Current.MapSceneWrapper.GetNavigationMeshCenterPosition(sourceFace);

            for (int probeIndex = 0; probeIndex < AdjacencyProbeCount; probeIndex++)
            {
                double angle = probeIndex * Math.PI * 2d / AdjacencyProbeCount;
                var direction = new Vec2((float)Math.Cos(angle), (float)Math.Sin(angle));
                Vec2 boundary = Campaign.Current.MapSceneWrapper.GetLastPositionOnNavMeshFaceForPointAndDirection(
                    sourceFace, sourceCenter, direction * AdjacencyProbeLength);
                Vec2 positionAcrossBoundary = boundary + direction * BoundaryCrossingDistance;

                AddFaceAt(positionAcrossBoundary, isOnLand: true, face.FaceIndex, adjacentFaces);
                AddFaceAt(positionAcrossBoundary, isOnLand: false, face.FaceIndex, adjacentFaces);
            }

            IReadOnlyCollection<InvasionCoastalMapFace> result =
                new List<InvasionCoastalMapFace>(adjacentFaces.Values);
            _adjacentFacesByFaceIndex[face.FaceIndex] = result;
            return result;
        }

        private static void AddFaceAt(Vec2 position, bool isOnLand, int sourceFaceIndex,
            IDictionary<int, InvasionCoastalMapFace> adjacentFaces)
        {
            CampaignVec2 mapPosition = new CampaignVec2(position, isOnLand);
            PathFaceRecord adjacentFace = Campaign.Current.MapSceneWrapper.GetFaceIndex(in mapPosition);
            if (!adjacentFace.IsValid() || adjacentFace.FaceIndex == sourceFaceIndex ||
                adjacentFaces.ContainsKey(adjacentFace.FaceIndex))
                return;

            adjacentFaces.Add(adjacentFace.FaceIndex, ToCoastalMapFace(adjacentFace));
        }

        private static InvasionCoastalMapFace ToCoastalMapFace(PathFaceRecord face)
        {
            TerrainType terrain = Campaign.Current.MapSceneWrapper.GetFaceTerrainType(face);
            return new InvasionCoastalMapFace(ToCoastalFaceTerrain(terrain), face.FaceIndex);
        }

        private void EnsureMapSceneCache()
        {
            object mapScene = Campaign.Current.MapSceneWrapper;
            if (ReferenceEquals(_mapScene, mapScene))
                return;

            _mapScene = mapScene;
            _coastalBySettlementId.Clear();
            _adjacentFacesByFaceIndex.Clear();
        }

        private static InvasionCoastalFaceTerrain ToCoastalFaceTerrain(TerrainType terrain)
        {
            return terrain == TerrainType.RuralArea ? InvasionCoastalFaceTerrain.RuralArea :
                terrain == TerrainType.Water ? InvasionCoastalFaceTerrain.Water :
                terrain == TerrainType.CoastalSea ? InvasionCoastalFaceTerrain.CoastalSea :
                terrain == TerrainType.OpenSea ? InvasionCoastalFaceTerrain.OpenSea :
                terrain == TerrainType.Lake ? InvasionCoastalFaceTerrain.Lake :
                terrain == TerrainType.River ? InvasionCoastalFaceTerrain.River :
                InvasionCoastalFaceTerrain.Land;
        }
    }
}
