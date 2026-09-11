namespace CustomSpawns.Data.Dto
{
    public sealed class InvasionData
    {
        public InvasionData(bool requiresFirstCoastalConquest, float foodStockFloor, float dailyFoodBonus,
            string reinforcementPartyTemplateId, float reinforcementIntervalInDays,
            int reinforcementActivePartyCap)
        {
            RequiresFirstCoastalConquest = requiresFirstCoastalConquest;
            FoodStockFloor = foodStockFloor;
            DailyFoodBonus = dailyFoodBonus;
            ReinforcementPartyTemplateId = reinforcementPartyTemplateId;
            ReinforcementIntervalInDays = reinforcementIntervalInDays;
            ReinforcementActivePartyCap = reinforcementActivePartyCap;
        }

        public bool RequiresFirstCoastalConquest { get; }
        public float FoodStockFloor { get; }
        public float DailyFoodBonus { get; }
        public string ReinforcementPartyTemplateId { get; }
        public float ReinforcementIntervalInDays { get; }
        public int ReinforcementActivePartyCap { get; }
    }
}
