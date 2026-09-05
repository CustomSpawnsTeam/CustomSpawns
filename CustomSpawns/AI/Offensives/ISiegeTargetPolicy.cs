using System;
using CustomSpawns.AI.Model;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace CustomSpawns.AI.Offensives
{
    internal interface ISiegeTargetPolicy
    {
        bool IsCandidateAllowed(MobileParty party, SiegeTargetCandidate candidate);
        bool IsExistingTargetAllowed(MobileParty party, Settlement target, bool matchingNativeAssault);
    }

    internal sealed class NoRestrictionSiegeTargetPolicy : ISiegeTargetPolicy
    {
        public bool IsCandidateAllowed(MobileParty party, SiegeTargetCandidate candidate) => true;

        public bool IsExistingTargetAllowed(MobileParty party, Settlement target, bool matchingNativeAssault) => true;
    }

    internal sealed class InvasionCoastalSiegeTargetPolicy : ISiegeTargetPolicy
    {
        private const float UnboundedLandPathDistance = float.MaxValue;
        private readonly Func<MobileParty, Func<Settlement, bool>?> _getInitialCoastalTargetFilter;
        private readonly Func<MobileParty, Settlement, bool> _hasReachableLandGate;

        internal InvasionCoastalSiegeTargetPolicy(
            Func<MobileParty, Func<Settlement, bool>?> getInitialCoastalTargetFilter,
            Func<MobileParty, Settlement, bool>? hasReachableLandGate = null)
        {
            _getInitialCoastalTargetFilter = getInitialCoastalTargetFilter;
            _hasReachableLandGate = hasReachableLandGate ?? HasReachableLandGate;
        }

        public bool IsCandidateAllowed(MobileParty party, SiegeTargetCandidate candidate)
        {
            Func<Settlement, bool>? initialCoastalTargetFilter = _getInitialCoastalTargetFilter(party);
            return IsEligibleCoastalTarget(party, candidate.Target, candidate.TargetId,
                initialCoastalTargetFilter);
        }

        public bool IsExistingTargetAllowed(MobileParty party, Settlement target, bool matchingNativeAssault)
        {
            if (matchingNativeAssault)
            {
                return true;
            }

            Func<Settlement, bool>? initialCoastalTargetFilter = _getInitialCoastalTargetFilter(party);
            return IsEligibleCoastalTarget(party, target, target.StringId, initialCoastalTargetFilter);
        }

        private bool IsEligibleCoastalTarget(MobileParty party, Settlement target,
            string targetId, Func<Settlement, bool>? initialCoastalTargetFilter)
        {
            if (initialCoastalTargetFilter == null)
            {
                return true;
            }

            if (!initialCoastalTargetFilter(target))
            {
                ReportCoastalRejection(targetId, "not an eligible coastal town");
                return false;
            }

            if (_hasReachableLandGate(party, target))
            {
                return true;
            }

            ReportCoastalRejection(targetId, "no reachable land gate");
            return false;
        }

        private static bool HasReachableLandGate(MobileParty party, Settlement settlement)
        {
            if (party.IsCurrentlyAtSea)
            {
                ReportCoastalRejection(settlement.StringId, "party is at sea");
                return false;
            }

            PathFaceRecord fromFace = party.CurrentNavigationFace;
            PathFaceRecord toFace = settlement.GatePosition.Face;
            if (!settlement.GatePosition.IsOnLand || fromFace.FaceIndex < 0 || toFace.FaceIndex < 0)
            {
                ReportCoastalRejection(settlement.StringId, "missing usable land navigation face");
                return false;
            }

            int[] invalidTerrainTypes = Campaign.Current.Models.PartyNavigationModel
                .GetInvalidTerrainTypesForNavigationType(party.NavigationCapability);
            bool hasPath = Campaign.Current.MapSceneWrapper.GetPathDistanceBetweenAIFaces(fromFace, toFace,
                party.Position.ToVec2(), settlement.GatePosition.ToVec2(), 0.3f, UnboundedLandPathDistance,
                out float pathDistance, invalidTerrainTypes,
                Campaign.Current.Models.MapDistanceModel.RegionSwitchCostFromLandToSea,
                Campaign.Current.Models.MapDistanceModel.RegionSwitchCostFromSeaToLand);
            if (hasPath && !float.IsNaN(pathDistance) && !float.IsInfinity(pathDistance) &&
                pathDistance >= 0f)
            {
                return true;
            }

            ReportCoastalRejection(settlement.StringId, "no finite land path");
            return false;
        }

        private static void ReportCoastalRejection(string settlementId, string reason)
        {
            System.Diagnostics.Debug.WriteLine(
                "CustomSpawns coastal siege target rejected '" + settlementId + "': " + reason);
        }
    }
}
