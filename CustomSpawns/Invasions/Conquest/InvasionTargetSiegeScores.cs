namespace CustomSpawns.Invasions.Conquest
{
    // How much a lord of a conquering invasion wants a siege of the kingdom it conquers, against vanilla's wish.
    public sealed class InvasionTargetSiegeScores
    {
        // The lowest distance score vanilla still weighs a siege at, given to a fief it does not weigh at all.
        private const float UnborderedDistanceScore = 0.1f;
        private const float LeadOverOtherSieges = 1.1f;

        /**
         * A lord's wish to besiege a fief of the kingdom being conquered, from vanilla's. Any such siege vanilla
         * judges can be won is wanted more than every other siege, so the invasion finishes that kingdom before it
         * spreads into the others it fights; among them, the preference multiplies vanilla's wish.
         */
        public float FindTargetSiegeScore(InvasionConquestRules rules, float score, float bestOtherSiegeScore) =>
            rules.TargetSiegePreference <= 1f || score <= 0f
                ? score
                : System.Math.Max(score * rules.TargetSiegePreference,
                    bestOtherSiegeScore * LeadOverOtherSieges);

        /**
         * A lord's wish to besiege a fief of the kingdom being conquered that vanilla does not weigh, because the
         * invasion holds no fortification near it. Only while no fief of that kingdom is worth a siege does the lord
         * want it, and then more than any other siege, so the invasion marches on to finish the kingdom instead of
         * turning on its neighbours. A lord already besieging it keeps wanting it, so it does not leave the siege for
         * a nearer one. A fief it cannot take, as vanilla judges it, stays unwanted.
         */
        public float FindUnborderedTargetSiegeScore(InvasionConquestRules rules, float targetScore,
            float bestTargetSiegeScore, float bestOtherSiegeScore, bool isBesiegingIt) =>
            rules.TargetSiegePreference <= 1f || targetScore <= 0f || (bestTargetSiegeScore > 0f && !isBesiegingIt)
                ? 0f
                : System.Math.Max(targetScore * UnborderedDistanceScore * rules.TargetSiegePreference,
                    bestOtherSiegeScore * LeadOverOtherSieges);

        /**
         * A lord's wish to join the siege an allied realm of its line lays to a fief of the kingdom being conquered,
         * which vanilla never weighs because the camp is not its realm's. It is wanted above every siege the lord
         * could open, so the first besiegers are not worn down by the garrison while their ally opens another siege.
         * A siege it cannot win even with the camp, as vanilla judges it, stays unwanted.
         */
        public float FindAlliedTargetSiegeScore(InvasionConquestRules rules, float targetScore,
            float bestTargetSiegeScore, float bestOtherSiegeScore) =>
            rules.TargetSiegePreference <= 1f || targetScore <= 0f
                ? 0f
                : System.Math.Max(targetScore * UnborderedDistanceScore * rules.TargetSiegePreference,
                    System.Math.Max(bestTargetSiegeScore, bestOtherSiegeScore) * LeadOverOtherSieges);
    }
}
