using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace CustomSpawns.AI
{
    internal interface IRaidClosestVillageContext : ISettlementOffensiveContext
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
