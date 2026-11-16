using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using CustomSpawns.Invasions;

namespace CustomSpawns.Data.Model
{
    public sealed class InvasionConfig
    {
        [XmlElement("RequiresFirstCoastalConquest")]
        public bool RequiresFirstCoastalConquest { get; set; }

        // Where the invasion's supplies may come from: Coastal, Any or None. Left out, supplies come by
        // sea, which is what every invasion did before there was a choice.
        [XmlElement("SupplyLine")]
        public string? SupplyLine { get; set; }

        // How many men the capital's garrison, the troop bank, may hold before the waves stop bringing men.
        // Shared by all of the reinforcement rules. Left out, the bank is bottomless.
        [XmlElement("MaxBankedTroops")]
        public int MaxBankedTroops { get; set; }

        // Raises that limit for every town the invasion's kingdom currently holds, allies' towns included.
        [XmlElement("BankedTroopsPerTownHeld")]
        public int BankedTroopsPerTownHeld { get; set; }

        // How many lords the invasion keeps at most; the waves field no more once it has them. Left out, the
        // waves field every lord they bring.
        [XmlElement("MaxLords")]
        public int MaxLords { get; set; }

        // Raises that cap for every town the invasion's kingdom currently holds.
        [XmlElement("LordsPerTownHeld")]
        public int LordsPerTownHeld { get; set; }

        // How many men stay standing in the invasion's capital: its lords draw that garrison down to this
        // but never below it.
        [XmlElement("MinimumCapitalGarrison")]
        public int MinimumCapitalGarrison { get; set; }

        // How many men every other fief the invasion holds is garrisoned with out of the capital's bank. Its
        // lords take no new fief while any of them stands below it. Left out, fiefs keep what they are left.
        [XmlElement("MinimumFiefGarrison")]
        public int MinimumFiefGarrison { get; set; }

        // How many more men each of the invasion's lords may lead than vanilla allows him. Left out, vanilla's
        // party size limit stands.
        [XmlElement("LordPartySizeBonus")]
        public int LordPartySizeBonus { get; set; }

        // The party template of the invasion this one is launched to fight alongside, once that one has taken
        // its first kingdom. Such an invasion never spawns by chance. Left out, the invasion comes on its own.
        [XmlElement("FollowsInvasion")]
        public string? FollowsInvasion { get; set; }

        // Who the invasion makes war on. Left out, the diplomacy configuration alone decides.
        [XmlElement("Conquest")]
        public InvasionConquestConfig? Conquest { get; set; }

        // The food convoys the invasion runs into its capital, on their own cadence.
        [XmlElement("Supplies")]
        public InvasionSuppliesConfig Supplies { get; set; } = new();

        [XmlElement("Reinforcements")]
        public InvasionReinforcementsConfig Reinforcements { get; set; } = new();
    }

    public sealed class InvasionSuppliesConfig
    {
        // How much food each convoy adds straight to the capital's food stocks, which the market only does
        // once grain sells. Left out, the invasion runs no convoys and its capital feeds itself.
        [XmlElement("FoodPerConvoy")]
        public int FoodPerConvoy { get; set; }

        // How often a convoy arrives.
        [XmlElement("IntervalInDays")]
        public float IntervalInDays { get; set; } = 7f;
    }

    /**
     * The invasion stays at war with the kingdom it first takes a fief from. Every other kingdom makes war
     * and peace with it as with any realm, but the more fiefs it holds the more its neighbours fear it.
     */
    public sealed class InvasionConquestConfig
    {
        // The share of the invasion's own worth each fief it holds adds to its neighbours' will to fight it.
        // Kingdoms go to war at about one sixth of that worth. Left out, nobody fears it more than vanilla does.
        [XmlElement("ThreatPerFiefHeld")]
        public float ThreatPerFiefHeld { get; set; }

        // How often, at most, the invasion declares war on the kingdom whose fief lies nearest when that is not
        // a kingdom it already fights. Left out, it only ever fights the wars others bring to it.
        [XmlElement("OpenFrontIntervalInDays")]
        public float OpenFrontIntervalInDays { get; set; }

        // How many times more the invasion's lords want to besiege a fief of the kingdom they conquer than any
        // other, until it falls. Left out, or at most one, they choose their sieges as vanilla lords do.
        [XmlElement("TargetSiegePreference")]
        public float TargetSiegePreference { get; set; }

        // How often the invasion weighs its target against the other kingdoms on its borders, and turns on one that
        // has become a much better prey. Left out, it keeps its target until that kingdom falls.
        [XmlElement("ReconsiderTargetIntervalInDays")]
        public float ReconsiderTargetIntervalInDays { get; set; }

        // The share of every fief in the land the invasion's whole line holds when every other kingdom drops its
        // quarrels to fight it: they make peace with each other and declare no war but on the line, until the line
        // is destroyed. Left out, the kingdoms never band together against it.
        [XmlElement("CoalitionFiefShare")]
        public float CoalitionFiefShare { get; set; }

