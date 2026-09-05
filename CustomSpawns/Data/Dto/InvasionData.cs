namespace CustomSpawns.Data.Dto
{
    public sealed class InvasionData
    {
        public InvasionData(float foodStockFloor, float dailyFoodBonus, int minimumGarrisonTroopCount,
            int maximumGarrisonTroopCount, float militiaStrengthRatio,
            float maximumDonorLossFraction, string reinforcementPartyTemplateId, float reinforcementIntervalInDays,
            int reinforcementActivePartyCap)
        {
            FoodStockFloor = foodStockFloor;
            DailyFoodBonus = dailyFoodBonus;
            MinimumGarrisonTroopCount = minimumGarrisonTroopCount;
            MaximumGarrisonTroopCount = maximumGarrisonTroopCount;
            MilitiaStrengthRatio = militiaStrengthRatio;
            MaximumDonorLossFraction = maximumDonorLossFraction;
            ReinforcementPartyTemplateId = reinforcementPartyTemplateId;
            ReinforcementIntervalInDays = reinforcementIntervalInDays;
            ReinforcementActivePartyCap = reinforcementActivePartyCap;
        }

        public float FoodStockFloor { get; }
        public float DailyFoodBonus { get; }
        public int MinimumGarrisonTroopCount { get; }
        public int MaximumGarrisonTroopCount { get; }
        public float MilitiaStrengthRatio { get; }
        public float MaximumDonorLossFraction { get; }
        public string ReinforcementPartyTemplateId { get; }
        public float ReinforcementIntervalInDays { get; }
        public int ReinforcementActivePartyCap { get; }
    }
}
