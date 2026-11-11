namespace CustomSpawns.Invasions
{
    /**
     * A kingdom bordering the invasion, how strong it is, how long a border they share, whether it has a coast and
     * whether the two are already at war.
     */
    public readonly struct InvasionNeighbour
    {
        public InvasionNeighbour(string kingdomId, float strength, int borderFortifications = 0,
            bool isCoastal = false, bool isAtWar = false)
        {
            KingdomId = kingdomId;
            Strength = strength;
            BorderFortifications = borderFortifications;
            IsCoastal = isCoastal;
            IsAtWar = isAtWar;
        }

        public string KingdomId { get; }
        public float Strength { get; }

        // How many of its fortifications lie next to one of the invasion's.
        public int BorderFortifications { get; }

        // Whether a fleet could land in one of its towns.
        public bool IsCoastal { get; }

        public bool IsAtWar { get; }
    }
}