        // The share of every fief in the land below which the coalition breaks up against a line that holds none of
        // its capitals: the line is beaten, and the kingdoms go back to their own quarrels. Below CoalitionFiefShare.
        // Left out, the coalition stands until the line is destroyed.
        [XmlElement("CoalitionDisbandFiefShare")]
        public float CoalitionDisbandFiefShare { get; set; }

        // How many realms, the waves of the line, may fight at once, its first included: no new one lands while that
        // many stand, however many kingdoms have fallen. Left out, the line lands one for every fallen kingdom.
        [XmlElement("MaxRealms")]
        public int MaxRealms { get; set; }

        // How many of the fiefs nearest a capital of the line the coalition must hold before it turns on that
        // capital above every other siege, to cut the line's reinforcements. Left out, it never does.
        [XmlElement("CoalitionCapitalRing")]
        public int CoalitionCapitalRing { get; set; }

        // Now and then raises one big army out of the capital's garrison against the kingdom it conquers. Left
        // out, the garrison only ever feeds the lords and the fiefs.
        [XmlElement("GarrisonArmy")]
        public InvasionGarrisonArmyConfig? GarrisonArmy { get; set; }
    }

    /**
     * The invasion's lords alone are seldom twice as strong as the last fiefs they besiege, which vanilla wants
     * before it lays a siege, while thousands wait in the capital. A lord leads those out, as parties of their own
     * in his army, and they come back to the garrison when the army breaks up.
     */
    public sealed class InvasionGarrisonArmyConfig
    {
        // The fewest days between two armies.
        [XmlElement("IntervalInDays")]
        public float IntervalInDays { get; set; } = 90f;

        // The share of the garrison's men the army takes, never leaving fewer than MinimumCapitalGarrison behind.
        [XmlElement("GarrisonShare")]
        public float GarrisonShare { get; set; } = 0.5f;
    }

    public sealed class InvasionReinforcementsConfig
    {
        [XmlElement("Rule")]
        public List<InvasionReinforcementRuleConfig> Rules { get; set; } = new();
    }

    public sealed class InvasionReinforcementRuleConfig
    {
        // Names the rule's own clock, so editing or reordering the configuration does not move a running
        // schedule onto another rule.
        [XmlElement("Id")]
        public string? Id { get; set; }

        [XmlElement("PartyTemplateId")]
        public string? PartyTemplateId { get; set; }

        [XmlElement("IntervalInDays")]
        public float IntervalInDays { get; set; } = 7f;

        // How many waves the rule ever sends. Left out, it keeps sending for as long as the invasion lives;
        // one wave makes the rule a single reinforcement that arrives the interval after the capital falls.
        [XmlElement("MaxWaves")]
        public int MaxWaves { get; set; }

        // How many lords join the invasion on each wave while it holds its capital. They fill their parties
        // from the garrison. Left out, a wave brings troops only.
        [XmlElement("LordsPerWave")]
        public int LordsPerWave { get; set; }

        // How many men one wave banks. The template is rolled again until there are that many, and cut down to
        // them with its mix kept. Left out, one whole roll is banked.
        [XmlElement("MaxMenPerWave")]
        public int MaxMenPerWave { get; set; }

        // The men a wave banks over MaxMenPerWave for every town the invasion holds, its capital included, as
        // BankedTroopsPerTownHeld and LordsPerTownHeld count them. It needs MaxMenPerWave.
        [XmlElement("MenPerTownHeld")]
        public int MenPerTownHeld { get; set; }
    }

    public static class InvasionConfigValidator
    {
        public static void Validate(InvasionConfig config)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }
            if (config.Supplies == null || config.Reinforcements?.Rules == null)
            {
                throw new ArgumentException("Invasion configuration groups cannot be null.");
            }

            if (InvasionSupplyLines.Find(config.SupplyLine) == null)
            {
                throw new ArgumentException("Invasion SupplyLine '" + config.SupplyLine + "' must be one of " +
                                           InvasionSupplyLines.KnownNames + ".");
            }

