using System.Collections.Generic;
using System.Linq;

namespace CustomSpawns.Invasions
{
    // Which bordering kingdom a conquering invasion turns on next, once its target falls or as borders move.
    public sealed class InvasionTargetChoice
    {
        // A kingdom already at war with the invasion, or one an ally conquers, is that much better a prey.
        private const float AtWarAppeal = 2f;
        private const float AlliedTargetAppeal = 2f;

        // How much better a prey another neighbour must be before the invasion drops its target for it, so it
        // does not turn back and forth between two close ones.
        private const float LeadToTurnOnAnother = 1.5f;

        /**
         * Once its target falls, an invasion turns on the best prey on its borders: the weakest kingdom for the
         * fortifications it has within reach, as an invader that picks a fight with a stronger neighbour is beaten
         * back before it can grow, and better still one it already fights or one an ally conquers, so the line
         * finishes kingdoms together. With no kingdom on its borders it has nothing to turn on.
         */
        public string? FindNextTarget(IEnumerable<InvasionNeighbour> neighbours,
            IEnumerable<string>? alliedTargetKingdomIds = null)
        {
            HashSet<string> alliedTargets = new(alliedTargetKingdomIds ?? Enumerable.Empty<string>());

            return neighbours.OrderByDescending(neighbour => FindAppeal(neighbour, alliedTargets))
                .Select(neighbour => neighbour.KingdomId)
                .FirstOrDefault();
        }

        /**
         * Every so often, a conquering invasion weighs its target against the other kingdoms on its borders. Wars
         * move borders: the target may have withdrawn behind fortifications out of reach while another kingdom took
         * fiefs next to the invasion's. It turns on that kingdom when it is a much better prey; a target no longer
         * on its borders is no prey at all. Null when it keeps its target.
         */
        public string? Reconsider(InvasionConquest conquest, InvasionConquestRules rules,
            IEnumerable<InvasionNeighbour> neighbours, IEnumerable<string> alliedTargetKingdomIds, double today)
        {
            if (rules.ReconsiderTargetIntervalInDays <= 0f ||
                today - conquest.LastReconsideredDay < rules.ReconsiderTargetIntervalInDays)
            {
                return null;
            }

            conquest.Reconsider(today);
            HashSet<string> alliedTargets = new(alliedTargetKingdomIds);
            List<InvasionNeighbour> candidates = neighbours.ToList();
            float targetAppeal = candidates.Where(neighbour => neighbour.KingdomId == conquest.TargetKingdomId)
                .Select(neighbour => FindAppeal(neighbour, alliedTargets)).DefaultIfEmpty(0f).Max();
            InvasionNeighbour? best = candidates.OrderByDescending(neighbour => FindAppeal(neighbour, alliedTargets))
                .Select(neighbour => (InvasionNeighbour?)neighbour).FirstOrDefault();

            return best == null || best.Value.KingdomId == conquest.TargetKingdomId ||
                   FindAppeal(best.Value, alliedTargets) <= targetAppeal * LeadToTurnOnAnother
                ? null
                : best.Value.KingdomId;
        }

        private static float FindAppeal(InvasionNeighbour neighbour, HashSet<string> alliedTargets) =>
            (1f + neighbour.BorderFortifications) / System.Math.Max(neighbour.Strength, 1f) *
            (neighbour.IsAtWar ? AtWarAppeal : 1f) *
            (alliedTargets.Contains(neighbour.KingdomId) ? AlliedTargetAppeal : 1f);
    }
}
