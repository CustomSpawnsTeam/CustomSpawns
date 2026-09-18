using System;

namespace CustomSpawns.Invasions
{
    // Pure admission and stock-floor rules. Campaign-state lookup belongs in the adapter.
    internal static class InvasionFoodSupportRules
    {
        internal static bool IsEligibleForOngoingSupport(bool hasCredit, bool isLiveCoastalTown,
            bool isOwnedByCreditedGroup, bool isConfigurationEnabled, bool isUnderSiege)
        {
            return hasCredit && isLiveCoastalTown && isOwnedByCreditedGroup &&
                   isConfigurationEnabled && !isUnderSiege;
        }

        internal static float ApplyFoodStockFloor(float existingStocks, float actualCapacity, float configuredFloor)
        {
            return Math.Max(existingStocks, Math.Min(actualCapacity, configuredFloor));
        }
    }
}
