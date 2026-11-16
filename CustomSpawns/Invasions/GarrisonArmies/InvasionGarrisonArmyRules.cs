namespace CustomSpawns.Invasions.GarrisonArmies
{
    public readonly struct InvasionGarrisonArmyRules
    {
        public InvasionGarrisonArmyRules(float intervalInDays, float garrisonShare, int minimumCapitalGarrison)
        {
            IntervalInDays = intervalInDays;
            GarrisonShare = garrisonShare;
            MinimumCapitalGarrison = minimumCapitalGarrison;
        }

        // The fewest days between two armies raised out of the capital's garrison.
        public float IntervalInDays { get; }

        // The share of the garrison's men an army takes.
        public float GarrisonShare { get; }

        // The garrison an army, like the invasion's lords, never draws the capital below.
        public int MinimumCapitalGarrison { get; }
    }
}
