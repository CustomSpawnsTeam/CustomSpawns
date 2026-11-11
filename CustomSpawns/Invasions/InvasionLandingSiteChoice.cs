using System.Collections.Generic;
using System.Linq;

namespace CustomSpawns.Invasions
{
    // Where a follower launched for its line comes ashore.
    public sealed class InvasionLandingSiteChoice
    {
        /**
         * Where a follower lands: the coastal town lying nearest the line's first realm. Its kingdom becomes the
         * target every realm of the line converges on, so the first realm fights next door instead of being drawn
         * to the far side of the map. Null when no coastal town is left to land in.
         */
        public InvasionLandingSite? FindLandingSite(IEnumerable<InvasionLandingSite> sites) =>
            sites.OrderBy(site => site.DistanceFromFirstRealm)
                .Select(site => (InvasionLandingSite?)site)
                .FirstOrDefault();
    }
}
