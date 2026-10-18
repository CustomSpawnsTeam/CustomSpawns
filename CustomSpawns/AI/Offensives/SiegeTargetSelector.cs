using System;
using System.Collections.Generic;
using System.Linq;

namespace CustomSpawns.AI.Offensives
{
    public sealed class SiegeTargetSelector
    {
        private readonly ForceRatioRules _forceRatioRules;

        public SiegeTargetSelector(ForceRatioRules forceRatioRules)
        {
            _forceRatioRules = forceRatioRules ?? throw new ArgumentNullException(nameof(forceRatioRules));
        }

        public SiegeTargetSelection? FindClosest(IReadOnlyList<SiegeTargetFacts> candidates,
            SiegeTargetCriteria criteria)
        {
            SiegeTargetFacts? closest = candidates
                .Where(candidate => IsEligible(candidate, criteria))
                .OrderBy(candidate => candidate.Distance)
                .Select(candidate => (SiegeTargetFacts?)candidate)
                .FirstOrDefault();

            return closest.HasValue
                ? new SiegeTargetSelection(closest.Value.SettlementId)
                : (SiegeTargetSelection?)null;
        }

        private bool IsEligible(SiegeTargetFacts candidate, SiegeTargetCriteria criteria)
        {
            if (!candidate.IsActive || !candidate.IsHostile ||
                !criteria.AllowedKinds.Contains(candidate.Kind) ||
                candidate.SettlementId == criteria.ExcludedSettlementId ||
                !candidate.HasCompatibleOrJoinableSiege ||
                !candidate.MatchesStrategicRequirement || !candidate.IsReachable)
            {
                return false;
            }

            if (criteria.Mode == SiegeTargetSelectionMode.GatherBeforeAssault)
            {
                return true;
            }

            return _forceRatioRules.Meets(criteria.AttackerStrength, candidate.DefenderStrength,
                       criteria.MinimumTargetRatio) &&
                   _forceRatioRules.Meets(criteria.AttackerStrength, candidate.NearbyHostileStrength,
                       criteria.MinimumRoamingRatio);
        }
    }
}
