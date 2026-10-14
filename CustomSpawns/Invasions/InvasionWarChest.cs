using System;

namespace CustomSpawns.Invasions
{
    /**
     * The gold and food a reinforcement wave brings each clan of the invasion, sized for the warbands its
     * lords are meant to lead rather than the ones they happen to have left.
     *
     * Vanilla does not let a lord keep the men his clan can pay for: it lets him keep the men his clan
     * leader's purse says he may. Every day it turns that purse into a wage limit for each war party, and a
     * party paying more than its limit loses men to desertion until it fits. A clan therefore needs a
     * reserve standing in its purse, on top of the wages it spends.
     */
    public sealed class InvasionWarChest
    {
        // Vanilla's wage limit for a party whose clan is too poor to grant any more, and the limit at the top
        // of each band of the purse.
        private const int PoorWageLimit = 200;
        private const int LowerBandWageLimit = 600;
        private const int UpperBandWageLimit = 1200;

        /**
         * The gold a clan leader must hold for vanilla to let each of his clan's parties pay wageLimit a day.
         * Below lowerBandGold the limit grows with the square of the purse up to 600, up to upperBandGold
         * it grows linearly to 1200, and above upperBandGold the clan pays whatever its parties cost.
         */
        public int WageLimitReserve(int wageLimit, int lowerBandGold, int upperBandGold)
        {
            if (wageLimit <= PoorWageLimit)
            {
                return 0;
            }

            if (wageLimit <= LowerBandWageLimit)
            {
                double share = (double)(wageLimit - PoorWageLimit) / (LowerBandWageLimit - PoorWageLimit);
                return (int)Math.Ceiling(lowerBandGold * Math.Sqrt(share));
            }

            if (wageLimit <= UpperBandWageLimit)
            {
                double share = (double)(wageLimit - LowerBandWageLimit) / (UpperBandWageLimit - LowerBandWageLimit);
                return (int)Math.Ceiling(lowerBandGold + share * (upperBandGold - lowerBandGold));
            }

            return upperBandGold + 1;
        }

        // What the wave pays a clan: the reserve its largest warband needs plus every wage until the next
        // wave, less what the clan already holds.
        public int Allowance(int targetWageLimit, int targetDailyWages, float intervalInDays, int gold,
            int lowerBandGold, int upperBandGold)
        {
            int needed = WageLimitReserve(targetWageLimit, lowerBandGold, upperBandGold) +
                         (int)Math.Ceiling(targetDailyWages * intervalInDays);

            return Math.Max(0, needed - gold);
        }

        // The food a warband of sizeLimit men needs to last the given days, less what it already carries.
        public int FoodShortfall(int sizeLimit, int menPerFoodPerDay, float days, int food) =>
            Math.Max(0, (int)Math.Ceiling((double)sizeLimit / menPerFoodPerDay * days) - food);
    }
}
