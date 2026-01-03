using System.Linq;
using CustomSpawns.Spawn.PartyComponents;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;

namespace CustomSpawns.Spawn
{
    public class CustomPartySpawnFactory : MobilePartySpawnFactory
    {
        protected override MobileParty CreateParty(Settlement spawnedSettlement, Clan clan, PartyTemplateObject templateObject, TextObject partyName)
        {
            Hero leader = clan.Leader; 
            if (leader == null && clan.Heroes.Count > 0)
            {
                leader = clan.Heroes.First();
            }
            
            var partyComponent = new CustomSpawnsPartyComponent(leader!, partyName, spawnedSettlement);
            
            return MobileParty.CreateParty(templateObject.StringId + "_" + 1, partyComponent);
        }
    }
}