using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements;

namespace CustomSpawns.Invasions.Adapters
{
    internal sealed class InvasionCapitalOwnershipBehaviour : CampaignBehaviorBase
    {
        private readonly IInvasionCapitalRepository _capitals;
        internal event Action<InvasionCapitalStatusChanged>? CapitalStatusChanged;

        internal InvasionCapitalOwnershipBehaviour(IInvasionCapitalRepository capitals)
        {
            _capitals = capitals ?? throw new ArgumentNullException(nameof(capitals));
        }

        public override void RegisterEvents()
        {
            CampaignEvents.OnSettlementOwnerChangedEvent.AddNonSerializedListener(this, OnOwnerChanged);
        }

        public override void SyncData(IDataStore dataStore)
        {
        }

        private void OnOwnerChanged(Settlement settlement, bool openToClaim, Hero newOwner, Hero oldOwner,
            Hero capturer, ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail detail)
        {
            ObserveOwnerChange(settlement.StringId, KingdomIdOf(oldOwner), KingdomIdOf(newOwner));
        }

        internal void ObserveOwnerChange(string settlementId, string? oldOwnerKingdomId,
            string? newOwnerKingdomId)
        {
            InvasionCapital? capital = _capitals.FindByCapital(settlementId);
            if (capital == null) return;

            bool wasHeld = string.Equals(capital.Value.InvaderKingdomId, oldOwnerKingdomId,
                StringComparison.Ordinal);
            bool isHeld = string.Equals(capital.Value.InvaderKingdomId, newOwnerKingdomId,
                StringComparison.Ordinal);
            if (wasHeld == isHeld) return;

            CapitalStatusChanged?.Invoke(new InvasionCapitalStatusChanged(capital.Value, isHeld));
        }

        private static string? KingdomIdOf(Hero? owner) => owner?.Clan?.Kingdom?.StringId;
    }
}
