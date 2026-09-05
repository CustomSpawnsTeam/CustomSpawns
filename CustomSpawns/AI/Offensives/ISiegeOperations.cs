using System.Collections.Generic;
using CustomSpawns.Data.Adapter;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace CustomSpawns.AI.Offensives
{
    /**
     * Everything a besieging offensive needs from the game on top of the roles every offensive uses:
     * picking a settlement it can take, sizing up its defenders, and following the siege it started.
     */
    internal interface ISiegeOperations : IPartyStrengthObserver, IOffensiveOperationStatus, IPatrolOrders
    {
        Settlement? FindClosestSiegeTarget(MobileParty party, float attackerStrength,
            float minimumTargetRatio, float minimumRoamingHostilesRatio,
            IReadOnlyList<SpawnSettlementType> targetTypes);
        Settlement? FindAlternativeSiegeTarget(MobileParty party, Settlement excludedTarget,
            float attackerStrength, float minimumTargetRatio, float minimumRoamingHostilesRatio,
            IReadOnlyList<SpawnSettlementType> targetTypes);
        float GetTargetStrength(MobileParty party, Settlement target);
        float GetHostileLordStrengthNearTarget(MobileParty party, Settlement target);
        IFaction GetTargetMapFaction(Settlement target);
        bool IsMatchingSiegeActive(MobileParty party, Settlement target);
        bool IsMatchingAssaultActive(MobileParty party, Settlement target);
        bool IsCurrentTargetValid(MobileParty party, Settlement target, bool matchingOperationActive,
            IFaction? intendedTargetFaction);
        bool IsTargetComplete(MobileParty party, Settlement target);
        AIBehaviorData CreateSiegeBehavior(MobileParty party, Settlement target);
    }
}
