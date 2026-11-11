namespace CustomSpawns.Invasions
{
    // Whether an invasion raises an army out of its capital's garrison today, and with how many men.
    public sealed class InvasionGarrisonArmyMuster
    {
        public const string CapitalBesieged = "capital_besieged";
        public const string GarrisonAtMinimum = "garrison_at_minimum";
        public const string NoTarget = "no_target";
        // The campaign could not create the army's one-time lord.
        public const string NoLord = "no_lord";
        private readonly InvasionGarrisonArmySizing _invasionGarrisonArmySizing;

        public InvasionGarrisonArmyMuster(InvasionGarrisonArmySizing invasionGarrisonArmySizing)
        {
            _invasionGarrisonArmySizing = invasionGarrisonArmySizing;
        }

        /**
         * One army at a time, and none before the interval since the last was raised has passed, measured from that
         * raise rather than from the day the army ended. The first is due at once.
         */
        public bool IsDue(InvasionGarrisonArmyRules rules, double? daysSinceLastRaise, bool isArmyOut) =>
            !isArmyOut && (daysSinceLastRaise == null || daysSinceLastRaise.Value >= rules.IntervalInDays);

        /**
         * A due army is raised with the men the sizing allows, or skipped for the first reason found. A besieged
         * capital needs its garrison more than any siege of the invasion's does.
         */
        public InvasionGarrisonArmyMusterDecision Decide(InvasionGarrisonArmyRules rules,
            InvasionGarrisonArmyFacts facts)
        {
            if (facts.IsCapitalBesieged)
            {
                return new InvasionGarrisonArmyMusterDecision(0, CapitalBesieged);
            }

            int men = _invasionGarrisonArmySizing.FindMen(rules, facts.GarrisonMen);

            if (men <= 0)
            {
                return new InvasionGarrisonArmyMusterDecision(0, GarrisonAtMinimum);
            }

            return facts.HasTarget
                ? new InvasionGarrisonArmyMusterDecision(men, null)
                : new InvasionGarrisonArmyMusterDecision(0, NoTarget);
        }
    }

    public readonly struct InvasionGarrisonArmyMusterDecision
    {
        public InvasionGarrisonArmyMusterDecision(int men, string? skipReason)
        {
            Men = men;
            SkipReason = skipReason;
        }

        // How many men leave the garrison; zero when the army is skipped.
        public int Men { get; }

        // Why no army is raised; null when one is.
        public string? SkipReason { get; }
    }
}
