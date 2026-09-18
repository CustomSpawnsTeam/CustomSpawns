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
        private readonly InvasionState _state;
        private readonly SpawnDao _spawns;
        private readonly DynamicSpawnData _parties;
        private readonly SpawnGroupService _spawnGroups;
        private readonly InvasionCoastalTownResolver _coastalTownResolver;
        private readonly InvasionReinforcements _reinforcements;

        internal InvasionReinforcementArrivals(InvasionState state, SpawnDao spawns, DynamicSpawnData parties,
            SpawnGroupService spawnGroups, InvasionCoastalTownResolver coastalTownResolver, Func<int, int> randomIndex)
        {
            _state = state;
            _spawns = spawns;
            _parties = parties;
            _spawnGroups = spawnGroups;
            _coastalTownResolver = coastalTownResolver;
            _reinforcements = new InvasionReinforcements(state, this, randomIndex);
        }

        internal void StartSchedule(long captureId)
        {
            InvasionGroupState? group = _reinforcements.StartSchedule(captureId);
            if (group == null) return;
            Logger.Info("Invasion coastal foothold gained: group=" + group.Id + " town=" +
                        _state.FindCapture(captureId)!.SettlementId + " next_arrival_day=" +
                        group.NextReinforcementDeadlineDays);
        }

        internal void LandDue()
        {
            foreach (InvasionReinforcementDraw draw in _reinforcements.DrawDue())
            {
                if (draw.Outcome == InvasionReinforcementOutcome.Landing) Land(draw);
                else Logger.Info("Invasion reinforcement slot skipped: group=" + draw.Group.Id +
                                 " reason=" + SkipReason(draw.Outcome));
            }
        }

        private static string SkipReason(InvasionReinforcementOutcome outcome) =>
            outcome == InvasionReinforcementOutcome.ActivePartyCapReached ? "active_cap" :
            outcome == InvasionReinforcementOutcome.NoLandingSite ? "no_held_port" : "configuration_or_clan";

        private void Land(InvasionReinforcementDraw draw)
        {
            InvasionData? configuration = GetConfiguration(draw.Group);
            SpawnDto? spawn = _spawns.FindByPartyTemplateId(draw.Group.SpawnTemplateId);
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

            MobileParty? reinforcement = _spawnGroups.SpawnInvasionReinforcement(spawn, template, landing, clan);
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
            if (founder == null || kingdom == null || lordTemplate == null) return null;

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
            _coastalTownResolver.IsCoastalTown(Settlement.Find(settlementId));

        IReadOnlyList<string> IInvasionReinforcementQuery.GetLandingSites(InvasionGroupState group) =>
            Settlement.All.Where(settlement => IsEligibleLandingSettlement(settlement, group))
                .Select(settlement => settlement.StringId).ToList();

        int IInvasionReinforcementQuery.CountActiveReinforcements(InvasionGroupState group) =>
            MobileParty.All.Count(party => party.IsActive && _parties.GetDynamicSpawnData(party) is { } data &&
                data.InvasionGroupId == group.Id && data.IsIndependentReinforcement);

        bool IInvasionReinforcementQuery.HasPartySlot(string clanId)
        {
            Clan? clan = Clan.FindFirst(candidate => candidate.StringId == clanId);
            return clan?.IsEliminated == false && clan.WarPartyComponents.Count < clan.WarPartyLimit;
        }

        private bool IsEligibleLandingSettlement(Settlement settlement, InvasionGroupState group)
        {
            InvasionFoothold? foothold = _state.FindFoothold(settlement.StringId);
            return foothold?.GroupId == group.Id && settlement.IsTown &&
                   group.HoldsClan(settlement.OwnerClan?.StringId) && settlement.SiegeEvent == null &&
                   _coastalTownResolver.IsCoastalTown(settlement);
        }

        private InvasionData? GetConfiguration(InvasionGroupState group) =>
            _spawns.FindByPartyTemplateId(group.SpawnTemplateId)?.Invasion;
    }
}
