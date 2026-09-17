using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace CustomSpawns.AI.Offensives
{
    /**
     * What the campaign map answers about reaching a settlement: whether a party can march to its gate
     * overland, and how far that gate is. An invasion landing asks this to rule out the towns it can
     * only reach by sea, and to rank the ones it can.
     */
    internal interface IMapPathQuery
    {
        bool HasReachableLandGate(MobileParty party, Settlement target);

        float GetDistanceToGate(MobileParty party, Settlement settlement);
    }
}
