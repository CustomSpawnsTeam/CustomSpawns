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
        private readonly InvasionCoastalTownResolver _invasionCoastalTownResolver;
        private readonly InvasionReinforcements _invasionReinforcements;

        internal InvasionReinforcementArrivals(InvasionState state, SpawnDao spawns, DynamicSpawnData parties,
            SpawnGroupService spawnGroups, InvasionCoastalTownResolver coastalTownResolver, Func<int, int> randomIndex)
        {
            _invasionState = state;
            _spawnDao = spawns;
            _dynamicSpawnData = parties;
            _spawnGroupService = spawnGroups;
            _invasionCoastalTownResolver = coastalTownResolver;
            _invasionReinforcements = new InvasionReinforcements(state, this, randomIndex);
        }

        internal void StartSchedule(long captureId)
        {
            InvasionGroupState? group = _invasionReinforcements.StartSchedule(captureId);

            if (group == null)
            {
                return;
            }

            Logger.Info("Invasion coastal foothold gained: group=" + group.Id + " town=" +
                        _invasionState.FindCapture(captureId)!.SettlementId + " next_arrival_day=" +
                        group.NextReinforcementDeadlineDays);
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
                    Logger.Info("Invasion reinforcement slot skipped: group=" + draw.Group.Id +
                                " reason=" + SkipReason(draw.Outcome));
                }
            }
        }

        private static string SkipReason(InvasionReinforcementOutcome outcome) =>
            outcome == InvasionReinforcementOutcome.ActivePartyCapReached ? "active_cap" :
            outcome == InvasionReinforcementOutcome.NoLandingSite ? "no_held_port" : "configuration_or_clan";

        private void Land(InvasionReinforcementDraw draw)
        {
            InvasionData? configuration = GetConfiguration(draw.Group);
            SpawnDto? spawn = _spawnDao.FindByPartyTemplateId(draw.Group.SpawnTemplateId);
            Settlement? landing = Settlement.Find(draw.LandingSettlementId!);
            PartyTemplateObject? template = configuration == null ? null :
                Campaign.Current.ObjectManager.GetObject<PartyTemplateObject>(configuration.ReinforcementPartyTemplateId);

            if (spawn == null || template == null || landing == null ||
                !IsEligibleLandingSettlement(landing, draw.Group))
            {
                Logger.Info("Invasion reinforcement slot skipped: group=" + draw.Group.Id +
                            " reason=template_or_port_changed");
                return;
            }

            Clan? clan = draw.ClanId == null
                ? FoundClan(draw.Group, landing)
                : Clan.FindFirst(candidate => candidate.StringId == draw.ClanId);

            if (clan == null)
            {
                Logger.Info("Invasion reinforcement slot skipped: group=" + draw.Group.Id +
                            " reason=no_clan_to_take_the_party");
                return;
            }

            MobileParty? reinforcement = _spawnGroupService.SpawnInvasionReinforcement(spawn, template, landing, clan);
            Logger.Info("Invasion reinforcement " + (reinforcement == null ? "failed" : "arrived") + ": group=" +
                        draw.Group.Id + " clan=" + clan.StringId + " town=" + landing.StringId +
                        " next_arrival_day=" + draw.Group.NextReinforcementDeadlineDays);
        }

        /**
         * Vanilla caps a clan's war parties by tier, so an invasion that outgrows its clans expands into
         * another one instead of into a single oversized clan: its kingdom then distributes fiefs and
         * respawns lords across all of them the way it does for any kingdom of its own.
         */
        private static Clan? FoundClan(InvasionGroupState group, Settlement landing)
        {
            Clan? founder = Clan.FindFirst(candidate => candidate.StringId == group.ClanId);
            Kingdom? kingdom = founder?.Kingdom;
            CharacterObject? lordTemplate = founder?.Culture?.LordTemplates
                .FirstOrDefault(character => character.Occupation == Occupation.Lord);

            if (founder == null || kingdom == null || lordTemplate == null)
            {
                return null;
            }

            Clan clan = Clan.CreateClan(founder.StringId + "_" + group.ClanIds.Count);
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
            group.AddClan(clan.StringId);
            Logger.Info("Invasion clan founded: group=" + group.Id + " clan=" + clan.StringId +
                        " kingdom=" + kingdom.StringId);
            return clan;
        }

        double IInvasionReinforcementQuery.NowInDays => CampaignTime.Now.ToDays;

        InvasionReinforcementSettings? IInvasionReinforcementQuery.GetSettings(InvasionGroupState group)
        {
            InvasionData? configuration = GetConfiguration(group);
            Clan? clan = Clan.FindFirst(candidate => candidate.StringId == group.ClanId);

            return configuration == null || clan?.IsEliminated != false
                ? null
                : new InvasionReinforcementSettings(configuration.ReinforcementIntervalInDays,
                    configuration.ReinforcementActivePartyCap);
        }

        bool IInvasionReinforcementQuery.IsCoastalTown(string settlementId) =>
            _invasionCoastalTownResolver.IsCoastalTown(Settlement.Find(settlementId));

        IReadOnlyList<string> IInvasionReinforcementQuery.GetLandingSites(InvasionGroupState group) =>
            Settlement.All.Where(settlement => IsEligibleLandingSettlement(settlement, group))
                .Select(settlement => settlement.StringId).ToList();

        int IInvasionReinforcementQuery.CountActiveReinforcements(InvasionGroupState group) =>
            MobileParty.All.Count(party => party.IsActive && _dynamicSpawnData.GetDynamicSpawnData(party) is { } data &&
                CountsAgainstActivePartyCap(data, group));

        /**
         * Only a party this invasion landed spends its cap. A party it merely took back from the game
         * costs it nothing, or the first adopted lord would hold the landings off for as long as it lives.
         */
        internal static bool CountsAgainstActivePartyCap(CsPartyData data, InvasionGroupState group) =>
            data.InvasionGroupId == group.Id && data.IsIndependentReinforcement;

        bool IInvasionReinforcementQuery.HasPartySlot(string clanId)
        {
            Clan? clan = Clan.FindFirst(candidate => candidate.StringId == clanId);

            return clan?.IsEliminated == false && clan.WarPartyComponents.Count < clan.WarPartyLimit;
        }

        internal Func<Settlement, bool>? FindInitialCoastalTargetFilter(MobileParty party)
        {
            InvasionGroupState? group = _invasionState.FindMemberGroup(party.StringId);

            if (group == null || group.IsSuspended || !group.HoldsClan(party.ActualClan?.StringId))
            {
                return null;
            }

            InvasionData? configuration = GetConfiguration(group);

            if (configuration?.RequiresFirstCoastalConquest != true)
            {
                return null;
            }

            bool factionHasCoastalTown = Settlement.All.Any(settlement => settlement.IsTown &&
                settlement.MapFaction == party.MapFaction && _invasionCoastalTownResolver.IsCoastalTown(settlement));

            return factionHasCoastalTown ? null : _invasionCoastalTownResolver.IsCoastalTown;
        }

        private bool IsEligibleLandingSettlement(Settlement settlement, InvasionGroupState group)
        {
            InvasionFoothold? foothold = _invasionState.FindFoothold(settlement.StringId);

            return foothold?.GroupId == group.Id && settlement.IsTown &&
                   group.HoldsClan(settlement.OwnerClan?.StringId) && settlement.SiegeEvent == null &&
                   _invasionCoastalTownResolver.IsCoastalTown(settlement);
        }

        private InvasionData? GetConfiguration(InvasionGroupState group) =>
            _spawnDao.FindByPartyTemplateId(group.SpawnTemplateId)?.Invasion;
    }
}
