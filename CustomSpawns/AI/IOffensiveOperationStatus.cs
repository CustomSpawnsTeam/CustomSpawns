using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace CustomSpawns.AI
{
    /**
     * What the game is already doing with the party, which the phase sequencing reads to know whether it
     * must issue an order at all. Whether the operation itself is running, valid or finished depends on
     * what is being attacked and is left to the offensive-specific ports.
     */
    internal interface IOffensiveOperationStatus
    {
        bool HasNativeContinuation(MobileParty party, Settlement target);
        AiBehavior GetShortTermBehavior(MobileParty party);
    }
}
