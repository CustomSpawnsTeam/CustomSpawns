namespace CustomSpawns.Invasions.Conquest
{
    // A fief the invasion could take next, and how far it lies from the nearest fief the invasion holds.
    public readonly struct InvasionPrey
    {
        public InvasionPrey(string kingdomId, float distance, bool isAtWar)
        {
            KingdomId = kingdomId;
            Distance = distance;
            IsAtWar = isAtWar;
        }

        public string KingdomId { get; }
        public float Distance { get; }
        public bool IsAtWar { get; }
    }
}
