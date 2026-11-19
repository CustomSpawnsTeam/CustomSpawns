using CustomSpawns.AI.Offensives;

namespace CustomSpawns.Invasions.CapitalObjectives
{
    // A fief the invasion holds, as a candidate to take over as its capital from the one it lost.
    public readonly struct CapitalSuccessorFacts
    {
        public CapitalSuccessorFacts(string settlementId, SiegeTargetKind kind, bool isCoastalTown,
            float distanceToLostCapital)
        {
            SettlementId = settlementId;
            Kind = kind;
            IsCoastalTown = isCoastalTown;
            DistanceToLostCapital = distanceToLostCapital;
        }

        public string SettlementId { get; }
        public SiegeTargetKind Kind { get; }
        public bool IsCoastalTown { get; }
        public float DistanceToLostCapital { get; }
    }
}
