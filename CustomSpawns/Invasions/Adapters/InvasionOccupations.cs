using System.Collections.Generic;
using System.Linq;
using CustomSpawns.Data.Dao;
using CustomSpawns.Data.Dto;
using CustomSpawns.Spawn;
using NLog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;

namespace CustomSpawns.Invasions.Adapters
{
    /**
     * What a captured town becomes: its escorts are absorbed into the garrison, its capturer becomes a
     * lord of the realm the invasion founds, and the invasion keeps feeding it while it holds it.
     */
    internal sealed class InvasionOccupations
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly InvasionState _state;
        private readonly SpawnDao _spawns;
        private readonly DynamicSpawnData _parties;
        private readonly InvasionCoastalTownResolver _coastalTownResolver;

        internal InvasionOccupations(InvasionState state, SpawnDao spawns, DynamicSpawnData parties,
            InvasionCoastalTownResolver coastalTownResolver)
        {
            _state = state;
            _spawns = spawns;
            _parties = parties;
            _coastalTownResolver = coastalTownResolver;
        }

        internal float GetOngoingFoodSupport(Town town)
        {
            InvasionFoothold? foothold = _state.FindFoothold(town.Settlement.StringId);
            InvasionGroupState? group = _state.FindGroup(foothold?.GroupId);
            InvasionData? configuration = group == null ? null : GetConfiguration(group);
            return IsEligibleForFood(town, group, configuration) ? configuration!.DailyFoodBonus : 0f;
        }

        internal void ApplyReady()
        {
            foreach (InvasionCapture capture in GetReadyOccupations())
            {
                Town? town = Settlement.Find(capture.SettlementId)?.Town;
                InvasionGroupState? group = _state.FindGroup(capture.CapturingGroupId);
                InvasionData? configuration = group == null ? null : GetConfiguration(group);

                if (town?.GarrisonParty == null || group == null || configuration == null)
                {
                    continue;
                }

                foreach (MobileParty party in MobileParty.All.Where(p => p.IsActive &&
                             capture.ParticipantPartyIds.Contains(p.StringId) &&
                             _parties.GetDynamicSpawnData(p)?.IsEscort == true).ToList())
                {
                    town.GarrisonParty.MemberRoster.Add(party.MemberRoster);
                    DestroyPartyAction.Apply(null, party);
                }

                if (IsEligibleForFood(town, group, configuration))
                {
                    InvasionOccupationFood.Apply(town, configuration.FoodStockFloor);
                }

                if (!PromoteCapturer(capture, town, group))
                {
                    continue;
                }

                if (_state.CompleteOccupation(capture.Id, town.Settlement.OwnerClan?.StringId ?? ""))
                {
                    Logger.Info("Invasion occupation applied: group=" + group.Id + " town=" + town.Settlement.StringId);
                }
            }
        }

        private static bool PromoteCapturer(InvasionCapture capture, Town town, InvasionGroupState group)
        {
            MobileParty? party = MobileParty.All.FirstOrDefault(candidate => candidate.IsActive &&
                candidate.StringId == capture.CapturerId);
            Hero? leader = party?.LeaderHero;
            Clan? clan = party?.ActualClan;

            if (party == null || leader == null || clan == null || leader.Clan != clan ||
                !group.HoldsClan(clan.StringId))
            {
                return false;
            }

            if (party.PartyComponent is not LordPartyComponent)
            {
                LordPartyComponent.ConvertPartyToLordParty(party, leader, leader);
            }

            if (clan.Kingdom == null)
            {
                clan.SetInitialHomeSettlement(town.Settlement);
                Campaign.Current.KingdomManager.CreateKingdom(clan.Name, clan.InformalName, clan.Culture, clan);
            }

            return true;
        }

        private bool IsEligibleForFood(Town town, InvasionGroupState? group, InvasionData? configuration)
        {
            Clan? clan = group == null ? null : Clan.FindFirst(candidate => candidate.StringId == group.ClanId);
            InvasionFoothold? foothold = _state.FindFoothold(town.Settlement.StringId);
            return InvasionFoodSupportRules.IsEligibleForOngoingSupport(foothold?.GroupId == group?.Id,
                configuration != null && _coastalTownResolver.IsCoastalTown(town.Settlement),
                group?.HoldsClan(town.Settlement.OwnerClan?.StringId) == true, configuration != null && !group!.IsSuspended &&
                clan?.IsEliminated == false, town.Settlement.SiegeEvent != null);
        }

        // Occupation is applied on the hourly tick after Bannerlord has finished the capture callbacks.
        private IReadOnlyList<InvasionCapture> GetReadyOccupations()
        {
            var ready = new List<InvasionCapture>();

            foreach (var capture in _state.Captures)
            {
                Settlement? settlement = Settlement.Find(capture.SettlementId);

                if (settlement?.Town?.GarrisonParty?.IsActive != true || settlement.Party.MapEvent != null ||
                    settlement.SiegeEvent != null ||
                    !_state.CanApplyOccupation(capture.Id, settlement.OwnerClan?.StringId ?? ""))
                {
                    continue;
                }

                bool donorInBattle = MobileParty.All.Any(p => p.IsActive && capture.ParticipantPartyIds.Contains(p.StringId) && p.MapEvent != null);

                if (!donorInBattle)
                {
                    ready.Add(capture);
                }
            }

            return ready;
        }

        private InvasionData? GetConfiguration(InvasionGroupState group) =>
            _spawns.FindByPartyTemplateId(group.SpawnTemplateId)?.Invasion;
    }
}
