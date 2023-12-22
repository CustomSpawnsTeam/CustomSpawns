using System.Linq;
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

            PartyComponent.OnPartyComponentCreatedDelegate initParty = party =>
            {
                if (clan.Leader != null)
                {
                    party.Party.SetCustomOwner(clan.Leader);
                }
                else if (clan.Heroes.Count > 0)
                {
                    party.Party.SetCustomOwner(clan.Heroes.First());
                }

                if (clan.Leader?.HomeSettlement == null)
                {
                    clan.UpdateHomeSettlement(spawnedSettlement);
                }
                party.Party.SetVisualAsDirty();
                party.SetCustomName(partyName);
                party.ActualClan = clan;
                party.SetCustomHomeSettlement(spawnedSettlement);
            };
            return MobileParty.CreateParty(templateObject.StringId + "_" + 1, new CustomPartyComponent(), initParty);
        }
    }
}