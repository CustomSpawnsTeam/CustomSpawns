using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace CustomSpawns.Data.Dto
{
    public sealed class InvasionData
    {
        public InvasionData(IReadOnlyList<string> coastalTownIds, float foodStockFloor, float dailyFoodBonus,
            int minimumGarrisonTroopCount, int maximumGarrisonTroopCount, float militiaStrengthRatio,
            float maximumDonorLossFraction, string reinforcementPartyTemplateId, float reinforcementIntervalInDays,
            int reinforcementActivePartyCap)
        {
            CoastalTownIds = new ReadOnlyCollection<string>(new List<string>(coastalTownIds));
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

        public IReadOnlyList<string> CoastalTownIds { get; }
        public bool UsesNativePortMetadata => CoastalTownIds.Count == 0;
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
