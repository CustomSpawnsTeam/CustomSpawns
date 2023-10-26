using CustomSpawns.Utils;
using TaleWorlds.Localization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace CustomSpawns.Spawn
{
    public class Spawner
    {
        private readonly BanditPartySpawnFactory _banditPartySpawnFactory;
        private readonly CustomPartySpawnFactory _customPartySpawnFactory;
        private readonly MessageBoxService _messageBoxService;
        private readonly ModDebug _modDebug;

        public Spawner(BanditPartySpawnFactory banditPartySpawnFactory, CustomPartySpawnFactory customPartySpawnFactory,
            MessageBoxService messageBoxService, ModDebug modDebug)
        {
            _banditPartySpawnFactory = banditPartySpawnFactory;
            _customPartySpawnFactory = customPartySpawnFactory;
            _messageBoxService = messageBoxService;
            _modDebug = modDebug;
        }

        // TODO use the speed parameter here instead of using the harmony patch  
        public MobileParty SpawnParty(Settlement spawnedSettlement, Clan clan, PartyTemplateObject templateObject,
            float speed=0f, TextObject partyName = null)
        {
            try
            {
                //get name and show message.
                TextObject name = partyName ?? clan.Name;
                _modDebug.ShowMessage(new TextObject("{=SpawnAPIDebug001}CustomSpawns: Spawning {NAME} at {POSITION} in settlement {SETTLEMENT}", new System.Collections.Generic.Dictionary<string, object> { { "NAME", name}, { "POSITION", spawnedSettlement.GatePosition}, { "SETTLEMENT", spawnedSettlement.Name} }).ToString(), DebugMessageType.Spawn);

                if (clan.IsBanditFaction)
                {
                    return _banditPartySpawnFactory.SpawnParty(spawnedSettlement, name, clan, templateObject);
                }
                return _customPartySpawnFactory.SpawnParty(spawnedSettlement, name, clan, templateObject);
            }
            catch (System.Exception e)
            {
                _messageBoxService.ShowMessage(new TextObject("{=SpawnAPIWarn002}Possible invalid spawn data. Spawning of party terminated.").ToString());
                _messageBoxService.ShowCustomSpawnsErrorMessage(e, "party spawning");
                return null;
            }

        }
    }
}
