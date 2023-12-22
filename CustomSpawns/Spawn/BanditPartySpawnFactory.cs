using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;

namespace CustomSpawns.Spawn
{
    public class BanditPartySpawnFactory : MobilePartySpawnFactory
    {
        protected override MobileParty CreateParty(Settlement spawnedSettlement, Clan clan,
            PartyTemplateObject templateObject, TextObject partyName)
        {
            MobileParty party = BanditPartyComponent.CreateBanditParty(templateObject.StringId + "_" + 1, clan,
                spawnedSettlement.Hideout, false);
            return InitParty(party, partyName ?? clan.Name, spawnedSettlement, clan);
        }

        private MobileParty InitParty(MobileParty mobileParty, TextObject partyName, Settlement homeSettlement, Clan clan)
        {
            if (clan.Leader != null)
            {
                mobileParty.Party.SetCustomOwner(clan.Leader);
            }
            else if (clan.Heroes.Count > 0)
            {
                mobileParty.Party.SetCustomOwner(clan.Heroes.First());
            }

            if (clan.Leader?.HomeSettlement == null)
            {
                clan.UpdateHomeSettlement(homeSettlement);
            }
            mobileParty.Party.SetVisualAsDirty();
            mobileParty.SetCustomName(partyName);
            mobileParty.ActualClan = clan;
            mobileParty.SetCustomHomeSettlement(homeSettlement);

            return mobileParty;
        }
    }
}