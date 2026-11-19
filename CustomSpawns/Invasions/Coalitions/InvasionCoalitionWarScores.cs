using System.Collections.Generic;
using System.Linq;

namespace CustomSpawns.Invasions.Coalitions
{
    /**
     * How much a lord of a kingdom in a coalition wants to fight the line it stands against, against vanilla's wish:
     * crush the line's field armies it can beat, retake the line's fiefs together, and once a capital of the line is
     * cut off, take it to end the reinforcements landing there.
     */
    public sealed class InvasionCoalitionWarScores
    {
        private const float LineSiegePreference = 2f;

        // The lowest distance score vanilla still weighs a siege at, given to a siege it does not weigh at all.
        private const float UnweighedDistanceScore = 0.1f;
        private const float LeadOverOtherSieges = 1.1f;
        private const float CapitalLead = 1.5f;
        private const float HuntPreference = 2f;
        private const float HuntLead = 1.5f;

        // How much stronger the hunters must be than the line's party before they go for it.
        private const float HuntAdvantage = 1.2f;

        // How far, in distances between two neighbouring towns, a lord marches to join a siege or take a capital.
        private const float CampReach = 2f;
        private const float CapitalReach = 4f;

        // A siege of a fief of the line, which vanilla weighs, is wanted more than any siege of another kingdom.
        public float FindLineSiegeScore(float score, float bestOtherSiegeScore) =>
            score <= 0f
                ? score
                : System.Math.Max(score * LineSiegePreference, bestOtherSiegeScore * LeadOverOtherSieges);

        /**
         * Joining the siege another kingdom of the coalition lays to a fief of the line, which vanilla never weighs
         * because the camp is not the lord's realm's. It is wanted above every siege the lord could open, so the
         * coalition piles into one camp instead of wearing itself down on many. A siege it cannot win even with the
         * camp, as vanilla judges it, stays unwanted.
         */
        public float FindCampScore(float targetScore, float bestSiegeScore) =>
            targetScore <= 0f
                ? 0f
                : System.Math.Max(targetScore * UnweighedDistanceScore * LineSiegePreference,
                    bestSiegeScore * LeadOverOtherSieges);

        /**
         * A siege of a cut-off capital of the line is wanted above every other siege, joining a camp included. A
         * capital vanilla does not weigh, being far from the lord's fiefs, is weighed as the farthest one it does.
         * One it cannot win, as vanilla judges it, stays unwanted.
         */
        public float FindCapitalScore(float score, bool isWeighedByVanilla, float bestSiegeScore) =>
            score <= 0f
                ? 0f
                : System.Math.Max(
                    score * LineSiegePreference * (isWeighedByVanilla ? 1f : UnweighedDistanceScore),
                    bestSiegeScore * CapitalLead);

        /**
         * Going after a field party of the line, from vanilla's wish, given how near it is: one at hand, nought at
         * the edge of sight. Only once the lord, with the coalition's parties already on it, outmatches it is it
         * wanted, then above what else the lord could do the nearer it is; until then vanilla's wish stands.
         */
        public float FindHuntScore(float score, float bestOtherScore, float closeness, float huntersStrength,
            float preyStrength) =>
            closeness <= 0f || huntersStrength < preyStrength * HuntAdvantage
                ? score
                : System.Math.Max(score * HuntPreference, bestOtherScore * HuntLead * closeness);

        /**
         * The strength a lord besieges a cut-off capital of the line with: its own, and that of every force of the
         * coalition in reach to join its camp. The coalition takes a capital together, as no realm's army can alone.
         */
        public float FindCoalitionSiegeStrength(float strength, IEnumerable<float> alliesStrengths) =>
            strength + alliesStrengths.Sum();

        public bool IsCampInReach(float distance, float townDistance) => distance <= townDistance * CampReach;

        public bool IsCapitalInReach(float distance, float townDistance) => distance <= townDistance * CapitalReach;
    }
}
