using System.Collections.Generic;
using CustomSpawns.Data.Adapter;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace CustomSpawns.AI
{
    internal interface IBesiegeClosestSettlementContext : ISettlementOffensiveContext
    {
        Settlement? FindClosestSiegeTarget(MobileParty party, float attackerStrength,
            float minimumRatio, IReadOnlyList<SpawnSettlementType> targetTypes);
        float GetTargetStrength(MobileParty party, Settlement target);
        IFaction GetTargetMapFaction(Settlement target);
        bool IsMatchingSiegeActive(MobileParty party, Settlement target);
        bool IsMatchingAssaultActive(MobileParty party, Settlement target);
        bool IsCurrentTargetValid(MobileParty party, Settlement target, bool matchingOperationActive,
            IFaction? intendedTargetFaction);
        bool IsTargetComplete(MobileParty party, Settlement target);
        AIBehaviorData CreateSiegeBehavior(MobileParty party, Settlement target);
    }
}
