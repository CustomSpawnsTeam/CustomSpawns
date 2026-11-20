using CustomSpawns.Invasions.CapitalObjectives;

namespace CustomSpawns.Invasions.Lines
{
    /**
     * When a beaten invasion's realm falls. Vanilla is kept from disbanding the kingdom of an invasion still
     * fighting, so its end is decided here: the realm falls once it holds no fief, not even its capital, and the
     * invasion has been too weak to retake one -- its capital objective dormant -- for two weeks since its last
     * attempt on the capital or its last lost fief. An attempt wakes the objective, and a lost fief restarts the
     * count, so either one buys the realm another two weeks.
     */
    public sealed class InvasionRealmCollapse
    {
        private const double WeakDaysBeforeCollapse = 14;

        public bool Observe(Invasion invasion, double nowDays, int fiefs, bool holdsCapital)
        {
            bool isWeak = invasion.CapitalObjective?.Phase == InvasionCapitalObjectivePhase.Dormant;
            bool lostAFief = fiefs < (invasion.LastFiefCount ?? fiefs);

            if (!isWeak)
            {
                invasion.WeakSinceDays = null;
            }
            else if (invasion.WeakSinceDays == null || lostAFief)
            {
                invasion.WeakSinceDays = nowDays;
            }

            invasion.LastFiefCount = fiefs;

            return isWeak && fiefs == 0 && !holdsCapital &&
                   nowDays - invasion.WeakSinceDays >= WeakDaysBeforeCollapse;
        }
    }
}
