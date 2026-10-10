using System.Collections.Generic;

namespace CustomSpawns.Invasions
{
    internal readonly struct InvasionCapital
    {
        public string InvaderKingdomId { get; }
        public string? CapitalSettlementId { get; }

        public InvasionCapital(string invaderKingdomId, string? capitalSettlementId)
        {
            InvaderKingdomId = invaderKingdomId;
            CapitalSettlementId = capitalSettlementId;
        }
    }

    internal readonly struct InvasionCapitalStatusChanged
    {
        public InvasionCapital Capital { get; }
        public bool IsHeldByInvader { get; }

        public InvasionCapitalStatusChanged(InvasionCapital capital, bool isHeldByInvader)
        {
            Capital = capital;
            IsHeldByInvader = isHeldByInvader;
        }
    }

    internal interface IInvasionCapitalRepository
    {
        IReadOnlyList<InvasionCapital> FindAll();
        InvasionCapital? FindByCapital(string settlementId);
        void Register(string invaderKingdomId, string? capitalSettlementId);
    }

    internal interface IInvasionClanRepository
    {
        string? FindSpawnTemplate(string clanId);
        IReadOnlyList<string> FindClanIds(string spawnTemplateId);
        void Register(string spawnTemplateId, string clanId);
    }

    internal interface IInvasionCapitalRetakeOrders
    {
        void Retake(InvasionCapital capital);
    }
}
