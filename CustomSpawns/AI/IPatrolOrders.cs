using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace CustomSpawns.AI
{
    /**
     * Where a settlement offensive waits while it holds no target, and the order that sends it there.
     */
    internal interface IPatrolOrders
    {
        Settlement? FindClosestNonHostileSettlement(MobileParty party);
        AIBehaviorData CreatePatrolBehavior(MobileParty party, Settlement target);
    }
}
