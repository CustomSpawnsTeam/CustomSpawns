using System;
using System.Collections.Generic;
using System.Linq;

namespace CustomSpawns.Invasions
{
    /**
     * Which realms of the invasion lines go to war with the rebels of a town. Vanilla sets the rebels at war with the
     * realm the town rose against alone, so rebels could keep a fief of a line, even its capital, from all its other
     * realms.
     */
    public sealed class InvasionRebellions
    {
        private readonly InvasionConquests _invasionConquests;

        public InvasionRebellions(InvasionConquests invasionConquests)
        {
            _invasionConquests = invasionConquests;
        }

        /**
         * Every other realm of the line when the town rose against one of its realms; otherwise each realm at war
         * with the kingdom the town rose against. Any other realm leaves the rebels be.
         */
        public IEnumerable<InvasionConquest> FindRealmsAtWarWithRebels(string? rebelledAgainstClanId,
            Func<InvasionConquest, bool> isAtWarWithRebelledAgainst)
        {
            InvasionConquest? rebelledAgainst = _invasionConquests.Find(rebelledAgainstClanId);

            return rebelledAgainst == null
                ? _invasionConquests.All.Where(isAtWarWithRebelledAgainst)
                : _invasionConquests.FindAllies(rebelledAgainst);
        }
    }
}
