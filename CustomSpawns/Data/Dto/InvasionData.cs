using System.Collections.Generic;

namespace CustomSpawns.Data.Dto
{
    public sealed class InvasionData
    {
        public InvasionData(bool requiresFirstCoastalConquest, float foodStockFloor, float townFoodAllowance,
            IReadOnlyList<InvasionReinforcementRuleData> reinforcementRules, float maxFactionStrength = 0f,
            float strengthPerTownHeld = 0f, string? supplyLine = null, int minimumGarrison = 0)
        {
            SupplyLine = supplyLine;
            RequiresFirstCoastalConquest = requiresFirstCoastalConquest;
            FoodStockFloor = foodStockFloor;
            TownFoodAllowance = townFoodAllowance;
            ReinforcementRules = reinforcementRules;
            MaxFactionStrength = maxFactionStrength;
            StrengthPerTownHeld = strengthPerTownHeld;
            MinimumGarrison = minimumGarrison;
        }

        public bool RequiresFirstCoastalConquest { get; }

        // Which supply doctrine the invasion lives under. Null means supplies come by sea.
        public string? SupplyLine { get; }
        public float FoodStockFloor { get; }

        // What the supplies cover each day on top of feeding the minimum garrison.
        public float TownFoodAllowance { get; }

        // The garrison the invasion keeps in the town it holds: what its supplies are sized to feed, and the
        // floor its lords may draw down to but not below.
        public int MinimumGarrison { get; }

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
            int lordsPerWave = 0, int maxWaves = 0)
        {
            Id = id;
            PartyTemplateId = partyTemplateId;
            IntervalInDays = intervalInDays;
            LordsPerWave = lordsPerWave;
            MaxWaves = maxWaves;
        }

        public string Id { get; }
        public string PartyTemplateId { get; }
        public float IntervalInDays { get; }

        // How many lords the wave brings while the invasion holds its capital.
        public int LordsPerWave { get; }

        // How many waves the rule ever sends. Zero leaves it sending for as long as the invasion lives.
        public int MaxWaves { get; }
    }
}
