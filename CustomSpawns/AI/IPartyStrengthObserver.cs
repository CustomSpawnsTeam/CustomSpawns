using TaleWorlds.CampaignSystem.Party;

namespace CustomSpawns.AI
{
    /**
     * How strong a party is and how strong the hostiles it can see are. Target strength is left to the
     * offensive-specific ports, because a raid sizes up a village and a siege sizes up a garrison the
     * attacking party is compared against.
     */
    internal interface IPartyStrengthObserver
    {
        float GetAttackerStrength(MobileParty party);
        float GetVisibleHostileStrength(MobileParty party);
    }
}
