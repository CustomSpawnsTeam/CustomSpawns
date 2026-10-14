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
     * What a captured town becomes: its escorts are absorbed into the garrison, and its capturer becomes a
     * lord of the realm the invasion founds.
     */
    public sealed class InvasionOccupations
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly InvasionState _invasionState;
        private readonly SpawnDao _spawnDao;
        private readonly DynamicSpawnData _dynamicSpawnData;
        private readonly IInvasionCapitalRepository _invasionCapitals;
        private readonly IInvasionClanRepository _invasionClans;

        public InvasionOccupations(InvasionState state, SpawnDao spawns, DynamicSpawnData parties,
            IInvasionCapitalRepository invasionCapitals, IInvasionClanRepository invasionClans)
        {
            _invasionState = state;
            _spawnDao = spawns;
            _dynamicSpawnData = parties;
            _invasionCapitals = invasionCapitals;
            _invasionClans = invasionClans;
        }

        public void ApplyReady()
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

                Clan? capturerClan = PromoteCapturer(capture, town, invasion);
                if (capturerClan?.Kingdom == null)
                {
                    continue;
                }

                _invasionClans.Register(invasion.SpawnTemplateId, capturerClan.StringId);

                if (_invasionState.CompleteOccupation(capture.Id, town.Settlement.OwnerClan?.StringId ?? ""))
                {
                    _invasionCapitals.Register(capturerClan.Kingdom.StringId, town.Settlement.StringId);
                    Logger.Info("Invasion occupation applied: invasion=" + invasion.ClanId + " town=" +
                                town.Settlement.StringId);
                }
            }
        }

        /**
         * The founding clan keeps its realm's crown. Bannerlord leaves a dead ruler's clan out of the election
         * that follows, which hands the realm to a clan the invasion founded; the founding clan is then evicted
         * by its no-kingdom policy, and the realm, ruled by a clan with no diplomacy of its own, makes peace
         * with everybody.
         */
        public void KeepCrowns()
        {
            foreach (Kingdom kingdom in Kingdom.All.Where(k => !k.IsEliminated && k.RulingClan != null).ToList())
            {
                Invasion? invasion = _invasionState.FindInvasionHolding(kingdom.RulingClan.StringId);
                Clan? founder = invasion == null
                    ? null
                    : Clan.FindFirst(candidate => candidate.StringId == invasion.ClanId);

                if (founder == null || founder == kingdom.RulingClan || founder.IsEliminated ||
                    founder.Leader == null || founder.Kingdom != null && founder.Kingdom != kingdom)
                {
                    continue;
                }

                // Crowned before it rejoins: the no-kingdom policy evicts it again the moment it joins a realm
                // it does not rule.
                ChangeRulingClanAction.Apply(kingdom, founder);

                if (founder.Kingdom == null)
                {
                    ChangeKingdomAction.ApplyByJoinToKingdom(founder, kingdom, default, false);
                }

                Logger.Info("Invasion crown restored: invasion=" + invasion!.ClanId + " kingdom=" +
                            kingdom.StringId);
            }
        }

        private static Clan? PromoteCapturer(InvasionCapture capture, Town town, Invasion invasion)
        {
            MobileParty? party = MobileParty.All.FirstOrDefault(candidate => candidate.IsActive &&
                candidate.StringId == capture.CapturerId);
            Hero? leader = party?.LeaderHero;
            Clan? clan = party?.ActualClan;

            if (party == null || leader == null || clan == null || leader.Clan != clan ||
                !invasion.HoldsClan(clan.StringId))
            {
                return null;
            }

            if (party.PartyComponent is not LordPartyComponent)
            {
                LordPartyComponent.ConvertPartyToLordParty(party, leader, leader);
            }

            if (clan.Kingdom == null)
            {
                clan.SetInitialHomeSettlement(town.Settlement);

                // The realm is founded once. A clan that is outside the realm it already founded -- it
                // stays that realm's ruling clan whether it is in it or not -- rejoins that one, instead
                // of founding another realm on every tick that finds it kingdomless.
                Kingdom? founded = Kingdom.All.FirstOrDefault(candidate => !candidate.IsEliminated &&
                    candidate.RulingClan == clan);

                if (founded == null)
                {
                    Campaign.Current.KingdomManager.CreateKingdom(clan.Name, clan.InformalName, clan.Culture, clan);
                }
                else
                {
                    ChangeKingdomAction.ApplyByJoinToKingdom(clan, founded, default, false);
                }
            }

            return clan;
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
