namespace CustomSpawns.AI.Offensives
{
    public readonly struct SiegeTargetFacts
    {
        public SiegeTargetFacts(string settlementId, SiegeTargetKind kind, bool isActive,
            bool isHostile, bool hasCompatibleOrJoinableSiege, bool matchesStrategicRequirement,
            bool isReachable, float distance, float defenderStrength, float nearbyHostileStrength,
            bool isPreferred = false)
        {
            IsPreferred = isPreferred;
            SettlementId = settlementId;
            Kind = kind;
            IsActive = isActive;
            IsHostile = isHostile;
            HasCompatibleOrJoinableSiege = hasCompatibleOrJoinableSiege;
            MatchesStrategicRequirement = matchesStrategicRequirement;
            IsReachable = isReachable;
            Distance = distance;
            DefenderStrength = defenderStrength;
            NearbyHostileStrength = nearbyHostileStrength;
        }

        public string SettlementId { get; }
        public SiegeTargetKind Kind { get; }
        public bool IsActive { get; }
        public bool IsHostile { get; }
        public bool HasCompatibleOrJoinableSiege { get; }
        public bool MatchesStrategicRequirement { get; }
        public bool IsReachable { get; }
        public float Distance { get; }
        public float DefenderStrength { get; }
        public float NearbyHostileStrength { get; }

        // Chosen over any target that is not, however much closer that one lies, as long as it is eligible.
        public bool IsPreferred { get; }
    }
}
