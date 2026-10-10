using System.Collections.Generic;

namespace CustomSpawns.Data.Dto
{
    public sealed class InvasionData
    {
        public InvasionData(bool requiresFirstCoastalConquest, float foodStockFloor, float dailyFoodBonus,
            IReadOnlyList<InvasionReinforcementRuleData> reinforcementRules, float maxFactionStrength = 0f,
            float strengthPerTownHeld = 0f, string? supplyLine = null)
        {
            SupplyLine = supplyLine;
            RequiresFirstCoastalConquest = requiresFirstCoastalConquest;
            FoodStockFloor = foodStockFloor;
            DailyFoodBonus = dailyFoodBonus;
            ReinforcementRules = reinforcementRules;
            MaxFactionStrength = maxFactionStrength;
            StrengthPerTownHeld = strengthPerTownHeld;
        }

        public bool RequiresFirstCoastalConquest { get; }

        // Which supply doctrine the invasion lives under. Null means supplies come by sea.
        public string? SupplyLine { get; }
        public float FoodStockFloor { get; }
        public float DailyFoodBonus { get; }

        /**
         * The strength every reinforcement rule of the invasion draws against, shared between them.
         * Zero leaves the invasion uncapped, which is what a configuration that does not mention it means.
         */
        public float MaxFactionStrength { get; }

        // What each town the invasion's kingdom holds right now adds to that ceiling, whichever of its clans
        // owns it. Losing the town lowers it again.
        public float StrengthPerTownHeld { get; }

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
