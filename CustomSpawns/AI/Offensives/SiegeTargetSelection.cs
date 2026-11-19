namespace CustomSpawns.AI.Offensives
{
    public readonly struct SiegeTargetSelection
    {
        public SiegeTargetSelection(string settlementId)
        {
            SettlementId = settlementId;
        }

        public string SettlementId { get; }
    }
}
