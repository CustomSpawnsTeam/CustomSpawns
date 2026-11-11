namespace CustomSpawns.Invasions
{
    // A coastal town a follower could land in, its kingdom, and how far it lies from the nearest fief of the line's first realm.
    public readonly struct InvasionLandingSite
    {
        public InvasionLandingSite(string settlementId, string kingdomId, float distanceFromFirstRealm)
        {
            SettlementId = settlementId;
            KingdomId = kingdomId;
            DistanceFromFirstRealm = distanceFromFirstRealm;
        }

        public string SettlementId { get; }
        public string KingdomId { get; }
        public float DistanceFromFirstRealm { get; }
    }
}
