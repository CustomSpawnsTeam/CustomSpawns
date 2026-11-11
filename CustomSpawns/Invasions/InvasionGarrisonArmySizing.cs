using System;

namespace CustomSpawns.Invasions
{
    // How many men an army raised out of an invasion's capital garrison takes, and how long they are fed for.
    public sealed class InvasionGarrisonArmySizing
    {
        /**
         * The lord buys food in the towns he passes like any other, but a siege camp has no market and vanilla breaks
         * up an army whose parties starve, so he sets out with a month of it: enough to see most sieges through.
         */
        private const float FoodDays = 30f;

        /**
         * The men an army takes out of a garrison: its share of them, but never the capital's minimum garrison.
         * A garrison of 3000 sends 1500 at a half share; one of 10000 sends 5000. Zero when the garrison is at
         * or below that minimum.
         */
        public int FindMen(InvasionGarrisonArmyRules rules, int garrisonMen)
        {
            int share = (int)Math.Floor(garrisonMen * (double)rules.GarrisonShare);

            return Math.Max(0, Math.Min(share, garrisonMen - rules.MinimumCapitalGarrison));
        }

        // Never fewer days than vanilla wants of an army before it lets one be called.
        public int FindFood(int men, int menPerFoodPerDay, float minimumFoodDays)
        {
            float days = Math.Max(FoodDays, minimumFoodDays);

            return men <= 0 ? 0 : (int)Math.Ceiling(men * days / Math.Max(1, menPerFoodPerDay));
        }
    }
}
