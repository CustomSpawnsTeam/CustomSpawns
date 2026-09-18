using System;
using System.Collections.Generic;
using System.Linq;
using CustomSpawns.AI.Model;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace CustomSpawns.AI.Offensives
{
    internal interface ISiegeTargetPolicy
    {
        bool IsCandidateAllowed(MobileParty party, SiegeTargetCandidate candidate);
        bool IsExistingTargetAllowed(MobileParty party, Settlement target, bool matchingNativeAssault);

        // The ordering key target selection ranks candidates by, resolved once per selection.
        Func<SiegeTargetCandidate, float> GetTargetDistance(MobileParty party);

        // The parties whose strength a garrison is weighed against: whoever attacks the town together.
        IReadOnlyList<MobileParty> GetAttackingForce(MobileParty party);
    }

    internal sealed class NoRestrictionSiegeTargetPolicy : ISiegeTargetPolicy
    {
        public bool IsCandidateAllowed(MobileParty party, SiegeTargetCandidate candidate) => true;

        public bool IsExistingTargetAllowed(MobileParty party, Settlement target, bool matchingNativeAssault) => true;

        public Func<SiegeTargetCandidate, float> GetTargetDistance(MobileParty party) =>
            candidate => candidate.Distance;

        public IReadOnlyList<MobileParty> GetAttackingForce(MobileParty party) => new[] { party };
    }

    internal sealed class InvasionCoastalSiegeTargetPolicy : ISiegeTargetPolicy
    {
        private readonly IInvasionLandingQuery _landing;
        private readonly IMapPathQuery _mapPaths;

        internal InvasionCoastalSiegeTargetPolicy(IInvasionLandingQuery landing, IMapPathQuery mapPaths)
        {
            _landing = landing;
            _mapPaths = mapPaths;
        }

        public bool IsCandidateAllowed(MobileParty party, SiegeTargetCandidate candidate)
        {
            Func<Settlement, bool>? initialCoastalTargetFilter =
                _landing.GetInitialCoastalTargetFilter(party);
            return IsEligibleCoastalTarget(party, candidate.Target, candidate.TargetId,
                initialCoastalTargetFilter);
        }

        public bool IsExistingTargetAllowed(MobileParty party, Settlement target, bool matchingNativeAssault)
        {
            if (matchingNativeAssault)
            {
                return true;
            }

            Func<Settlement, bool>? initialCoastalTargetFilter =
                _landing.GetInitialCoastalTargetFilter(party);
            return IsEligibleCoastalTarget(party, target, target.StringId, initialCoastalTargetFilter);
        }

        public Func<SiegeTargetCandidate, float> GetTargetDistance(MobileParty party)
        {
            // An invasion lands as one force, so the town it heads for is the nearest for all of its
            // parties together rather than the nearest for whichever party is deciding.
            List<MobileParty> others = GetAttackingForce(party)
                .Where(landingParty => landingParty != party).ToList();
            return candidate => candidate.Distance +
                                others.Sum(other => _mapPaths.GetDistanceToGate(other, candidate.Target));
        }

        // That same force says how strong the invasion is: while it is still looking for a foothold its
        // parties take a town together, so a garrison is weighed against all of them.
        public IReadOnlyList<MobileParty> GetAttackingForce(MobileParty party)
        {
            if (_landing.GetInitialCoastalTargetFilter(party) == null)
            {
                return new[] { party };
            }

            return _landing.GetLandingParties(party);
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

            if (_mapPaths.HasReachableLandGate(party, target))
            {
                return true;
            }

            ReportCoastalRejection(targetId, "no reachable land gate");
            return false;
        }

        private static void ReportCoastalRejection(string settlementId, string reason)
        {
            System.Diagnostics.Debug.WriteLine(
                "CustomSpawns coastal siege target rejected '" + settlementId + "': " + reason);
        }
    }
}
