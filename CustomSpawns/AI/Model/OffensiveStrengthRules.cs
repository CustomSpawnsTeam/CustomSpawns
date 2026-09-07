using System.Collections.Generic;
using System.Linq;

namespace CustomSpawns.AI.Model
{
    /**
     * How a settlement offensive judges strength: what an observer can see around itself, whether a force
     * is strong enough to commit, and which of the candidates it can take on is the nearest.
     */
    internal static class OffensiveStrengthRules
    {
        internal static bool IsWithinSeeingRange(float distance, float observerSeeingRange)
        {
            return distance <= observerSeeingRange;
        }

        internal static float SumVisibleHostileStrength(IEnumerable<RoamingPartyStrength> parties,
            float observerSeeingRange)
        {
            return parties.Where(p => !p.IsSelf && p.IsActive && p.IsHostile &&
                                      IsWithinSeeingRange(p.Distance, observerSeeingRange))
                .GroupBy(p => p.ArmyIdentity)
                .Sum(army => army.Sum(p => p.PartyStrength));
        }

        internal static float SumVisibleHostileLordStrength(
            IEnumerable<RoamingPartyStrength> parties, float observerSeeingRange)
        {
            return parties.Where(p => !p.IsSelf && p.IsActive && p.IsHostile && p.IsLordParty &&
                                      IsWithinSeeingRange(p.Distance, observerSeeingRange))
                .GroupBy(p => p.ArmyIdentity)
                .Sum(army => army.Sum(p => p.PartyStrength));
        }

        internal static float CalculateActiveSiegeDefenderStrength(float garrisonStrength,
            IEnumerable<RoamingPartyStrength> parties, float observerSeeingRange)
        {
            return garrisonStrength + parties
                .Where(p => !p.IsSelf && p.IsActive && p.IsHostile && !p.IsFleeing &&
                            IsWithinSeeingRange(p.Distance, observerSeeingRange))
                .Sum(p => p.PartyStrength);
        }

        internal static bool MeetsStrengthRatio(float attackerStrength, float defenderStrength,
            float minimumRatio)
        {
            return defenderStrength <= 0f || attackerStrength / defenderStrength >= minimumRatio;
        }

        internal static T? ChooseClosestTarget<T>(IEnumerable<RaidTargetCandidate<T>> candidates,
            float attackerStrength, float minimumRatio) where T : class
        {
            return candidates.Where(c => c.IsValid &&
                                         MeetsStrengthRatio(attackerStrength, c.DefenderStrength, minimumRatio))
                .OrderBy(c => c.Distance)
                .Select(c => c.Target)
                .FirstOrDefault();
        }
    }
}
