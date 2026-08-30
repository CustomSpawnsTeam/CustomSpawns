using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace CustomSpawns.AI
{
    /**
     * The lookups a settlement offensive needs whether it raids or besieges. Everything that depends on
     * what is being attacked is left to the behaviour-specific contexts.
     */
    internal interface ISettlementOffensiveContext
    {
        float GetAttackerStrength(MobileParty party);
        float GetVisibleHostileStrength(MobileParty party);
        Settlement? FindClosestNonHostileSettlement(MobileParty party);
        bool HasNativeContinuation(MobileParty party, Settlement target);
        AiBehavior GetShortTermBehavior(MobileParty party);
        AIBehaviorData CreatePatrolBehavior(MobileParty party, Settlement target);
    }
}
