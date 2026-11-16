namespace CustomSpawns.Invasions
{
    public readonly struct InvasionConquestRules
    {
        public InvasionConquestRules(float threatPerFiefHeld, float openFrontIntervalInDays,
            float targetSiegePreference = 0f, float reconsiderTargetIntervalInDays = 0f,
            float coalitionFiefShare = 0f, int maxRealms = 0, int coalitionCapitalRing = 0,
            float coalitionDisbandFiefShare = 0f)
        {
            ThreatPerFiefHeld = threatPerFiefHeld;
            OpenFrontIntervalInDays = openFrontIntervalInDays;
            TargetSiegePreference = targetSiegePreference;
            ReconsiderTargetIntervalInDays = reconsiderTargetIntervalInDays;
            CoalitionFiefShare = coalitionFiefShare;
            MaxRealms = maxRealms;
            CoalitionCapitalRing = coalitionCapitalRing;
            CoalitionDisbandFiefShare = coalitionDisbandFiefShare;
        }

        // The share of the invader's own worth every fief it holds adds to a kingdom's will to fight it.
        public float ThreatPerFiefHeld { get; }

        // The fewest days between two wars the invasion starts itself. Zero never starts one.
        public float OpenFrontIntervalInDays { get; }

        // How many times more the invasion's lords want a siege of the conquered kingdom's fiefs. At most one
        // leaves their sieges to vanilla.
        public float TargetSiegePreference { get; }

        // The fewest days between two times the invasion weighs its target against its neighbours. Zero keeps the
        // target until it falls.
        public float ReconsiderTargetIntervalInDays { get; }

        // The share of every fief the line holds when the other kingdoms band together against it, until it is
        // destroyed. Zero never.
        public float CoalitionFiefShare { get; }

        // How many realms of the line may fight at once, its first included. Zero sets no limit.
        public int MaxRealms { get; }

        // How many of the fiefs nearest a capital of the line must be lost to it before the coalition besieges that
        // capital above every other fief. Zero never.
        public int CoalitionCapitalRing { get; }

        // The share of every fief below which the coalition breaks up against a line holding none of its capitals.
        // Zero keeps it standing until the line is destroyed.
        public float CoalitionDisbandFiefShare { get; }
    }
}
