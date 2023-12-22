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
            
            PartyComponent.OnPartyComponentCreatedDelegate initParty = party =>
            {
                if (leader?.HomeSettlement == null)
                {
                    clan.UpdateHomeSettlement(spawnedSettlement); 
                }
                party.Party.SetVisualAsDirty();
                party.Party.SetCustomOwner(leader);
                party.SetCustomName(partyName);
                party.ActualClan = clan;
                party.SetCustomHomeSettlement(spawnedSettlement);
            };

            var partyComponent = new CustomSpawnsPartyComponent(leader!, partyName, spawnedSettlement);
            
            return MobileParty.CreateParty(templateObject.StringId + "_" + 1, partyComponent, initParty);
        }
    }
}