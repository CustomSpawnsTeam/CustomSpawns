namespace CustomSpawns.Invasions
{
    // What a due convoy carries and when the next one is expected.
    internal readonly struct InvasionFoodConvoyDecision
    {
        internal InvasionFoodConvoyDecision(bool unloads, double? nextConvoyDays)
        {
            Unloads = unloads;
            NextConvoyDays = nextConvoyDays;
        }

        internal bool Unloads { get; }
        internal double? NextConvoyDays { get; }
    }

    /**
     * When the invasion's next food convoy reaches its capital. A convoy is food unloaded into that town's
     * own market, so the town lives off it exactly as it lives off anything a trader brings in: nothing
     * here writes a food balance, and no other settlement the invasion holds is supplied at all.
     */
    internal static class InvasionFoodConvoyRules
    {
        internal static bool IsConfigured(int foodPerConvoy, double intervalInDays) =>
            foodPerConvoy > 0 && intervalInDays > 0;

        /**
         * A capital that has just been taken winds the clock without being supplied, so the first convoy
         * arrives an interval after the landing rather than on the day of it.
         *
         * While the capital is lost or besieged the clock holds where it is instead of running on: a convoy
         * cannot be unloaded into a town the invasion does not hold or cannot reach, and a siege is already
         * starving that town by vanilla's own rules. Whatever was due is unloaded once the way in is open
         * again, rather than being forfeited to a siege the invasion had no hand in.
         */
        internal static InvasionFoodConvoyDecision Decide(double? nextConvoyDays, double nowDays,
            double intervalInDays, bool capitalReachable)
        {
            if (!capitalReachable)
            {
                return new InvasionFoodConvoyDecision(false, nextConvoyDays);
            }
            if (nextConvoyDays == null)
            {
                return new InvasionFoodConvoyDecision(false, nowDays + intervalInDays);
            }
            return nextConvoyDays <= nowDays
                ? new InvasionFoodConvoyDecision(true, nowDays + intervalInDays)
                : new InvasionFoodConvoyDecision(false, nextConvoyDays);
        }
    }
}
