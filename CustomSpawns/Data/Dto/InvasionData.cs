using System.Collections.Generic;

namespace CustomSpawns.Data.Dto
{
    public sealed class InvasionData
    {
        public InvasionData(bool requiresFirstCoastalConquest,
            IReadOnlyList<InvasionReinforcementRuleData> reinforcementRules, int maxBankedTroops = 0,
            int bankedTroopsPerTownHeld = 0, string? supplyLine = null, int minimumCapitalGarrison = 0,
            int foodPerConvoy = 0, float foodConvoyIntervalInDays = 0f, int maxLords = 0, int lordsPerTownHeld = 0,
            int minimumFiefGarrison = 0, InvasionConquestData? conquest = null, int lordPartySizeBonus = 0,
            string? followsInvasion = null)
        {
            FollowsInvasion = followsInvasion;
            LordPartySizeBonus = lordPartySizeBonus;
            MinimumFiefGarrison = minimumFiefGarrison;
            Conquest = conquest;
            MaxLords = maxLords;
            LordsPerTownHeld = lordsPerTownHeld;
            FoodPerConvoy = foodPerConvoy;
            FoodConvoyIntervalInDays = foodConvoyIntervalInDays;
            SupplyLine = supplyLine;
            RequiresFirstCoastalConquest = requiresFirstCoastalConquest;
            ReinforcementRules = reinforcementRules;
            MaxBankedTroops = maxBankedTroops;
            BankedTroopsPerTownHeld = bankedTroopsPerTownHeld;
            MinimumCapitalGarrison = minimumCapitalGarrison;
        }

        public bool RequiresFirstCoastalConquest { get; }

        // Which supply doctrine the invasion lives under. Null means supplies come by sea.
        public string? SupplyLine { get; }

        // The garrison the invasion keeps standing in its capital: the floor its lords may draw the bank
        // down to but not below.
        public int MinimumCapitalGarrison { get; }

        // What every other fief is garrisoned up to before the invasion takes another. Zero leaves fiefs alone.
        public int MinimumFiefGarrison { get; }

        // Null leaves the invasion's wars to the diplomacy configuration.
        public InvasionConquestData? Conquest { get; }

        // Added to the party size limit of every lord of the invasion. Zero leaves vanilla's limit alone.
        public int LordPartySizeBonus { get; }

        // The party template of the invasion this one is launched alongside. Null for an invasion that spawns
        // by chance on its own.
        public string? FollowsInvasion { get; }

        // How much food each convoy adds to the capital's food stocks. Zero runs no convoys at all.
        public int FoodPerConvoy { get; }

        // How often a convoy arrives, in days. Nothing to do with the reinforcement cadence.
        public float FoodConvoyIntervalInDays { get; }

        /**
         * How many men the capital's bank may hold before the waves stop bringing men, shared by every
         * reinforcement rule. Zero leaves the bank bottomless, which is what a configuration that does not
         * mention it means.
         */
        public int MaxBankedTroops { get; }

        // What each town the invasion's kingdom holds right now adds to that limit, whichever of its clans
        // owns it. Losing the town lowers it again.
        public int BankedTroopsPerTownHeld { get; }

        // The most lords the invasion keeps, the lords it already has counted. Zero leaves it uncapped.
        public int MaxLords { get; }

        // What each town the invasion's kingdom holds adds to that cap.
        public int LordsPerTownHeld { get; }

        // In configuration order, which is the order the rules draw in.
        public IReadOnlyList<InvasionReinforcementRuleData> ReinforcementRules { get; }
    }

    public sealed class InvasionConquestData
    {
        public InvasionConquestData(float threatPerFiefHeld, float openFrontIntervalInDays = 0f,
            float targetSiegePreference = 0f, float reconsiderTargetIntervalInDays = 0f,
            InvasionGarrisonArmyData? garrisonArmy = null)
        {
            ThreatPerFiefHeld = threatPerFiefHeld;
            OpenFrontIntervalInDays = openFrontIntervalInDays;
            TargetSiegePreference = targetSiegePreference;
            ReconsiderTargetIntervalInDays = reconsiderTargetIntervalInDays;
            GarrisonArmy = garrisonArmy;
        }

        // Zero leaves the invasion's neighbours to vanilla diplomacy alone.
        public float ThreatPerFiefHeld { get; }

        // Zero never has the invasion start a war of its own.
        public float OpenFrontIntervalInDays { get; }

        // At most one leaves the invasion's lords to choose their sieges as vanilla lords do.
        public float TargetSiegePreference { get; }

        // Zero keeps the invasion on its target until that kingdom falls.
        public float ReconsiderTargetIntervalInDays { get; }

        // Null never raises an army out of the capital's garrison.
        public InvasionGarrisonArmyData? GarrisonArmy { get; }
    }

    public sealed class InvasionGarrisonArmyData
    {
        public InvasionGarrisonArmyData(float intervalInDays, float garrisonShare)
        {
            IntervalInDays = intervalInDays;
            GarrisonShare = garrisonShare;
        }

        public float IntervalInDays { get; }

        // The share of the garrison's men an army takes, never cutting below the capital's minimum garrison.
        public float GarrisonShare { get; }
    }

    public sealed class InvasionReinforcementRuleData
    {
        public InvasionReinforcementRuleData(string id, string partyTemplateId, float intervalInDays,
            int lordsPerWave = 0, int maxWaves = 0, int maxMenPerWave = 0, int menPerTownHeld = 0)
        {
            MaxMenPerWave = maxMenPerWave;
            MenPerTownHeld = menPerTownHeld;
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

        // The most men one wave banks. Zero banks the template's whole roll.
        public int MaxMenPerWave { get; }

        // The men a wave banks over MaxMenPerWave for every town held, the capital included.
        public int MenPerTownHeld { get; }
    }
}
