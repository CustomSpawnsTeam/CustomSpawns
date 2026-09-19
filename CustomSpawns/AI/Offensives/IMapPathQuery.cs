using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace CustomSpawns.AI.Offensives
{
    /**
     * What the campaign map answers about reaching a settlement: whether a party can march to its gate
     * overland, and how far that gate is. A party that came ashore asks this to rule out the towns it
     * can only reach by sea, and to rank the ones it can reach on foot.
     */
    internal interface IMapPathQuery
    {
        bool HasReachableLandGate(MobileParty party, Settlement target);

        float GetDistanceToGate(MobileParty party, Settlement settlement);
    }
}
