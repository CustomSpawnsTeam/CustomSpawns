namespace CustomSpawns.Invasions
{
    // The kingdom an invasion is conquering, as its strength budget sees it. Bannerlord access stays behind
    // this boundary.
    public interface IInvasionConquestTargets
    {
        /**
         * What the kingdom the invasion conquers fields, its war parties and garrisons together, measured as
         * the invasion's own strength is. Zero once that kingdom has fallen or while the invasion conquers none.
         */
        double MeasureConquestTargetStrength(Invasion invasion);

        // The kingdom the invasion is conquering; null once it has fallen or while the invasion conquers none.
        string? FindConquestTargetKingdomId(Invasion invasion);
    }
}
