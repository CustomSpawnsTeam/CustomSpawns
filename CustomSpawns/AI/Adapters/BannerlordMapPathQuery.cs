using CustomSpawns.AI.Offensives;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace CustomSpawns.AI.Adapters
{
    internal sealed class BannerlordMapPathQuery : IMapPathQuery
    {
        private const float UnboundedLandPathDistance = float.MaxValue;

        public bool HasReachableLandGate(MobileParty party, Settlement target)
        {
            if (party.IsCurrentlyAtSea)
            {
                return false;
            }

            PathFaceRecord fromFace = party.CurrentNavigationFace;
            PathFaceRecord toFace = target.GatePosition.Face;
            if (!target.GatePosition.IsOnLand || fromFace.FaceIndex < 0 || toFace.FaceIndex < 0)
            {
                return false;
            }
            int[] invalidTerrainTypes = Campaign.Current.Models.PartyNavigationModel
                .GetInvalidTerrainTypesForNavigationType(party.NavigationCapability);
            bool hasPath = Campaign.Current.MapSceneWrapper.GetPathDistanceBetweenAIFaces(fromFace, toFace,
                party.Position.ToVec2(), target.GatePosition.ToVec2(), 0.3f, UnboundedLandPathDistance,
                out float pathDistance, invalidTerrainTypes,
                Campaign.Current.Models.MapDistanceModel.RegionSwitchCostFromLandToSea,
                Campaign.Current.Models.MapDistanceModel.RegionSwitchCostFromSeaToLand);
            return hasPath && !float.IsNaN(pathDistance) && !float.IsInfinity(pathDistance) &&
                pathDistance >= 0f;
        }

        public float GetDistanceToGate(MobileParty party, Settlement settlement)
        {
            return party.Position.Distance(settlement.GatePosition);
        }
    }
}
