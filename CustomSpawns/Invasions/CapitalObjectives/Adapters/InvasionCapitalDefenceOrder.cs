using CustomSpawns.AI;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace CustomSpawns.Invasions.CapitalObjectives.Adapters
{
    public sealed class InvasionCapitalDefenceOrder : InvasionCapitalOrder
    {
        public InvasionCapitalDefenceOrder(IInvasionCapitalObjectiveQuery invasionCapitalObjectiveQuery)
            : base(invasionCapitalObjectiveQuery)
        {
        }

        protected override AiBehavior Behavior => AiBehavior.DefendSettlement;
        protected override InvasionCapitalObjectiveKind Kind =>
            InvasionCapitalObjectiveKind.Defence;

        protected override bool IsTargetingPort(MobileParty party, Settlement target) =>
            target.HasPort && party.IsCurrentlyAtSea;
    }
}
