using System.Collections.Generic;
using System.Linq;
using CustomSpawns.Data.Dao;
using CustomSpawns.Data.Dto;
using CustomSpawns.Spawn.PartyComponents;
using CustomSpawns.Utils;
using TaleWorlds.Core;
using TaleWorlds.Library;
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
        private readonly IPartyBehaviourDao _partyBehaviourDao;
        private readonly MessageBoxService _messageBoxService;
        private readonly ModDebug _modDebug;
        private readonly ISet<string> _clansReportedWithoutLordTemplate = new HashSet<string>();

        public Spawner(BanditPartySpawnFactory banditPartySpawnFactory, CustomPartySpawnFactory customPartySpawnFactory,
            IPartyBehaviourDao partyBehaviourDao, MessageBoxService messageBoxService, ModDebug modDebug)
        {
            _banditPartySpawnFactory = banditPartySpawnFactory;
            _customPartySpawnFactory = customPartySpawnFactory;
            _partyBehaviourDao = partyBehaviourDao;
            _messageBoxService = messageBoxService;
            _modDebug = modDebug;
        }

        public MobileParty SpawnParty(Settlement spawnedSettlement, Clan clan, PartyTemplateObject templateObject,
            TextObject? partyName = null, string? priorityPartyBehaviourTemplateId = null)
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

                MobileParty party = _customPartySpawnFactory.SpawnParty(spawnedSettlement, name, clan, templateObject);
                AssignGeneratedLordWhenPartyBesieges(party, clan, priorityPartyBehaviourTemplateId);
                return party;
            }
            catch (System.Exception e)
            {
                _messageBoxService.ShowMessage(new TextObject("{=SpawnAPIWarn002}Possible invalid spawn data. Spawning of party terminated.").ToString());
                _messageBoxService.ShowCustomSpawnsErrorMessage(e, "party spawning");
                return null;
            }

        }

        /**
         * Bannerlord only lets a party besiege a settlement when it is led by a hero,
         * so a party configured to besiege is given a lord generated on the fly.
         * The party is kept even when the lord cannot be generated, so it still patrols and raids.
         */
        private void AssignGeneratedLordWhenPartyBesieges(MobileParty party, Clan clan,
            string? priorityPartyBehaviourTemplateId)
        {
            if (party.PartyComponent is not CustomSpawnsPartyComponent component
                || party.LeaderHero != null
                || !BesiegesSettlements(priorityPartyBehaviourTemplateId))
            {
                return;
            }

            try
            {
                CharacterObject? lordTemplate = clan.Culture?.LordTemplates
                    .FirstOrDefault(character => character.Occupation == Occupation.Lord);
                if (lordTemplate == null)
                {
                    if (_clansReportedWithoutLordTemplate.Add(clan.StringId))
                    {
                        _messageBoxService.ShowMessage("Custom Spawns cannot generate a lord to lead the besieging "
                                                       + "parties of " + clan.StringId
                                                       + " because their culture has no lord template.");
                    }

                    return;
                }

                Hero lord = HeroCreator.CreateSpecialHero(lordTemplate, null, clan, null, MBRandom.RandomInt(18, 36));
                lord.ChangeState(Hero.CharacterStates.Active);
                component.AssignLeader(lord);
                _modDebug.ShowMessage(new TextObject("{=SpawnAPIDebug002}CustomSpawns: {NAME} now leads the besieging party {PARTY}",
                    new Dictionary<string, object> { { "NAME", lord.Name }, { "PARTY", party.Name } }).ToString(), DebugMessageType.Spawn);
            }
            catch (System.Exception e)
            {
                _messageBoxService.ShowCustomSpawnsErrorMessage(e, "the generation of a lord for a besieging party");
            }
        }

        private bool BesiegesSettlements(string? priorityPartyBehaviourTemplateId)
        {
            if (priorityPartyBehaviourTemplateId == null)
            {
                return false;
            }

            PartyBehaviourDto? partyBehaviour = _partyBehaviourDao.FindById(priorityPartyBehaviourTemplateId);
            return partyBehaviour?.PriorityPartyBehaviours
                .Any(behaviour => behaviour is BesiegeClosestSettlementData) ?? false;
        }
    }
}
