using System.Collections.Generic;

namespace CustomSpawns.Data.Dto
{
    public sealed class InvasionData
    {
        public InvasionData(bool requiresFirstCoastalConquest, float foodStockFloor, float dailyFoodBonus,
            IReadOnlyList<InvasionReinforcementRuleData> reinforcementRules)
        {
            RequiresFirstCoastalConquest = requiresFirstCoastalConquest;
            FoodStockFloor = foodStockFloor;
            DailyFoodBonus = dailyFoodBonus;
            ReinforcementRules = reinforcementRules;
        }

        public bool RequiresFirstCoastalConquest { get; }
        public float FoodStockFloor { get; }
        public float DailyFoodBonus { get; }

        // In configuration order, which is the order the rules draw in.
        public IReadOnlyList<InvasionReinforcementRuleData> ReinforcementRules { get; }
    }

    public sealed class InvasionReinforcementRuleData
    {
        public InvasionReinforcementRuleData(string id, string partyTemplateId, float intervalInDays,
            int maxConcurrentParties)
        {
            Id = id;
            PartyTemplateId = partyTemplateId;
            IntervalInDays = intervalInDays;
            MaxConcurrentParties = maxConcurrentParties;
        }

        public string Id { get; }
        public string PartyTemplateId { get; }
        public float IntervalInDays { get; }
        public int MaxConcurrentParties { get; }
    }
}
