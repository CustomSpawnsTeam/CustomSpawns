using System;
using System.Collections.Generic;
using System.Linq;
using CustomSpawns.Data.Dao;
using CustomSpawns.Data.Dto;
using CustomSpawns.Spawn;
using NLog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace CustomSpawns.Invasions.Adapters
{
    /**
     * Everything that keeps an invasion arriving: when the next party is due, which of its clans can
     * take one, and where it lands. The campaign behaviour only forwards the events that move the
     * clock; the schedule itself belongs to InvasionReinforcements.
     */
    internal sealed class InvasionReinforcementArrivals : IInvasionReinforcementQuery
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly InvasionState _invasionState;
        private readonly SpawnDao _spawnDao;
        private readonly DynamicSpawnData _dynamicSpawnData;
        private readonly SpawnGroupService _spawnGroupService;
        private readonly InvasionSupplyBases _invasionSupplyBases;
        private readonly InvasionReinforcements _invasionReinforcements;

        internal InvasionReinforcementArrivals(InvasionState state, SpawnDao spawns, DynamicSpawnData parties,
            SpawnGroupService spawnGroups, InvasionSupplyBases supplyBases, Func<int, int> randomIndex)
        {
            _invasionState = state;
            _spawnDao = spawns;
            _dynamicSpawnData = parties;
            _spawnGroupService = spawnGroups;
            _invasionSupplyBases = supplyBases;
            _invasionReinforcements = new InvasionReinforcements(state, this, randomIndex);
        }

        internal void StartSchedule(long captureId)
        {
            Invasion? invasion = _invasionReinforcements.StartSchedule(captureId);

            if (invasion == null)
            {
                return;
            }

            Logger.Info("Invasion coastal foothold gained: invasion=" + invasion.ClanId + " town=" +
                        _invasionState.FindCapture(captureId)!.SettlementId + " next_arrival_days=" +
                        string.Join(",", invasion.ReinforcementSchedules.Select(schedule =>
                            schedule.RuleId + "@" + schedule.NextDeadlineDays)));
        }

        internal void LandDue()
        {
            foreach (InvasionReinforcementDraw draw in _invasionReinforcements.DrawDue())
            {
                if (draw.Outcome == InvasionReinforcementOutcome.Landing)
                {
                    Land(draw);
                }
                else
                {
                    Logger.Info("Invasion reinforcement slot skipped: invasion=" + draw.Invasion.ClanId +
                                " rule=" + draw.RuleId + " reason=" + SkipReason(draw.Outcome));
                }
            }
        }

        private static string SkipReason(InvasionReinforcementOutcome outcome) =>
            outcome == InvasionReinforcementOutcome.MaxConcurrentPartiesReached ? "max_concurrent" :
            outcome == InvasionReinforcementOutcome.FactionStrengthReached ? "faction_strength" :
            outcome == InvasionReinforcementOutcome.NoLandingSite ? "no_held_port" : "configuration_or_clan";

        private void Land(InvasionReinforcementDraw draw)
        {
            InvasionReinforcementRuleData? rule = FindRule(draw.Invasion, draw.RuleId);
            SpawnDto? spawn = _spawnDao.FindByPartyTemplateId(draw.Invasion.SpawnTemplateId);
            Settlement? landing = Settlement.Find(draw.LandingSettlementId!);
            PartyTemplateObject? template = rule == null ? null :
                Campaign.Current.ObjectManager.GetObject<PartyTemplateObject>(rule.PartyTemplateId);

            if (spawn == null || template == null || landing == null ||
                !IsEligibleLandingSettlement(landing, draw.Invasion, spawn.Invasion))
            {
                Logger.Info("Invasion reinforcement slot skipped: invasion=" + draw.Invasion.ClanId +
                            " rule=" + draw.RuleId + " reason=template_or_port_changed");
                return;
            }

            Clan? clan = draw.ClanId == null
                ? FoundClan(draw.Invasion, landing)
                : Clan.FindFirst(candidate => candidate.StringId == draw.ClanId);

            if (clan == null)
            {
                Logger.Info("Invasion reinforcement slot skipped: invasion=" + draw.Invasion.ClanId +
                            " rule=" + draw.RuleId + " reason=no_clan_to_take_the_party");
                return;
            }

            MobileParty? reinforcement = _spawnGroupService.SpawnInvasionReinforcement(spawn, template, landing, clan,
                draw.RuleId);
            int landed = reinforcement == null ? 0 : TrimToPermittedStrength(reinforcement, draw.PermittedStrength);
            Logger.Info("Invasion reinforcement " + (reinforcement == null ? "failed" : "arrived") + ": invasion=" +
                        draw.Invasion.ClanId + " rule=" + draw.RuleId + " clan=" + clan.StringId + " town=" +
                        landing.StringId + " men=" + landed + " next_arrival_day=" +
                        draw.Invasion.FindReinforcementSchedule(draw.RuleId)?.NextDeadlineDays);
        }

        /**
         * A party worth more than the invasion has room for lands smaller rather than not at all. How much
         * of it is allowed is the engine's answer; this only takes the men off the roster, from the back,
         * leaving the heroes alone because a party without its leader has nobody to march it.
         */
        private static int TrimToPermittedStrength(MobileParty reinforcement, double permittedStrength)
        {
            TroopRoster roster = reinforcement.MemberRoster;
            int permitted = InvasionReinforcements.PermittedMemberCount(roster.TotalManCount,
                reinforcement.Party.EstimatedStrength, permittedStrength);

            for (int index = roster.Count - 1; index >= 0 && roster.TotalManCount > permitted; index--)
            {
                TroopRosterElement element = roster.GetElementCopyAtIndex(index);

                if (!element.Character.IsHero)
                {
                    roster.RemoveTroop(element.Character,
                        Math.Min(element.Number, roster.TotalManCount - permitted));
                }
            }

            return roster.TotalManCount;
        }

        /**
         * Vanilla caps a clan's war parties by tier, so an invasion that outgrows its clans expands into
         * another one instead of into a single oversized clan: its kingdom then distributes fiefs across
         * all of them, and fields a party for each lord that has none, the way it does for any kingdom of
         * its own. A lord beaten in the field is no longer among them: it is removed rather than fielded
         * again.
         */
        private static Clan? FoundClan(Invasion invasion, Settlement landing)
        {
            Clan? founder = Clan.FindFirst(candidate => candidate.StringId == invasion.ClanId);
            Kingdom? kingdom = founder?.Kingdom;
            CharacterObject? lordTemplate = founder?.Culture?.LordTemplates
                .FirstOrDefault(character => character.Occupation == Occupation.Lord);

            if (founder == null || kingdom == null || lordTemplate == null)
            {
                return null;
            }

            Clan clan = Clan.CreateClan(founder.StringId + "_" + invasion.ClanIds.Count);
            TextObject name = NameGenerator.Current.GenerateClanName(founder.Culture, landing);
            clan.ChangeClanName(name, name);
            clan.Culture = founder.Culture;
            clan.Banner = Banner.CreateRandomClanBanner();
            clan.Color = founder.Color;
            clan.Color2 = founder.Color2;
            clan.IsNoble = true;
            clan.SetInitialHomeSettlement(landing);

            // Tier has no public setter; renown is the way in, and it carries the same war party limit.
            clan.AddRenown(Campaign.Current.Models.ClanTierModel.GetRequiredRenownForTier(founder.Tier), false);

            Hero leader = HeroCreator.CreateSpecialHero(lordTemplate, landing, clan, null, MBRandom.RandomInt(18, 36));
            leader.ChangeState(Hero.CharacterStates.Active);
            clan.SetLeader(leader);
            EnterSettlementAction.ApplyForCharacterOnly(leader, landing);
            ChangeKingdomAction.ApplyByJoinToKingdom(clan, kingdom, default, false);
            CampaignEventDispatcher.Instance.OnClanCreated(clan, false);
            invasion.AddClan(clan.StringId);
            Logger.Info("Invasion clan founded: invasion=" + invasion.ClanId + " clan=" + clan.StringId +
                        " kingdom=" + kingdom.StringId);
            return clan;
        }

        double IInvasionReinforcementQuery.NowInDays => CampaignTime.Now.ToDays;

        IReadOnlyList<InvasionReinforcementRule>? IInvasionReinforcementQuery.GetRules(Invasion invasion)
        {
            InvasionData? configuration = GetConfiguration(invasion);
            Clan? clan = Clan.FindFirst(candidate => candidate.StringId == invasion.ClanId);

            return configuration == null || clan?.IsEliminated != false
                ? null
                : configuration.ReinforcementRules.Select(rule => new InvasionReinforcementRule(rule.Id,
                    rule.IntervalInDays, rule.MaxConcurrentParties)).ToList();
        }

        bool IInvasionReinforcementQuery.IsSupplyBase(Invasion invasion, string settlementId) =>
            _invasionSupplyBases.IsSupplyBase(Settlement.Find(settlementId), GetConfiguration(invasion));

        IReadOnlyList<string> IInvasionReinforcementQuery.GetLandingSites(Invasion invasion)
        {
            InvasionData? configuration = GetConfiguration(invasion);

            return Settlement.All
                .Where(settlement => IsEligibleLandingSettlement(settlement, invasion, configuration))
                .Select(settlement => settlement.StringId).ToList();
        }

        // Each rule holds its own count, so a stream at its ceiling never holds another stream off.
        int IInvasionReinforcementQuery.CountActiveReinforcements(Invasion invasion, string ruleId) =>
            MobileParty.All.Count(party => party.IsActive && _dynamicSpawnData.GetDynamicSpawnData(party) is { } data &&
                CountsAgainstMaxConcurrentParties(party.ActualClan?.StringId, data, invasion) &&
                string.Equals(data.ReinforcementRuleId, ruleId, StringComparison.Ordinal));

        /**
         * Vanilla's own measure of what a faction fields -- the parties on the map plus the garrisons,
         * which is where an occupation puts the escort it arrived with -- narrowed to the clans of this
         * invasion. The measure is vanilla's to define, so it is read off the clan rather than summed
         * again here. It is a cached total, and refreshed first the way vanilla refreshes it before
         * reading it, so that a party landed a moment ago already counts against the next rule of the
         * same day.
         */
        InvasionStrengthBudget IInvasionReinforcementQuery.GetStrengthBudget(Invasion invasion)
        {
            InvasionData? configuration = GetConfiguration(invasion);
            List<Clan> clans = Clan.All.Where(clan => invasion.HoldsClan(clan.StringId)).ToList();
            double currentStrength = 0;

            foreach (Clan clan in clans)
            {
                clan.UpdateCurrentStrength();
                currentStrength += clan.CurrentTotalStrength;
            }

            // The per-town bonus is the kingdom's, not the invading clans': a town held by any member of the
            // kingdom raises the ceiling, however that member came by it. The engine keeps the kingdom's fief
            // list live, so a town lost stops counting the moment it changes hands.
            Kingdom? kingdom = clans.Select(clan => clan.Kingdom).FirstOrDefault(candidate => candidate != null);
            int townsHeld = kingdom == null
                ? clans.Sum(clan => clan.Fiefs.Count(fief => fief.IsTown))
                : kingdom.Fiefs.Count(fief => fief.IsTown);

            return new InvasionStrengthBudget(configuration?.MaxFactionStrength ?? 0,
                configuration?.StrengthPerTownHeld ?? 0, townsHeld, currentStrength);
        }

        /**
         * Only a party this invasion landed spends its cap. A party it merely took back from the game
         * costs it nothing, or the first adopted lord would hold the landings off for as long as it lives.
         */
        internal static bool CountsAgainstMaxConcurrentParties(string? clanId, CsPartyData data,
            Invasion invasion) =>
            invasion.HoldsClan(clanId) && data.IsIndependentReinforcement;

        bool IInvasionReinforcementQuery.HasPartySlot(string clanId)
        {
            Clan? clan = Clan.FindFirst(candidate => candidate.StringId == clanId);

            return clan?.IsEliminated == false && clan.WarPartyComponents.Count < clan.WarPartyLimit;
        }

        internal Func<Settlement, bool>? FindInitialCoastalTargetFilter(MobileParty party)
        {
            Invasion? invasion = _invasionState.FindInvasionOfParty(party.StringId);

            if (invasion == null || invasion.IsSuspended || !invasion.HoldsClan(party.ActualClan?.StringId))
            {
                return null;
            }

            InvasionData? configuration = GetConfiguration(invasion);

            if (configuration?.RequiresFirstCoastalConquest != true)
            {
                return null;
            }

            // Until the invasion has landed anywhere, any supply base will do. Once it has a capital, that
            // capital is the only target that lifts the restriction, so losing it sends every party back for
            // it rather than at whichever other port happens to be closest.
            if (invasion.CapitalSettlementId != null)
            {
                string capitalId = invasion.CapitalSettlementId;

                return _invasionState.HoldsCapital(invasion) ? null : settlement => settlement.StringId == capitalId;
            }

            bool factionHasSupplyBase = Settlement.All.Any(settlement =>
                settlement.MapFaction == party.MapFaction &&
                _invasionSupplyBases.IsSupplyBase(settlement, configuration));

            return factionHasSupplyBase
                ? null
                : settlement => _invasionSupplyBases.IsSupplyBase(settlement, configuration);
        }

        private bool IsEligibleLandingSettlement(Settlement settlement, Invasion invasion,
            InvasionData? configuration)
        {
            InvasionFoothold? foothold = _invasionState.FindFoothold(settlement.StringId);

            return foothold?.InvasionClanId == invasion.ClanId &&
                   invasion.HoldsClan(settlement.OwnerClan?.StringId) && settlement.SiegeEvent == null &&
                   _invasionSupplyBases.IsSupplyBase(settlement, configuration);
        }

        private InvasionData? GetConfiguration(Invasion invasion) =>
            _spawnDao.FindByPartyTemplateId(invasion.SpawnTemplateId)?.Invasion;

        private InvasionReinforcementRuleData? FindRule(Invasion invasion, string ruleId) =>
            GetConfiguration(invasion)?.ReinforcementRules.FirstOrDefault(rule => rule.Id == ruleId);
    }
}
