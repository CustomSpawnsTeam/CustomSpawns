using System.Collections.Generic;

namespace CustomSpawns.Data.Dto
{
    public sealed class InvasionData
    {
        public InvasionData(bool requiresFirstCoastalConquest,
            IReadOnlyList<InvasionReinforcementRuleData> reinforcementRules, float maxFactionStrength = 0f,
            float strengthPerTownHeld = 0f, string? supplyLine = null, int minimumCapitalGarrison = 0,
            int foodPerConvoy = 0, float foodConvoyIntervalInDays = 0f, int maxLords = 0, int lordsPerTownHeld = 0)
        {
            MaxLords = maxLords;
            LordsPerTownHeld = lordsPerTownHeld;
            FoodPerConvoy = foodPerConvoy;
            FoodConvoyIntervalInDays = foodConvoyIntervalInDays;
            SupplyLine = supplyLine;
            RequiresFirstCoastalConquest = requiresFirstCoastalConquest;
            ReinforcementRules = reinforcementRules;
            MaxFactionStrength = maxFactionStrength;
            StrengthPerTownHeld = strengthPerTownHeld;
            MinimumCapitalGarrison = minimumCapitalGarrison;
        }

        public bool RequiresFirstCoastalConquest { get; }

        // Which supply doctrine the invasion lives under. Null means supplies come by sea.
        public string? SupplyLine { get; }

        // The garrison the invasion keeps standing in its capital: the floor its lords may draw the bank
        // down to but not below.
        public int MinimumCapitalGarrison { get; }

        // How much food each convoy adds to the capital's food stocks. Zero runs no convoys at all.
        public int FoodPerConvoy { get; }

        // How often a convoy arrives, in days. Nothing to do with the reinforcement cadence.
        public float FoodConvoyIntervalInDays { get; }

        /**
         * The strength every reinforcement rule of the invasion draws against, shared between them.
         * Zero leaves the invasion uncapped, which is what a configuration that does not mention it means.
         */
        public float MaxFactionStrength { get; }

        // What each town the invasion's kingdom holds right now adds to that ceiling, whichever of its clans
        // owns it. Losing the town lowers it again.
        public float StrengthPerTownHeld { get; }

        // The most lords the invasion keeps, the lords it already has counted. Zero leaves it uncapped.
        public int MaxLords { get; }

        // What each town the invasion's kingdom holds adds to that cap.
        public int LordsPerTownHeld { get; }

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
