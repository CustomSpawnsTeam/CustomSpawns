using TaleWorlds.CampaignSystem.Settlements;

namespace CustomSpawns.Invasions.Adapters
{
    internal static class InvasionOccupationFood
    {
        internal static void Apply(Town town, float floor)
        {
            town.FoodStocks = InvasionFoodSupportRules.ApplyFoodStockFloor(town.FoodStocks,
                town.FoodStocksUpperLimit(), floor);
        }
    }
}
