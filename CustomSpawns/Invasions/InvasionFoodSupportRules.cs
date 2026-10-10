using System;

namespace CustomSpawns.Invasions
{
    // Pure admission and stock-floor rules. Campaign-state lookup belongs in the adapter.
    internal static class InvasionFoodSupportRules
    {
        internal static bool IsEligibleForOngoingSupport(bool hasFoothold, bool isLiveSupplyBase,
            bool isOwnedByHoldingInvasion, bool isConfigurationEnabled, bool isUnderSiege)
        {
            return hasFoothold && isLiveSupplyBase && isOwnedByHoldingInvasion &&
                   isConfigurationEnabled && !isUnderSiege;
        }

        internal static float ApplyFoodStockFloor(float existingStocks, float actualCapacity, float configuredFloor)
        {
            return Math.Max(existingStocks, Math.Min(actualCapacity, configuredFloor));
        }

        /**
         * What the invasion lands at the town each day: the shortfall the town cannot cover itself, and no
         * more than feeding the minimum garrison costs plus the town's own allowance. A garrison stocked
         * above that minimum is deliberately left unfed, so troops banked and never spent starve back down
         * to it.
         */
        internal static float DailySupply(float dailyChangeWithoutSupply, int minimumGarrison, int menPerFood,
            float townAllowance)
        {
            float fedGarrison = menPerFood > 0 ? minimumGarrison / (float)menPerFood : 0f;
            return Math.Min(Math.Max(0f, -dailyChangeWithoutSupply), fedGarrison + townAllowance);
        }
    }
}
