using System.Collections.Generic;
using System.Linq;
using CustomSpawns.Data.Dao;
using CustomSpawns.Data.Dto;
using CustomSpawns.Spawn.PartyComponents;
using CustomSpawns.Utils;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace CustomSpawns.Spawn
{
    /**
     * Bannerlord only lets a party besiege a settlement when it is led by a hero,
     * so a party configured to besiege is given a lord generated on the fly.
     * The party is kept even when the lord cannot be generated, so it still patrols and raids.
     */
    public class BesiegingPartyLordResolver
    {
        private readonly IPartyBehaviourDao _partyBehaviourDao;
        private readonly MessageBoxService _messageBoxService;
        private readonly ModDebug _modDebug;
        private readonly ISet<string> _clansReportedWithoutLordTemplate = new HashSet<string>();

        public BesiegingPartyLordResolver(IPartyBehaviourDao partyBehaviourDao,
            MessageBoxService messageBoxService, ModDebug modDebug)
        {
            _partyBehaviourDao = partyBehaviourDao;
            _messageBoxService = messageBoxService;
            _modDebug = modDebug;
        }

        public void AssignLordWhenPartyBesieges(MobileParty party, Clan clan,
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
                CharacterObject? lordTemplate = FindLordTemplate(clan);
                if (lordTemplate == null)
                {
                    ReportMissingLordTemplateOnce(clan);
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

        private static CharacterObject? FindLordTemplate(Clan clan)
        {
            return clan.Culture?.LordTemplates
                .FirstOrDefault(character => character.Occupation == Occupation.Lord);
        }

        private void ReportMissingLordTemplateOnce(Clan clan)
        {
            if (_clansReportedWithoutLordTemplate.Add(clan.StringId))
            {
                _messageBoxService.ShowMessage("Custom Spawns cannot generate a lord to lead the besieging "
                                               + "parties of " + clan.StringId
                                               + " because their culture has no lord template.");
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
