namespace CustomSpawns.Invasions.CapitalObjectives
{
    public readonly struct InvasionCapitalObjectiveDirective
    {
        public InvasionCapitalObjectiveDirective(string kingdomId, string commanderPartyId,
            string targetSettlementId, InvasionCapitalObjectiveKind kind,
            InvasionCapitalObjectivePhase phase)
        {
            KingdomId = kingdomId;
            CommanderPartyId = commanderPartyId;
            TargetSettlementId = targetSettlementId;
            Kind = kind;
            Phase = phase;
        }

        public string KingdomId { get; }
        public string CommanderPartyId { get; }
        public string TargetSettlementId { get; }
        public InvasionCapitalObjectiveKind Kind { get; }
        public InvasionCapitalObjectivePhase Phase { get; }
    }

    public interface IInvasionCapitalObjectiveQuery
    {
        InvasionCapitalObjectiveDirective? FindForKingdom(string kingdomId);
    }
}
