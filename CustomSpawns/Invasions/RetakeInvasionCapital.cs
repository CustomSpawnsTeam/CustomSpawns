using System;

namespace CustomSpawns.Invasions
{
    internal sealed class RetakeInvasionCapital
    {
        private readonly IInvasionCapitalRetakeOrders _orders;

        internal RetakeInvasionCapital(IInvasionCapitalRetakeOrders orders)
        {
            _orders = orders ?? throw new ArgumentNullException(nameof(orders));
        }

        internal void Handle(InvasionCapitalStatusChanged change)
        {
            if (change.IsHeldByInvader || change.Capital.CapitalSettlementId == null)
            {
                return;
            }

            _orders.Retake(change.Capital);
        }
    }
}
