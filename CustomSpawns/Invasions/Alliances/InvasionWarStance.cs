namespace CustomSpawns.Invasions.Alliances
{
    /**
     * What an invasion's war with another faction should be. Policy leaves it to the diplomacy configuration;
     * War keeps it at war for good; Open leaves it to the kingdoms themselves, as any two realms of Calradia;
     * Peace keeps two allied invasions from ever fighting each other.
     */
    public enum InvasionWarStance
    {
        Policy,
        War,
        Open,
        Peace
    }
}
