using System;
using System.Collections.Generic;
using System.Linq;

namespace CustomSpawns.Invasions
{
    /**
     * The capital garrison read as the invasion's troop bank. Once the capital is held a wave of
     * reinforcements is paid into that garrison, and the lords of the invasion draw their warbands back
     * out of it. The bank limit is what stops the waves coming, so the bank is only ever about as large as
     * that limit and the last wave over it.
     *
     * The minimum capital garrison is a floor for everyone: the men standing above it are the only ones
     * that are ever spendable.
     */
    public sealed class InvasionTroopBank
    {
        // What a lord may take out: whatever stands above the floor, and never more than he can lead.
        public int Drawable(int wanted, int garrisonCount, int minimumCapitalGarrison) =>
            Math.Max(0, Math.Min(wanted, garrisonCount - minimumCapitalGarrison));

        // Each party takes the garrison's mix, with spare men going to the largest fractional shares.
        public int[] Distribute(IReadOnlyList<int> troopCounts, int wanted)
        {
            int[] allocated = new int[troopCounts.Count];
            int total = troopCounts.Sum();

            if (total == 0 || wanted <= 0)
            {
                return allocated;
            }

            int drawn = Math.Min(wanted, total);

            for (int index = 0; index < troopCounts.Count; index++)
            {
                allocated[index] = (int)((long)troopCounts[index] * drawn / total);
            }

            int remaining = drawn - allocated.Sum();

            foreach (int index in Enumerable.Range(0, troopCounts.Count)
                         .OrderByDescending(index => (long)troopCounts[index] * drawn % total)
                         .Take(remaining))
            {
                allocated[index]++;
            }

            return allocated;
        }

        // A wave rolled over its cap keeps the template's mix, every kind of troop cut by the same share.
        public int[] CapWave(IReadOnlyList<int> rolled, int maxMen) =>
            maxMen <= 0 ? rolled.ToArray() : Distribute(rolled, maxMen);

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
