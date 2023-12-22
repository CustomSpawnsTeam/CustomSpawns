using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace CustomSpawns.Spawn.PartySize
{
    /**
     * Observes the size limit of all parties  
     */
    public interface IPartySizeLimitObserver
    {
        /*
         * This method is called when the party size limit has been calculated.
         * @param mobileParty the mobile party for which the party size limit has been calculated
         * @param partySizeLimit the party size limit that has been calculated
         * @return the party size limit that should be used
         */
        ExplainedNumber OnPartySizeLimitCalculated(MobileParty mobileParty, ExplainedNumber partySizeLimit);
    }
}