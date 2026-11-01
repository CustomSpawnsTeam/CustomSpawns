using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NLog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;

namespace CustomSpawns.Invasions.Adapters
{
    public sealed class InvasionLossDiagnostics
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly Func<InvasionState> _invasionState;
        private readonly Dictionary<string, PartyObservation> _partyObservations = new(StringComparer.Ordinal);
        private readonly Dictionary<MapEvent, string> _battleInvasions = new();

        public InvasionLossDiagnostics(Func<InvasionState> invasionState)
        {
            _invasionState = invasionState;
        }

        public void RegisterEvents()
        {
            CampaignEvents.MapEventStarted.AddNonSerializedListener(this, OnBattleStarted);
            CampaignEvents.MapEventEnded.AddNonSerializedListener(this, OnBattleEnded);
            CampaignEvents.MobilePartyDestroyed.AddNonSerializedListener(this, OnPartyDestroyed);
            CampaignEvents.OnPartyRemovedEvent.AddNonSerializedListener(this, OnPartyRemoved);
            CampaignEvents.OnTroopsDesertedEvent.AddNonSerializedListener(this, OnTroopsDeserted);
            CampaignEvents.HeroPrisonerTaken.AddNonSerializedListener(this, OnHeroCaptured);
            CampaignEvents.HeroPrisonerReleased.AddNonSerializedListener(this, OnHeroReleased);
            CampaignEvents.HeroKilledEvent.AddNonSerializedListener(this, OnHeroKilled);
            CampaignEvents.CharacterBecameFugitiveEvent.AddNonSerializedListener(this, OnHeroFugitive);
        }

        public void ObserveParties(string cadence)
        {
            Record(() =>
            {
                if (cadence == "load")
                {
                    _partyObservations.Clear();
                    _battleInvasions.Clear();
                }

                var activeIds = new HashSet<string>(StringComparer.Ordinal);
                foreach (MobileParty party in MobileParty.All.Where(party => party.IsActive))
                {
                    string? invasionId = FindInvasion(party.Party);
                    if (invasionId == null)
                    {
                        continue;
                    }

                    activeIds.Add(party.StringId);
                    string facts = Describe(party);
                    _partyObservations.TryGetValue(party.StringId, out PartyObservation previous);
                    int count = party.MemberRoster.TotalManCount;
                    Logger.Info("Invasion party observation: cadence=" + cadence +
                                " day=" + Number(CampaignTime.Now.ToDays) +
                                " invasion=" + invasionId +
                                " party=" + party.StringId +
                                " previous_troops=" + (previous.Facts == null ? "unknown" : previous.Count.ToString()) +
                                " troop_delta=" + (previous.Facts == null ? "unknown" : (count - previous.Count).ToString()) +
                                " previous_day=" + (previous.Facts == null ? "unknown" : Number(previous.Day)) +
                                " delta_kind=observed_net_change delta_cause=unattributed " + facts);
                    _partyObservations[party.StringId] = new PartyObservation(
                        invasionId, count, CampaignTime.Now.ToDays, facts);
                }

                foreach (string staleId in _partyObservations.Keys.Where(id => !activeIds.Contains(id)).ToArray())
                {
                    PartyObservation previous = _partyObservations[staleId];
                    Logger.Info("Invasion party no longer observed: party=" + staleId +
                                " invasion=" + previous.InvasionId +
                                " last_day=" + Number(previous.Day) +
                                " cause=unknown_or_membership_changed last=[" + previous.Facts + "]");
                    _partyObservations.Remove(staleId);
                }
            });
        }

        private void OnBattleStarted(MapEvent mapEvent, PartyBase attacker, PartyBase defender)
        {
            Record(() =>
            {
                string invasionIds = FindBattleInvasions(mapEvent);
                if (invasionIds.Length == 0)
                {
                    return;
                }

                _battleInvasions[mapEvent] = invasionIds;
                LogBattle("start", mapEvent, invasionIds);
            });
        }

        private void OnBattleEnded(MapEvent mapEvent)
        {
            Record(() =>
            {
                if (!_battleInvasions.TryGetValue(mapEvent, out string invasionIds))
                {
                    invasionIds = FindBattleInvasions(mapEvent);
                }
                _battleInvasions.Remove(mapEvent);
                if (invasionIds.Length == 0)
                {
                    return;
                }

                LogBattle("end", mapEvent, invasionIds);
            });
        }

