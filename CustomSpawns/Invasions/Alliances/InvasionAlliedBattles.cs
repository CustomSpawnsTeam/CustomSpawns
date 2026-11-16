namespace CustomSpawns.Invasions.Alliances
{
    /**
     * Whether a lord rides into a battle its allies fight nearby. Vanilla weighs a battle with its own realm's
     * strength alone, so a lord of one kingdom shies from an invasion's army another kingdom is already beating, and a
     * lord of one realm of a line flees the army another realm of the line is fighting.
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
         * Whether a lord may ride against the enemy of an allied battle, a coalition formed or not: it is at war with
         * that enemy, and the enemy is an invasion realm, or a kingdom fought by an invasion realm's lord. Bandits and
         * minor clans are left to vanilla, so a line's lords keep to its conquest.
         */
        public bool AreRivals(bool isInvasionRealm, bool enemyIsInvasionRealm, bool enemyIsKingdom, bool isAtWar) =>
            isAtWar && (enemyIsInvasionRealm || (isInvasionRealm && enemyIsKingdom));

        /**
         * Whether joining the battle replaces vanilla's initiative: when it is wanted more, or when vanilla would flee
         * from the very enemy the allies are beating, having weighed it without them.
         */
        public bool OverridesInitiative(float joinScore, float initiativeScore, bool isFleeingTheBattle) =>
            joinScore > InitiativeThreshold && (joinScore > initiativeScore || isFleeingTheBattle);
    }
}
