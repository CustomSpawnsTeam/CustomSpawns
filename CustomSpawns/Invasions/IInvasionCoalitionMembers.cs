using System.Collections.Generic;

namespace CustomSpawns.Invasions
{
    // Who stands in a coalition against an invasion line. Bannerlord access stays behind this boundary.
    public interface IInvasionCoalitionMembers
    {
        /**
         * The kingdoms standing in a coalition, by id, the player's only once the player has joined; none while no
         * line holds enough of the land to raise one.
         */
        IReadOnlyList<string> FindCoalitionMembers();

        // The realms still standing of every line a coalition stands against, by id; none while no coalition holds.
        IReadOnlyList<string> FindInvaders();
    }
}
