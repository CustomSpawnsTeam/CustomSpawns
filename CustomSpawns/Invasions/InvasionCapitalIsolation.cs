using System.Collections.Generic;
using System.Linq;

namespace CustomSpawns.Invasions
{
    // When a capital of a line has been cut off from the rest of it, so the coalition turns on it.
    public sealed class InvasionCapitalIsolation
    {
        /**
         * Whether the line holds fewer than half of the ring of fiefs nearest its capital, given whether it holds
         * each fief, nearest first: a capital whose neighbours are mostly lost is cut off, though a fief of the line
         * still stands among them. A ring of zero never cuts a capital off.
         */
        public bool IsIsolated(IEnumerable<bool> nearestFiefsHeldByLine, int ringSize) =>
            ringSize > 0 && nearestFiefsHeldByLine.Take(ringSize).Count(isHeld => isHeld) * 2 < ringSize;
    }
}
