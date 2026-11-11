using System.Collections.Generic;
using System.Linq;

namespace CustomSpawns.Invasions
{
    // Where a follower launched as a kingdom falls comes ashore.
    public sealed class InvasionLandingSiteChoice
    {
        /**
         * Where a follower lands: the coastal town lying farthest from every fief its allies hold, so it opens a
         * war in another part of the map instead of fighting over theirs. Its kingdom becomes the follower's
         * target, so a kingdom an ally already conquers is passed over while any other has a coast. Null when no
         * coastal town is left to land in.
         */
        public InvasionLandingSite? FindLandingSite(IEnumerable<InvasionLandingSite> sites,
            IEnumerable<string> alliedTargetKingdomIds)
        {
            HashSet<string> alliedTargets = new(alliedTargetKingdomIds);

            return sites.OrderBy(site => alliedTargets.Contains(site.KingdomId))
                .ThenByDescending(site => site.DistanceFromAllies)
                .Select(site => (InvasionLandingSite?)site)
                .FirstOrDefault();
        }
    }
}
