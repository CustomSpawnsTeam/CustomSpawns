namespace CustomSpawns.Invasions
{
    public readonly struct InvasionConquestRules
    {
        public InvasionConquestRules(float threatPerFiefHeld, float openFrontIntervalInDays,
            float targetSiegePreference = 0f, float reconsiderTargetIntervalInDays = 0f, int maxRealms = 0)
        {
            ThreatPerFiefHeld = threatPerFiefHeld;
            OpenFrontIntervalInDays = openFrontIntervalInDays;
            TargetSiegePreference = targetSiegePreference;
            ReconsiderTargetIntervalInDays = reconsiderTargetIntervalInDays;
            MaxRealms = maxRealms;
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

        // How many realms of the line may fight at once, its first included. Zero sets no limit.
        public int MaxRealms { get; }
    }
}
