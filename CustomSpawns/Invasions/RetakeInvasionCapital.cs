using System;

namespace CustomSpawns.Invasions
{
    public sealed class RetakeInvasionCapital
    {
        private readonly IInvasionCapitalRetakeOrders _orders;

        public RetakeInvasionCapital(IInvasionCapitalRetakeOrders orders)
        {
            _orders = orders ?? throw new ArgumentNullException(nameof(orders));
        }

        public void Handle(InvasionCapitalStatusChanged change)
        {
            if (change.IsHeldByInvader || change.Capital.CapitalSettlementId == null)
            {
                return;
            }

            _orders.Retake(change.Capital);
        }
    }
}
