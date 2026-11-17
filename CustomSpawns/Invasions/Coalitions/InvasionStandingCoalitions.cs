using System;
using System.Collections.Generic;
using System.Linq;

namespace CustomSpawns.Invasions.Coalitions
{
    /**
     * The invasion lines a coalition stands against, by the spawn template of each line's first invasion. A coalition
     * is raised once its line holds the configured share of the land, and stands until the line is destroyed or
     * beaten: losing fiefs back below the share does not break it up.
     */
    public sealed class InvasionStandingCoalitions
    {
        private readonly HashSet<string> _lineIds = new(StringComparer.Ordinal);

        // Raises the coalition against the line when it holds the share; whether one stands against it either way.
        public bool StandsAgainst(string lineId, bool holdsCoalitionShare)
        {
            if (holdsCoalitionShare)
            {
                _lineIds.Add(lineId);
            }

            return _lineIds.Contains(lineId);
        }

        public void DisbandAllBut(IEnumerable<string> standingLineIds) =>
            _lineIds.IntersectWith(standingLineIds.ToList());

        public List<string> Export() => _lineIds.OrderBy(lineId => lineId, StringComparer.Ordinal).ToList();

        public void Restore(List<string>? saved)
        {
            _lineIds.Clear();
            _lineIds.UnionWith((saved ?? new List<string>()).Where(lineId => !string.IsNullOrEmpty(lineId)));
        }
    }
}
