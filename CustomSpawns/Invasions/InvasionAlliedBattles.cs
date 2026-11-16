namespace CustomSpawns.Invasions
{
    /**
     * Whether a lord rides into a battle its allies fight nearby. Vanilla weighs a battle with its own realm's
     * strength alone, so a lord of one kingdom of a coalition shies from the line's army another kingdom is already
     * beating, and the coalition fights its battles one realm at a time.
     */
    public sealed class InvasionAlliedBattles
    {
        // How much stronger the lord and its allies must be than the enemy's side before it joins.
        private const float JoinAdvantage = 1.2f;

        // Vanilla's lift for a battle the lord's own side already fights.
        private const float BattlePreference = 1.2f;
        private const float MaximumAdvantageScore = 3f;

        // Vanilla takes an initiative only above this score.
        private const float InitiativeThreshold = 1f;

        /**
         * The score of joining the battle, from the strength of the lord, of its allies' side, and of the enemy's,
         * as vanilla scores going after an enemy it outmatches; nought when the lord and its allies would not win.
         */
        public float FindJoinScore(float strength, float alliesStrength, float enemyStrength)
        {
            if (enemyStrength <= 0f)
            {
                return 0f;
            }

            float advantage = (strength + alliesStrength) / enemyStrength;

            return advantage < JoinAdvantage
                ? 0f
                : BattlePreference * System.Math.Min(MaximumAdvantageScore, 0.5f * (1f + advantage));
        }

        /**
         * Whether joining the battle replaces vanilla's initiative: when it is wanted more, or when vanilla would flee
         * from the very enemy the allies are beating, having weighed it without them.
         */
        public bool OverridesInitiative(float joinScore, float initiativeScore, bool isFleeingTheBattle) =>
            joinScore > InitiativeThreshold && (joinScore > initiativeScore || isFleeingTheBattle);
    }
}
