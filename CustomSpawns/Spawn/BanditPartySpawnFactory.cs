using System.Collections.Generic;
using System.Linq;
using CustomSpawns.Utils;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Map;
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
            string partyId = templateObject.StringId + "_" + 1;
            Hideout? hideout = GetHomeHideout(spawnedSettlement, clan);
            // The native bandit counts key on the party's home settlement, so a party with no hideout has to be
            // homed on a settlement the way looters are.
            MobileParty party = hideout == null
                ? BanditPartyComponent.CreateLooterParty(partyId, clan, spawnedSettlement, false, templateObject,
                    spawnedSettlement.Position)
                : BanditPartyComponent.CreateBanditParty(partyId, clan, hideout, false, templateObject,
                    spawnedSettlement.Position);
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
                clan.SetInitialHomeSettlement(homeSettlement);
            }
            mobileParty.Party.SetVisualAsDirty();
            mobileParty.Party.SetCustomName(partyName);
            mobileParty.ActualClan = clan;
            mobileParty.SetCustomHomeSettlement(homeSettlement);

            return mobileParty;
        }

        private Hideout? GetHomeHideout(Settlement spawnedSettlement, Clan clan)
        {
            if (spawnedSettlement.IsHideout)
            {
                return spawnedSettlement.Hideout;
            }
            // Non-native bandit clans might not have any hideouts on the map, and a native clan has none until one
            // is infested.
            return GetNearestHideoutFromSettlement(spawnedSettlement, clan);
        }

        private static Hideout? GetNearestHideoutFromSettlement(Settlement spawnedSettlement, Clan clan)
        {
            return CampaignUtils.GetNearestSettlement(
                Hideout.All.Where(hideout => hideout.MapFaction == clan).Select(hideout => hideout.Settlement).ToList(),
                new List<IMapPoint>() { spawnedSettlement })?.Hideout;
        }
    }
}