            ValidateNonNegative("Invasion MaxBankedTroops", config.MaxBankedTroops);
            ValidateNonNegative("Invasion BankedTroopsPerTownHeld", config.BankedTroopsPerTownHeld);
            ValidateNonNegative("Invasion MaxLords", config.MaxLords);
            ValidateNonNegative("Invasion LordsPerTownHeld", config.LordsPerTownHeld);
            ValidateNonNegative("Invasion MinimumCapitalGarrison", config.MinimumCapitalGarrison);
            ValidateNonNegative("Invasion MinimumFiefGarrison", config.MinimumFiefGarrison);
            ValidateNonNegative("Invasion LordPartySizeBonus", config.LordPartySizeBonus);
            if (config.Conquest != null)
            {
                ValidateNonNegativeFinite("Invasion Conquest ThreatPerFiefHeld", config.Conquest.ThreatPerFiefHeld);
                ValidateNonNegativeFinite("Invasion Conquest OpenFrontIntervalInDays",
                    config.Conquest.OpenFrontIntervalInDays);
                ValidateNonNegativeFinite("Invasion Conquest TargetSiegePreference",
                    config.Conquest.TargetSiegePreference);
                ValidateNonNegativeFinite("Invasion Conquest ReconsiderTargetIntervalInDays",
                    config.Conquest.ReconsiderTargetIntervalInDays);
                ValidateNonNegativeFinite("Invasion Conquest CoalitionFiefShare", config.Conquest.CoalitionFiefShare);
                ValidateNonNegative("Invasion Conquest MaxRealms", config.Conquest.MaxRealms);
                ValidateNonNegative("Invasion Conquest CoalitionCapitalRing", config.Conquest.CoalitionCapitalRing);
                if (config.Conquest.CoalitionFiefShare > 1f)
                {
                    throw new ArgumentException("Invasion Conquest CoalitionFiefShare cannot be more than one.");
                }

                ValidateNonNegativeFinite("Invasion Conquest CoalitionDisbandFiefShare",
                    config.Conquest.CoalitionDisbandFiefShare);

                // At or above the forming share, a coalition would break up and form again against the same line.
                if (config.Conquest.CoalitionDisbandFiefShare > 0f &&
                    config.Conquest.CoalitionDisbandFiefShare >= config.Conquest.CoalitionFiefShare)
                {
                    throw new ArgumentException(
                        "Invasion Conquest CoalitionDisbandFiefShare must be less than CoalitionFiefShare.");
                }
                if (config.Conquest.GarrisonArmy != null)
                {
                    ValidateGarrisonArmy(config.Conquest.GarrisonArmy);
                }
            }
            ValidateNonNegative("Invasion Supplies FoodPerConvoy", config.Supplies.FoodPerConvoy);
            // Convoys are looked at once a day, so a shorter interval than that would only lie about itself.
            if (config.Supplies.FoodPerConvoy > 0 &&
                (!IsFinite(config.Supplies.IntervalInDays) || config.Supplies.IntervalInDays < 1f))
            {
                throw new ArgumentException("Invasion Supplies IntervalInDays must be finite and at least one day.");
            }
            if (config.Reinforcements.Rules.Count == 0)
            {
                throw new ArgumentException("Invasion Reinforcements requires at least one Rule.");
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);

            foreach (InvasionReinforcementRuleConfig rule in config.Reinforcements.Rules)
            {
                ValidateRule(rule, ids);
            }
        }

        private static void ValidateRule(InvasionReinforcementRuleConfig rule, HashSet<string> ids)
        {
            if (string.IsNullOrWhiteSpace(rule.Id))
            {
                throw new ArgumentException("Invasion Reinforcements Rule Id is required.");
            }
            if (!ids.Add(rule.Id!))
            {
                throw new ArgumentException("Invasion Reinforcements Rule Id '" + rule.Id + "' is used twice.");
            }
            if (string.IsNullOrWhiteSpace(rule.PartyTemplateId))
            {
                throw new ArgumentException("Invasion Reinforcements Rule '" + rule.Id +
                                           "' PartyTemplateId is required.");
            }
            // Waves are looked at once a day, so a shorter interval than that would only lie about itself.
            if (!IsFinite(rule.IntervalInDays) || rule.IntervalInDays < 1f)
            {
                throw new ArgumentException("Invasion Reinforcements Rule '" + rule.Id +
                                           "' IntervalInDays must be finite and at least one day.");
            }
            ValidateNonNegative("Invasion Reinforcements Rule '" + rule.Id + "' LordsPerWave", rule.LordsPerWave);
            ValidateNonNegative("Invasion Reinforcements Rule '" + rule.Id + "' MaxWaves", rule.MaxWaves);
            ValidateNonNegative("Invasion Reinforcements Rule '" + rule.Id + "' MaxMenPerWave", rule.MaxMenPerWave);
            ValidateNonNegative("Invasion Reinforcements Rule '" + rule.Id + "' MenPerTownHeld", rule.MenPerTownHeld);
        }

        private static void ValidateGarrisonArmy(InvasionGarrisonArmyConfig garrisonArmy)
        {
            // Garrisons are looked at once a day, so a shorter interval than that would only lie about itself.
            if (!IsFinite(garrisonArmy.IntervalInDays) || garrisonArmy.IntervalInDays < 1f)
            {
                throw new ArgumentException(
                    "Invasion Conquest GarrisonArmy IntervalInDays must be finite and at least one day.");
            }
            ValidateNonNegativeFinite("Invasion Conquest GarrisonArmy GarrisonShare", garrisonArmy.GarrisonShare);
            if (garrisonArmy.GarrisonShare > 1f)
            {
                throw new ArgumentException("Invasion Conquest GarrisonArmy GarrisonShare cannot be more than one.");
            }
        }

        private static void ValidateNonNegative(string name, int value)
        {
            if (value < 0)
            {
                throw new ArgumentException(name + " cannot be negative.");
            }
        }

        private static void ValidateNonNegativeFinite(string name, float value)
        {
            if (!IsFinite(value))
            {
                throw new ArgumentException(name + " must be finite.");
            }
            if (value < 0f)
            {
                throw new ArgumentException(name + " cannot be negative.");
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
