using System.Collections.Generic;

namespace CustomSpawns.Invasions
{
    public readonly struct InvasionCapital
    {
        public string InvaderKingdomId { get; }
        public string? CapitalSettlementId { get; }

        public InvasionCapital(string invaderKingdomId, string? capitalSettlementId)
        {
            InvaderKingdomId = invaderKingdomId;
            CapitalSettlementId = capitalSettlementId;
        }
    }

    public interface IInvasionCapitalRepository
    {
        IReadOnlyList<InvasionCapital> FindAll();
        void Register(string invaderKingdomId, string? capitalSettlementId);
        void RelocateAfterObjectiveCapture(string invaderKingdomId, string capitalSettlementId);
    }

    public interface IInvasionClanRepository
    {
        string? FindSpawnTemplate(string clanId);
        IReadOnlyList<string> FindClanIds(string spawnTemplateId);
        void Register(string spawnTemplateId, string clanId);
    }
}