        private static void LogBattle(string stage, MapEvent mapEvent, string invasionIds)
        {
            Logger.Info("Invasion battle: stage=" + stage +
                        " event=" + mapEvent.Id +
                        " start_day=" + Number(mapEvent.BattleStartTime.ToDays) +
                        " day=" + Number(CampaignTime.Now.ToDays) +
                        " invasions=[" + invasionIds + "] type=" + mapEvent.EventType +
                        " settlement=" + mapEvent.MapEventSettlement?.StringId +
                        " position=" + mapEvent.Position +
                        " state=" + mapEvent.BattleState +
                        " winner=" + mapEvent.WinningSide +
                        " retreating=" + mapEvent.RetreatingSide +
                        " diplomatic_end=" + mapEvent.DiplomaticallyFinished +
                        " attackers=[" + Describe(mapEvent.AttackerSide, stage) + "]" +
                        " defenders=[" + Describe(mapEvent.DefenderSide, stage) + "]");
        }

        private static string Describe(MapEventSide side, string stage) =>
            string.Join(";", side.Parties.Select(participant =>
                "party=" + participant.Party.Id +
                ",faction=" + participant.Party.MapFaction?.StringId +
                ",leader=" + participant.Party.LeaderHero?.StringId +
                ",snapshot_stage=" + stage +
                ",current_strength=" + Number(participant.Party.CalculateCurrentStrength()) +
                ",current_nonhero_tiers=[" + DescribeTiers(participant.Party.MemberRoster) + "]" +
                ",start_healthy=" + participant.HealthyManCountAtStart +
                ",killed=" + participant.DiedInBattle.TotalManCount +
                ",wounded=" + participant.WoundedInBattle.TotalManCount +
                ",routed=" + participant.RoutedInBattle.TotalManCount +
                ",remaining=" + participant.Party.NumberOfAllMembers +
                ",healthy=" + participant.Party.NumberOfHealthyMembers +
                ",active=" + participant.Party.IsActive));

        private string FindBattleInvasions(MapEvent mapEvent) =>
            string.Join(";", mapEvent.InvolvedParties.Select(FindInvasion)
                .Where(id => id != null).Distinct(StringComparer.Ordinal).OrderBy(id => id));

        private void OnPartyDestroyed(MobileParty party, PartyBase destroyer) =>
            Record(() => LogPartyRemoval("destroyed", party.Party, destroyer));

        private void OnPartyRemoved(PartyBase party) =>
            Record(() => LogPartyRemoval("removed", party, null));

        private void LogPartyRemoval(string stage, PartyBase party, PartyBase? destroyer)
        {
            _partyObservations.TryGetValue(party.Id, out PartyObservation previous);
            string? invasionId = FindInvasion(party) ?? previous.InvasionId;
            if (invasionId == null)
            {
                return;
            }

            Logger.Info("Invasion party loss: stage=" + stage +
                        " day=" + Number(CampaignTime.Now.ToDays) +
                        " invasion=" + invasionId +
                        " party=" + party.Id +
                        " destroyer=" + destroyer?.Id +
                        " destroyer_faction=" + destroyer?.MapFaction?.StringId +
                        " leader=" + party.LeaderHero?.StringId +
                        " troops=" + party.NumberOfAllMembers +
                        " healthy=" + party.NumberOfHealthyMembers +
                        " event=" + party.MobileParty?.MapEvent?.Id +
                        " last_day=" + (previous.Facts == null ? "unknown" : Number(previous.Day)) +
                        " last=[" + previous.Facts + "]");
        }

        private void OnTroopsDeserted(MobileParty party, TroopRoster deserters)
        {
            Record(() =>
            {
                string? invasionId = FindInvasion(party.Party);
                if (invasionId == null)
                {
                    return;
                }

                Logger.Info("Invasion desertion: day=" + Number(CampaignTime.Now.ToDays) +
                            " invasion=" + invasionId + " party=" + party.StringId +
                            " deserted=" + deserters.TotalManCount +
                            " roster=[" + string.Join(";", deserters.GetTroopRoster().Select(troop =>
                                troop.Character.StringId + ":" + troop.Number)) + "] " +
                            Describe(party));
            });
        }

        private void OnHeroCaptured(PartyBase captor, Hero hero) =>
            Record(() => LogHero("captured", hero, "captor=" + captor?.Id +
                " captor_faction=" + captor?.MapFaction?.StringId));

        private void OnHeroReleased(Hero hero, PartyBase party, IFaction capturer,
            EndCaptivityDetail detail, bool showNotification) =>
            Record(() => LogHero("released", hero, "detail=" + detail +
                " captor=" + party?.Id + " captor_faction=" + capturer?.StringId));

