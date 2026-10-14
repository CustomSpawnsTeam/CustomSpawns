using System.Collections.Generic;
using System.Linq;

namespace CustomSpawns.AI.Model
{
    /**
     * How a settlement offensive judges strength: what an observer can see around itself, whether a force
     * is strong enough to commit, and which of the candidates it can take on is the nearest.
     */
    public static class OffensiveStrengthRules
    {
        public static bool IsWithinSeeingRange(float distance, float observerSeeingRange)
        {
            return distance <= observerSeeingRange;
        }

        public static float SumVisibleHostileStrength(IEnumerable<RoamingPartyStrength> parties,
            float observerSeeingRange)
        {
            return parties.Where(p => !p.IsSelf && p.IsActive && p.IsHostile &&
                                      IsWithinSeeingRange(p.Distance, observerSeeingRange))
                .Sum(p => p.PartyStrength);
        }

        public static float SumVisibleHostileLordStrength(
            IEnumerable<RoamingPartyStrength> parties, float observerSeeingRange)
        {
            // A lord stack in flight is not a threat near the target: it is running past it, and the
            // force on the other side of this ratio has its own runaways taken out for that very reason.
            return parties.Where(p => !p.IsSelf && p.IsActive && p.IsHostile && p.IsLordParty &&
                                      !p.IsFleeing &&
                                      IsWithinSeeingRange(p.Distance, observerSeeingRange))
                .Sum(p => p.PartyStrength);
        }

        /**
         * What a camp standing at a town has against it: the town itself, and the hostile parties near it
         * that could lift the siege. A merchant train or a peasant cart is near the town constantly and
         * can lift nothing, so neither counts, any more than they count as defenders inside the walls.
         */
        public static float CalculateActiveSiegeDefenderStrength(float defenderStrength,
            IEnumerable<RoamingPartyStrength> parties, float seeingRange)
        {
            return defenderStrength + parties
                .Where(p => !p.IsSelf && p.IsActive && p.IsHostile && !p.IsFleeing &&
                            !p.IsNonCombatant && IsWithinSeeingRange(p.Distance, seeingRange))
                .Sum(p => p.PartyStrength);
        }

        /**
         * What the parties that have turned up at a target can bring to its walls: those within their own
         * sight of it, and nothing else. What a party is worth on paper deliberately does not enter: an
         * army total or an escort still a province away is exactly what this figure exists to leave out.
         */
        public static float CalculateArrivedAttackerStrength(IEnumerable<AttackingPartyStrength> force)
        {
            return force.Where(p => IsWithinSeeingRange(p.DistanceToTarget, p.SeeingRange))
                .Sum(p => p.PartyStrength);
        }

        public static bool MeetsStrengthRatio(float attackerStrength, float defenderStrength,
            float minimumRatio)
        {
            return defenderStrength <= 0f || attackerStrength / defenderStrength >= minimumRatio;
        }

        public static T? ChooseClosestTarget<T>(IEnumerable<RaidTargetCandidate<T>> candidates,
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
