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
    public interface ISiegeOperations : IPartyStrengthObserver, IPatrolOrders
    {
        Settlement? FindClosestSiegeTarget(MobileParty party, float attackerStrength,
            float minimumTargetRatio, float minimumRoamingHostilesRatio,
            IReadOnlyList<SpawnSettlementType> targetTypes);
        Settlement? FindAlternativeSiegeTarget(MobileParty party, Settlement excludedTarget,
            float attackerStrength, float minimumTargetRatio, float minimumRoamingHostilesRatio,
            IReadOnlyList<SpawnSettlementType> targetTypes);
        float GetTargetStrength(MobileParty party, Settlement target);
        float GetActiveSiegeDefenderStrength(MobileParty party, Settlement target);
        float GetHostileLordStrengthNearTarget(MobileParty party, Settlement target);

        // Whether this party masses on its objective instead of only taking one it can already carry.
        bool GathersBeforeAssault(MobileParty party);

        // Whether a siege is already running against the target that this party may march to support.
        bool IsSupportableSiegeActive(MobileParty party, Settlement target);

        // The strength of the attacking force that has arrived at the target, ignoring those still marching.
        float GetArrivedAttackerStrength(MobileParty party, Settlement target);

        // What the party brings by itself, without the siblings that share its objective: what is with it
        // rather than what is committed to it.
        float GetOwnAttackerStrength(MobileParty party);
        IFaction GetTargetMapFaction(Settlement target);
        bool IsMatchingSiegeActive(MobileParty party, Settlement target);
        bool IsMatchingAssaultActive(MobileParty party, Settlement target);

        // Whether anybody in the attacking force is already committed to a battle at the target, whatever
        // that battle is: a sally out of the town it besieges and a field fight against a relief force are
        // neither of them assaults. The whole force is asked, not only this party: a fight that engages
        // some of a camp is a fight the rest of it is part of.
        bool IsForcePinnedInBattle(MobileParty party, Settlement target);
        bool IsCurrentTargetValid(MobileParty party, Settlement target, bool matchingOperationActive,
            IFaction? intendedTargetFaction);
        bool IsTargetComplete(MobileParty party, Settlement target);
        AIBehaviorData CreateSiegeBehavior(MobileParty party, Settlement target);
        AIBehaviorData? AbandonSiegeAndCreateRetreatBehavior(MobileParty party, Settlement target);
    }
}
