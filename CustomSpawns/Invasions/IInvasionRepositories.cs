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

    public readonly struct InvasionCapitalStatusChanged
    {
        public InvasionCapital Capital { get; }
        public bool IsHeldByInvader { get; }

        public InvasionCapitalStatusChanged(InvasionCapital capital, bool isHeldByInvader)
        {
            Capital = capital;
            IsHeldByInvader = isHeldByInvader;
        }
    }

    public interface IInvasionCapitalRepository
    {
        IReadOnlyList<InvasionCapital> FindAll();
        InvasionCapital? FindByCapital(string settlementId);
        void Register(string invaderKingdomId, string? capitalSettlementId);
    }

    public interface IInvasionClanRepository
    {
        string? FindSpawnTemplate(string clanId);
        IReadOnlyList<string> FindClanIds(string spawnTemplateId);
        void Register(string spawnTemplateId, string clanId);
    }

    public interface IInvasionCapitalRetakeOrders
    {
        void Retake(InvasionCapital capital);
    }
}
