using CustomSpawns.Invasions.CapitalObjectives;
using CustomSpawns.AI;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace CustomSpawns.Invasions.Adapters
{
    public sealed class InvasionCapitalAttackOrder : InvasionCapitalOrder
    {
        public InvasionCapitalAttackOrder(IInvasionCapitalObjectiveQuery invasionCapitalObjectiveQuery)
            : base(invasionCapitalObjectiveQuery)
        {
        }

        protected override AiBehavior Behavior => AiBehavior.BesiegeSettlement;
        protected override InvasionCapitalObjectiveKind Kind => InvasionCapitalObjectiveKind.Attack;
    }
}
