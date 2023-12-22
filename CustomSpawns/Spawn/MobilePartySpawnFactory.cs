using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;

namespace CustomSpawns.Spawn
{
    public abstract class MobilePartySpawnFactory : IMobilePartySpawn
    {
        protected abstract MobileParty CreateParty(Settlement spawnedSettlement, Clan clan, PartyTemplateObject templateObject, TextObject partyName);

        public MobileParty SpawnParty(Settlement homeSettlement, TextObject partyName, Clan clan, PartyTemplateObject partyTemplate)
        {
            MobileParty mobileParty = CreateParty(homeSettlement, clan, partyTemplate, partyName ?? clan.Name);
            mobileParty.InitializeMobilePartyAroundPosition(partyTemplate, homeSettlement.GatePosition, 10f);
            return mobileParty;
        }

    }
}