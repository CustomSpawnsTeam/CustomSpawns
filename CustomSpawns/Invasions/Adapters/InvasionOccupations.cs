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
        private readonly InvasionState _invasionState;
        private readonly SpawnDao _spawnDao;
        private readonly DynamicSpawnData _dynamicSpawnData;
        private readonly InvasionCoastalTownResolver _invasionCoastalTownResolver;

        internal InvasionOccupations(InvasionState state, SpawnDao spawns, DynamicSpawnData parties,
            InvasionCoastalTownResolver coastalTownResolver)
        {
            _invasionState = state;
            _spawnDao = spawns;
            _dynamicSpawnData = parties;
            _invasionCoastalTownResolver = coastalTownResolver;
        }

        internal float GetOngoingFoodSupport(Town town)
        {
            InvasionFoothold? foothold = _invasionState.FindFoothold(town.Settlement.StringId);
            Invasion? invasion = _invasionState.FindInvasion(foothold?.InvasionClanId);
            InvasionData? configuration = invasion == null ? null : GetConfiguration(invasion);
            return IsEligibleForFood(town, invasion, configuration) ? configuration!.DailyFoodBonus : 0f;
        }

        internal void ApplyReady()
        {
            foreach (InvasionCapture capture in GetReadyOccupations())
            {
                Town? town = Settlement.Find(capture.SettlementId)?.Town;
                Invasion? invasion = _invasionState.FindInvasion(capture.CapturingInvasionClanId);
                InvasionData? configuration = invasion == null ? null : GetConfiguration(invasion);

                if (town?.GarrisonParty == null || invasion == null || configuration == null)
                {
                    continue;
                }

                foreach (MobileParty party in MobileParty.All.Where(p => p.IsActive &&
                             capture.ParticipantPartyIds.Contains(p.StringId) &&
                             _dynamicSpawnData.GetDynamicSpawnData(p)?.IsEscort == true).ToList())
                {
                    town.GarrisonParty.MemberRoster.Add(party.MemberRoster);
                    DestroyPartyAction.Apply(null, party);
                }

                if (IsEligibleForFood(town, invasion, configuration))
                {
                    InvasionOccupationFood.Apply(town, configuration.FoodStockFloor);
                }

                if (!PromoteCapturer(capture, town, invasion))
                {
                    continue;
                }

                if (_invasionState.CompleteOccupation(capture.Id, town.Settlement.OwnerClan?.StringId ?? ""))
                {
                    Logger.Info("Invasion occupation applied: invasion=" + invasion.ClanId + " town=" + town.Settlement.StringId);
                }
            }
        }

        private static bool PromoteCapturer(InvasionCapture capture, Town town, Invasion invasion)
        {
            MobileParty? party = MobileParty.All.FirstOrDefault(candidate => candidate.IsActive &&
                candidate.StringId == capture.CapturerId);
            Hero? leader = party?.LeaderHero;
            Clan? clan = party?.ActualClan;

            if (party == null || leader == null || clan == null || leader.Clan != clan ||
                !invasion.HoldsClan(clan.StringId))
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

        private bool IsEligibleForFood(Town town, Invasion? invasion, InvasionData? configuration)
        {
            Clan? clan = invasion == null ? null : Clan.FindFirst(candidate => candidate.StringId == invasion.ClanId);
            InvasionFoothold? foothold = _invasionState.FindFoothold(town.Settlement.StringId);
            return InvasionFoodSupportRules.IsEligibleForOngoingSupport(foothold?.InvasionClanId == invasion?.ClanId,
                configuration != null && _invasionCoastalTownResolver.IsCoastalTown(town.Settlement),
                invasion?.HoldsClan(town.Settlement.OwnerClan?.StringId) == true, configuration != null && !invasion!.IsSuspended &&
                clan?.IsEliminated == false, town.Settlement.SiegeEvent != null);
        }

        // Occupation is applied on the hourly tick after Bannerlord has finished the capture callbacks.
        private IReadOnlyList<InvasionCapture> GetReadyOccupations()
        {
            var ready = new List<InvasionCapture>();

            foreach (var capture in _invasionState.Captures)
            {
                Settlement? settlement = Settlement.Find(capture.SettlementId);

                if (settlement?.Town?.GarrisonParty?.IsActive != true || settlement.Party.MapEvent != null ||
                    settlement.SiegeEvent != null ||
                    !_invasionState.CanApplyOccupation(capture.Id, settlement.OwnerClan?.StringId ?? ""))
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

        private InvasionData? GetConfiguration(Invasion invasion) =>
            _spawnDao.FindByPartyTemplateId(invasion.SpawnTemplateId)?.Invasion;
    }
}
