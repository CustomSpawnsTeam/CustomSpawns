using CustomSpawns.Data.Dto;
using CustomSpawns.Data.Model;

namespace CustomSpawns.Data.Adapter
{
    internal static class InvasionDtoAdapter
    {
        public static InvasionData Adapt(InvasionConfig config)
        {
            InvasionConfigValidator.Validate(config);
            return new InvasionData(config.RequiresFirstCoastalConquest, config.Supplies.FoodStockFloor,
                config.Supplies.DailyFoodBonus,
                config.Reinforcements.PartyTemplateId!, config.Reinforcements.IntervalInDays,
                config.Reinforcements.MaxConcurrentParties);
        }
    }
}
