using System;
using System.Collections.Generic;
using System.Linq;
using CustomSpawns.AI.Model;
using CustomSpawns.AI.Offensives;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace CustomSpawns.Invasions.Adapters
{
    /**
     * Where an invasion is allowed to land, and which parties land together. Siege targeting restricts and
     * ranks settlements through this without knowing what an invasion is.
     */
    internal interface IInvasionLandingQuery
    {
        // The towns a party may take while its invasion holds no coastal town; null once it may march inland.
        Func<Settlement, bool>? GetInitialCoastalTargetFilter(MobileParty party);

        // The parties that land together, so one town can be the nearest for the invasion as a whole.
        IReadOnlyList<MobileParty> GetLandingParties(MobileParty party);
    }

    internal sealed class InvasionCoastalSiegeTargetPolicy : ISiegeTargetPolicy
    {
        // How far apart two parties of one landing may be and still count as marching together. A landing
        // is scattered over 10 map units around its port (MobilePartySpawnFactory.SpawnParty), so two of
        // its parties can start 20 apart and spread further as they set off, which sets the floor. There
        // is no ceiling that also separates two neighbouring ports: on the DADG map the closest pair of
        // port towns is 21.4 apart, inside that floor. So this tells a stranded remnant from the force at
        // hand only once the two are roughly a town apart, which is the case the playtest reported.
        private const float GatheringRadius = 30f;

        private readonly IInvasionLandingQuery _invasionLandingQuery;
        private readonly IMapPathQuery _mapPathQuery;

        internal InvasionCoastalSiegeTargetPolicy(IInvasionLandingQuery landing, IMapPathQuery mapPaths)
        {
            _invasionLandingQuery = landing;
            _mapPathQuery = mapPaths;
        }

        public bool IsCandidateAllowed(MobileParty party, SiegeTargetCandidate candidate)
        {
            Func<Settlement, bool>? initialCoastalTargetFilter =
                _invasionLandingQuery.GetInitialCoastalTargetFilter(party);
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
                _invasionLandingQuery.GetInitialCoastalTargetFilter(party);
            return IsEligibleCoastalTarget(party, target, target.StringId, initialCoastalTargetFilter);
        }

        public Func<SiegeTargetCandidate, float> GetTargetDistance(MobileParty party)
        {
            // An invasion lands as one force, so the town it heads for is the nearest for all of its
            // parties together rather than the nearest for whichever party is deciding.
            List<MobileParty> others = GetLandingForce(party)
                .Where(landingParty => landingParty != party).ToList();
            return candidate => candidate.Distance +
                                others.Sum(other => _mapPathQuery.GetDistanceToGate(other, candidate.Target));
        }

        // That same force says how strong the invasion is, but only the part of it that is actually here:
        // while the landing is still looking for a foothold its gathered parties take a town together, so
        // a garrison is weighed against them and not against a remnant stranded elsewhere on the map.
        public IReadOnlyList<MobileParty> GetAttackingForce(MobileParty party)
        {
            List<MobileParty> force = new List<MobileParty> { party };

            force.AddRange(GetLandingForce(party)
                .Where(landingParty => landingParty != party && IsGatheredWith(party, landingParty)));

            return force;
        }

        private IReadOnlyList<MobileParty> GetLandingForce(MobileParty party)
        {
            if (_invasionLandingQuery.GetInitialCoastalTargetFilter(party) == null)
            {
                return new[] { party };
            }

            return _invasionLandingQuery.GetLandingParties(party);
        }

        private static bool IsGatheredWith(MobileParty party, MobileParty landingParty)
        {
            return landingParty.Position.Distance(party.Position) <= GatheringRadius;
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

            if (_mapPathQuery.HasReachableLandGate(party, target))
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
