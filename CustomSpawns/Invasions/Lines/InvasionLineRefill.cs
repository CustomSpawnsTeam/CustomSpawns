namespace CustomSpawns.Invasions.Lines
{
    /**
     * Whether a conquering invasion line lands a new realm. It owes one for every kingdom outside the invasions
     * that has fallen, on top of its first. Every invasion of the line still fighting counts, a copy that has
     * landed but holds no town yet included, so none is sent after one still on its way, and one beaten at its
     * landing is sent again. A line with no realm left standing launches nothing, and a line already fielding
     * its most realms waits for one to fall. The count is checked every thirty days, and at most one realm lands
     * per check.
     */
    public sealed class InvasionLineRefill
    {
        private const int CheckIntervalInDays = 30;

        public bool IsCheckDay(int day) => day % CheckIntervalInDays == 0;

        // Zero maxKingdoms sets no limit.
        public bool ShouldLaunch(int fallenKingdoms, int fightingInvasions, bool hasStandingRealm, int maxKingdoms) =>
            hasStandingRealm && fightingInvasions < fallenKingdoms + 1 &&
            (maxKingdoms <= 0 || fightingInvasions < maxKingdoms);
    }
}
