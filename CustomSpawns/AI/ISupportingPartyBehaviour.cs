using CustomSpawns.Spawn;
using TaleWorlds.CampaignSystem.Party;

namespace CustomSpawns.AI
{
    /**
     * Behaviour of the parties spawned alongside a main party. It replaces every behaviour configured for
     * their spawn and is not part of the party behaviour XML API, so a submod cannot select it.
     */
    public interface ISupportingPartyBehaviour
    {
        PartyBehaviourResult GetPriorityBehaviour(MobileParty party, CsPartyData partyData);
    }
}
