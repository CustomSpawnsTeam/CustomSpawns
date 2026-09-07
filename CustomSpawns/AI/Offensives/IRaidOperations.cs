using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace CustomSpawns.AI.Offensives
{
    /**
     * Everything a raiding offensive needs from the game on top of the roles every offensive uses:
     * picking a village it can raid, sizing up its militia, and following the raid it started.
     */
    internal interface IRaidOperations : IPartyStrengthObserver, IPatrolOrders
    {
        Settlement? FindClosestRaidTarget(MobileParty party, float attackerStrength,
            float minimumRatio);
        float GetTargetStrength(Settlement target);
        bool IsMatchingRaidActive(MobileParty party, Settlement target);
        bool IsCurrentTargetValid(MobileParty party, Settlement target, bool matchingRaidActive);
        bool IsTargetComplete(Settlement target);
        AIBehaviorData CreateRaidBehavior(MobileParty party, Settlement target);
    }
}
