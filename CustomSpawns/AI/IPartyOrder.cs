using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace CustomSpawns.AI
{
    public interface IPartyOrder
    {
        AIBehaviorData? Find(MobileParty party);
    }
}
