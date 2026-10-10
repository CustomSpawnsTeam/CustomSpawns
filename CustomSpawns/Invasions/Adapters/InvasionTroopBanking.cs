using System.Linq;
using CustomSpawns.Data.Dao;
using CustomSpawns.Data.Dto;
using Helpers;
using NLog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace CustomSpawns.Invasions.Adapters
{
    /**
     * What a reinforcement wave is: men paid into the capital's garrison, and lords who come to take them
     * out again. Nothing marches in, and the garrison takes the wave whole however large it has grown --
     * the strength ceiling is what decides whether a wave comes at all.
     *
     * The lords are only fielded here. Their men come from the garrison draw that vanilla's own party
     * creation triggers, which serves a lord arriving with the wave and a beaten lord come back alike.
     */
    internal sealed class InvasionTroopBanking
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly SpawnDao _spawnDao;
        private readonly InvaderHeroCreator _invaderHeroes;

        internal InvasionTroopBanking(SpawnDao spawns, InvaderHeroCreator invaderHeroes)
        {
            _spawnDao = spawns;
            _invaderHeroes = invaderHeroes;
        }

        // Whether the wave was paid in, which is what decides whether it counts against the rule's allowance.
        internal bool Bank(InvasionReinforcementDraw draw)
        {
            InvasionReinforcementRuleData? rule = _spawnDao.FindByPartyTemplateId(draw.Invasion.SpawnTemplateId)
                ?.Invasion?.ReinforcementRules.FirstOrDefault(candidate => candidate.Id == draw.RuleId);
            Town? town = Settlement.Find(draw.Invasion.CapitalSettlementId!)?.Town;
            PartyTemplateObject? template = rule == null
                ? null
                : Campaign.Current.ObjectManager.GetObject<PartyTemplateObject>(rule.PartyTemplateId);

            if (rule == null || template == null || town?.GarrisonParty == null)
            {
                Logger.Info("Invasion reinforcement slot skipped: invasion=" + draw.Invasion.ClanId +
                            " rule=" + draw.RuleId + " reason=template_or_capital_changed");
                return false;
            }

            int banked = PayIntoGarrison(town, template);
            int lords = FieldLords(draw.Invasion, town.Settlement, rule.LordsPerWave);
            Logger.Info("Invasion reinforcement banked: invasion=" + draw.Invasion.ClanId + " rule=" +
                        draw.RuleId + " town=" + town.Settlement.StringId + " men=" + banked + " lords=" + lords +
                        " garrison=" + town.GarrisonParty.MemberRoster.TotalManCount + " next_arrival_day=" +
                        draw.Invasion.FindReinforcementSchedule(draw.RuleId)?.NextDeadlineDays);
            return true;
        }

        /**
         * The wave is rolled off the rule's own party template, one roll per kind of troop in it between
         * the numbers the template declares, and paid straight into the garrison. Heroes in a template
         * are skipped: the lords of a wave are fielded as lords, not banked as men.
         */
        private static int PayIntoGarrison(Town town, PartyTemplateObject template)
        {
            TroopRoster garrison = town.GarrisonParty.MemberRoster;
            int banked = 0;

            foreach (PartyTemplateStack stack in template.Stacks.Where(stack => stack.Character is { IsHero: false }))
            {
                int number = MBRandom.RandomInt(stack.MinValue, stack.MaxValue + 1);
                garrison.AddToCounts(stack.Character, number);
                banked += number;
            }

            return banked;
        }

        private int FieldLords(Invasion invasion, Settlement capital, int lordsPerWave)
        {
            int fielded = 0;

            for (int index = 0; index < lordsPerWave; index++)
            {
                Hero? lord = InvaderHeroCreator.CreateLord(_invaderHeroes.FindClanWithRoom(invasion, capital), capital);

                if (lord == null)
                {
                    break;
                }

                MobilePartyHelper.SpawnLordParty(lord, capital);
                fielded++;
            }

            return fielded;
        }
    }
}