        private void OnHeroKilled(Hero hero, Hero killer,
            KillCharacterAction.KillCharacterActionDetail detail, bool showNotification) =>
            Record(() => LogHero("killed_or_removed", hero,
                "detail=" + detail + " killer=" + killer?.StringId));

        private void OnHeroFugitive(Hero hero, bool showNotification) =>
            Record(() => LogHero("fugitive", hero, ""));

        private void LogHero(string stage, Hero hero, string detail)
        {
            Invasion? invasion = _invasionState().FindInvasionHolding(hero.Clan?.StringId);
            if (invasion == null)
            {
                return;
            }

            Logger.Info("Invasion lord event: stage=" + stage +
                        " day=" + Number(CampaignTime.Now.ToDays) +
                        " invasion=" + invasion.ClanId + " hero=" + hero.StringId +
                        " clan=" + hero.Clan?.StringId +
                        " party=" + hero.PartyBelongedTo?.StringId +
                        " alive=" + hero.IsAlive + " state=" + hero.HeroState + " " + detail);
        }

        private string? FindInvasion(PartyBase party)
        {
            string? clanId = party.MobileParty?.ActualClan?.StringId ?? party.Settlement?.OwnerClan?.StringId;
            if (clanId == null && party.MobileParty is MobileParty mobile &&
                (mobile.IsGarrison || mobile.IsMilitia))
            {
                clanId = mobile.CurrentSettlement?.OwnerClan?.StringId;
            }

            return _invasionState().FindInvasionHolding(clanId)?.ClanId ??
                   _invasionState().FindInvasionOfParty(party.Id)?.ClanId;
        }

        private static string Describe(MobileParty party) =>
            "clan=" + party.ActualClan?.StringId +
            " faction=" + party.MapFaction?.StringId +
            " component=" + party.PartyComponent?.GetType().Name +
            " lord_party=" + party.IsLordParty +
            " garrison=" + party.IsGarrison +
            " militia=" + party.IsMilitia +
            " troops=" + party.MemberRoster.TotalManCount +
            " wounded=" + party.MemberRoster.TotalWounded +
            " current_nonhero_tiers=[" + DescribeTiers(party.MemberRoster) + "]" +
            " prisoners=" + party.PrisonRoster.TotalManCount +
            " limit=" + party.Party.PartySizeLimit +
            " strength=" + Number(party.Party.CalculateCurrentStrength()) +
            " food=" + Number(party.Food) + " food_change=" + Number(party.FoodChange) +
            " starving=" + party.Party.IsStarving + " starving_days=" + Number(party.Party.DaysStarving) +
            " morale=" + Number(party.Morale) +
            " wage=" + party.TotalWage + " unpaid_wages=" + Number(party.HasUnpaidWages) +
            " trade_gold=" + party.PartyTradeGold +
            " leader_gold=" + party.LeaderHero?.Gold + " clan_gold=" + party.ActualClan?.Gold +
            " leader=" + party.LeaderHero?.StringId +
            " army=" + party.Army?.LeaderParty.StringId +
            " attached_to=" + party.AttachedTo?.StringId +
            " settlement=" + party.CurrentSettlement?.StringId +
            " position=" + party.Position +
            " besieging=" + party.BesiegedSettlement?.StringId +
            " target_settlement=" + party.TargetSettlement?.StringId +
            " target_party=" + party.TargetParty?.StringId +
            " behavior=" + party.DefaultBehavior +
            " battle=" + party.MapEvent?.Id;

        private static string DescribeTiers(TroopRoster roster) =>
            string.Join(";", roster.GetTroopRoster()
                .Where(troop => !troop.Character.IsHero)
                .GroupBy(troop => troop.Character.Tier)
                .OrderBy(tier => tier.Key)
                .Select(tier => "t" + tier.Key + ":total=" + tier.Sum(troop => troop.Number) +
                    ",healthy=" + tier.Sum(troop => troop.Number - troop.WoundedNumber)));

        private static string Number(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);

        private static void Record(Action write)
        {
            try
            {
                write();
            }
            catch (System.Exception exception)
            {
                Logger.Warn(exception, "Invasion loss diagnostics unavailable");
            }
        }

        private readonly struct PartyObservation
        {
            public PartyObservation(string invasionId, int count, double day, string facts)
            {
                InvasionId = invasionId;
                Count = count;
                Day = day;
                Facts = facts;
            }

            public string? InvasionId { get; }
            public int Count { get; }
            public double Day { get; }
            public string? Facts { get; }
        }
    }
}