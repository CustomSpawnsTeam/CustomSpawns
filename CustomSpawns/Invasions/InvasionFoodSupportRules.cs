using System;

namespace CustomSpawns.Invasions
{
    // Pure admission and stock-floor rules. Campaign-state lookup belongs in the adapter.
    internal static class InvasionFoodSupportRules
    {
        internal static bool IsEligibleForOngoingSupport(bool hasFoothold, bool isLiveCoastalTown,
            bool isOwnedByHoldingInvasion, bool isConfigurationEnabled, bool isUnderSiege)
        {
            return hasFoothold && isLiveCoastalTown && isOwnedByHoldingInvasion &&
                   isConfigurationEnabled && !isUnderSiege;
        }

        internal static float ApplyFoodStockFloor(float existingStocks, float actualCapacity, float configuredFloor)
        {
            return Math.Max(existingStocks, Math.Min(actualCapacity, configuredFloor));
        }
    }
}
