using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace CustomSpawns.AI.Offensives
{
    /**
     * What the campaign map answers about reaching a settlement: whether a party can march to its gate
     * overland. A party that cannot cross water asks this to rule out the towns it can only reach by sea.
     */
    internal interface IMapPathQuery
    {
        bool HasReachableLandGate(MobileParty party, Settlement target);
    }
}
