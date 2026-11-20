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

                foreach (MobileParty party in MobileParty.All.Where(p => capture.CanAbsorbEscort(p.StringId,
                             p.IsActive, () => _dynamicSpawnData.GetDynamicSpawnData(p)?.IsEscort == true)).ToList())
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
            var realmIds = new HashSet<string>(_invasionCapitals.FindAll().Select(capital => capital.InvaderKingdomId));

            foreach (Kingdom kingdom in Kingdom.All.ToList())
            {
                if (!RestoreFounder(kingdom) && realmIds.Contains(kingdom.StringId))
                {
                    ReclaimFromForeignRuler(kingdom);
                }
            }
        }

        private bool RestoreFounder(Kingdom kingdom)
        {
            Invasion? invasion = _invasionState.FindInvasionHolding(kingdom.RulingClan?.StringId);
            Clan? founder = invasion == null
                ? null
                : Clan.FindFirst(candidate => candidate.StringId == invasion.ClanId);

            if (invasion == null || founder == null ||
                !invasion.ShouldRestoreCrown(kingdom.StringId, kingdom.RulingClan?.StringId, kingdom.IsEliminated,
                    (founder.IsEliminated, founder.Leader != null, founder.Kingdom?.StringId)))
            {
                return false;
            }

            // Crowned before it rejoins: the no-kingdom policy evicts it again the moment it joins a realm
            // it does not rule.
            ChangeRulingClanAction.Apply(kingdom, founder);

            if (founder.Kingdom == null)
            {
                ChangeKingdomAction.ApplyByJoinToKingdom(founder, kingdom, default, false);
            }

            Logger.Info("Invasion crown restored: invasion=" + invasion.ClanId + " kingdom=" +
                        kingdom.StringId);
            return true;
        }

        // An invasion realm a clan not of the invasion came to rule goes back to the invasion's leading clan.
        private void ReclaimFromForeignRuler(Kingdom kingdom)
        {
            foreach (Invasion invasion in _invasionState.Invasions)
            {
                Clan? leader = invasion.FindLeadingClan();

                if (leader?.Leader == null || !invasion.ShouldReclaimCrown(kingdom.StringId,
                        kingdom.RulingClan?.StringId, kingdom.IsEliminated, leader.StringId, leader.Kingdom?.StringId))
                {
                    continue;
                }

                Logger.Info("Invasion crown reclaimed: invasion=" + invasion.ClanId + " kingdom=" +
                            kingdom.StringId + " from=" + kingdom.RulingClan?.StringId + " to=" + leader.StringId);
                ChangeRulingClanAction.Apply(kingdom, leader);
                return;
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

                if (_invasionState.IsOccupationReady(capture.Id, settlement?.OwnerClan?.StringId ?? "",
                        (settlement?.Town?.GarrisonParty?.IsActive == true, settlement?.Party.MapEvent != null,
                            settlement?.SiegeEvent != null),
                        MobileParty.All.Select(party => (party.StringId, party.IsActive, party.MapEvent != null))))
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
