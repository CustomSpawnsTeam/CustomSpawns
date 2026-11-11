namespace CustomSpawns.Invasions
{
    // A coastal town a follower could land in, its kingdom, and how far it lies from the nearest fief of its allies.
    public readonly struct InvasionLandingSite
    {
        public InvasionLandingSite(string settlementId, string kingdomId, float distanceFromAllies)
        {
            SettlementId = settlementId;
            KingdomId = kingdomId;
            DistanceFromAllies = distanceFromAllies;
        }

        public string SettlementId { get; }
        public string KingdomId { get; }
        public float DistanceFromAllies { get; }
    }
}
