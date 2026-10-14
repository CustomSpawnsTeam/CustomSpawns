using System;

namespace CustomSpawns.Invasions
{
    /**
     * The capital garrison read as the invasion's troop bank. Once the capital is held a wave of
     * reinforcements is paid into that garrison whole, and the lords of the invasion draw their warbands
     * back out of it. Nothing limits what the bank may hold: the faction strength ceiling is what stops
     * the waves coming, so the bank is only ever as large as that ceiling lets the invasion grow.
     *
     * The minimum capital garrison is a floor for everyone: the men standing above it are the only ones
     * that are ever spendable.
     */
    public sealed class InvasionTroopBank
    {
        // What a lord may take out: whatever stands above the floor, and never more than he can lead.
        public int Drawable(int wanted, int garrisonCount, int minimumCapitalGarrison) =>
            Math.Max(0, Math.Min(wanted, garrisonCount - minimumCapitalGarrison));

        // The bank only replaces vanilla's free warband when it can give back at least as many men; a lord
        // sent out with fewer than he came with is a lord sent out to be captured.
        public bool ReplacesFreeWarband(int freeTroops, int garrisonCount, int minimumCapitalGarrison) =>
            garrisonCount - minimumCapitalGarrison >= freeTroops;

        /**
         * How many of a wave's lords may be fielded: the invasion keeps at most maxLords, plus
         * lordsPerTownHeld for every town its kingdom holds, counting the lords it already has. Zero
         * maxLords leaves the wave uncapped.
         */
        public int LordsToField(int lordsPerWave, int maxLords, int lordsPerTownHeld, int townsHeld,
            int livingLords) =>
            maxLords <= 0
                ? lordsPerWave
                : Math.Max(0, Math.Min(lordsPerWave, maxLords + lordsPerTownHeld * townsHeld - livingLords));
    }
}
