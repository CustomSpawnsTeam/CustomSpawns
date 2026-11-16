using System;
using System.Collections.Generic;
using System.Linq;
using CustomSpawns.Data.Dao;
using CustomSpawns.Data.Dto;
using CustomSpawns.Invasions.Adapters;
using CustomSpawns.Invasions.Bank.Adapters;
using CustomSpawns.Invasions.Conquest;
using CustomSpawns.Invasions.Lords;
using CustomSpawns.Spawn;
using NLog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace CustomSpawns.Invasions.Reinforcements.Adapters
{
    /**
     * Everything that keeps an invasion reinforced: when the next wave is due, and whether the capital it
     * would go to can take it. The campaign behaviour only forwards the events that move the clock; the
     * schedule itself belongs to InvasionReinforcements, and paying a wave in belongs to InvasionTroopBanking.
     */
    public sealed class InvasionReinforcementArrivals : IInvasionReinforcementQuery
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly InvasionState _invasionState;
        private readonly SpawnDao _spawnDao;
        private readonly InvasionSupplyBases _invasionSupplyBases;
        private readonly InvasionReinforcements _invasionReinforcements;
        private readonly InvasionTroopBanking _invasionTroopBanking;
        private readonly IInvasionLords _invasionLords;
        private readonly IInvasionConquestTargets _invasionConquestTargets;

        public InvasionReinforcementArrivals(InvasionState state, SpawnDao spawns, InvasionSupplyBases supplyBases,
            InvasionTroopBanking troopBanking, IInvasionLords invasionLords,
            IInvasionConquestTargets invasionConquestTargets)
        {
            _invasionConquestTargets = invasionConquestTargets;
            _invasionState = state;
            _spawnDao = spawns;
            _invasionSupplyBases = supplyBases;
            _invasionTroopBanking = troopBanking;
            _invasionLords = invasionLords;
            _invasionReinforcements = new InvasionReinforcements(state, this);
        }

        public void StartSchedule(long captureId)
        {
            Invasion? invasion = _invasionReinforcements.StartSchedule(captureId);

            if (invasion == null)
            {
                return;
            }

            Logger.Info("Invasion capital taken: invasion=" + invasion.ClanId + " town=" +
                        _invasionState.FindCapture(captureId)!.SettlementId + " next_arrival_days=" +
                        string.Join(",", invasion.ReinforcementSchedules.Select(schedule =>
                            schedule.RuleId + "@" + schedule.NextDeadlineDays)));
        }

        public void SendDue()
        {
            foreach (InvasionReinforcementDraw draw in _invasionReinforcements.DrawDue())
            {
                if (draw.Outcome != InvasionReinforcementOutcome.Banking)
                {
                    Logger.Info("Invasion reinforcement slot skipped: invasion=" + draw.Invasion.ClanId +
                                " rule=" + draw.RuleId + " reason=" + SkipReason(draw.Outcome));

                    _invasionTroopBanking.PayOnly(draw);
                }
                else if (_invasionTroopBanking.Bank(draw))
                {
                    _invasionReinforcements.RecordDelivered(draw);
                }
            }
        }

        public void LogStrength(string cadence)
        {
            foreach (Invasion invasion in _invasionState.Invasions)
            {
                Clan[] clans = Clan.All.Where(clan => invasion.HoldsClan(clan.StringId)).ToArray();

                if (clans.Length == 0)
                {
                    continue;
                }

                MobileParty[] parties = clans.SelectMany(clan => clan.WarPartyComponents)
                    .Select(component => component.MobileParty).Where(party => party.IsActive).ToArray();
                var statistics = new InvasionPartyStatistics(parties.Select(party =>
                    (party.MemberRoster.TotalManCount, party.MemberRoster.TotalHealthyCount,
                        party.Party.PartySizeLimit, party.Party.EstimatedStrength)));
                MobileParty[] garrisons = clans.SelectMany(clan => clan.Fiefs)
                    .Select(fief => fief.GarrisonParty).Where(party => party != null)
                    .Select(party => party!).ToArray();
                double garrisonStrength = garrisons.Sum(party => (double)party.Party.EstimatedStrength);
                InvasionBankLimit bankLimit = GetBankLimit(invasion);
                Kingdom? kingdom = clans.Select(clan => clan.Kingdom).FirstOrDefault(value => value != null);
                int castles = kingdom?.Fiefs.Count(fief => fief.IsCastle) ??
                              clans.Sum(clan => clan.Fiefs.Count(fief => fief.IsCastle));
                Settlement? capital = invasion.CapitalSettlementId == null
                    ? null
                    : Settlement.Find(invasion.CapitalSettlementId);

                Logger.Info(FormattableString.Invariant(
                                $"Invasion strength summary: cadence={cadence} day={CampaignTime.Now.ToDays:0.00} invasion={invasion.ClanId} kingdom={kingdom?.StringId ?? "none"} clans={clans.Length} suspended={invasion.IsSuspended}") +
                            FormattableString.Invariant(
                                $" towns={bankLimit.TownsHeld} castles={castles} faction_strength={statistics.Strength + garrisonStrength:0.0} bank_troops={bankLimit.BankedTroops} bank_limit={bankLimit.Limit} headroom={bankLimit.Headroom} conquest_target_strength={_invasionConquestTargets.MeasureConquestTargetStrength(invasion):0.0}") +
                            FormattableString.Invariant(
                                $" field_strength={statistics.Strength:0.0} field_parties={statistics.Count} field_troops={statistics.Men} field_healthy={statistics.Healthy} party_size_avg={statistics.AverageSize:0.0} party_size_min={statistics.MinimumSize} party_size_max={statistics.MaximumSize} party_limit_avg={statistics.AverageLimit:0.0} party_fill_percent={statistics.FillPercent:0.0}") +
                            FormattableString.Invariant(
                                $" living_lords={_invasionLords.CountLivingLords(invasion)} garrison_troops={garrisons.Sum(party => party.MemberRoster.TotalManCount)} garrison_strength={garrisonStrength:0.0} capital={invasion.CapitalSettlementId ?? "none"} capital_held={_invasionState.HoldsCapital(invasion)} capital_besieged={capital?.IsUnderSiege == true} capital_garrison={capital?.Town?.GarrisonParty?.MemberRoster.TotalManCount ?? 0} capital_garrison_floor={GetConfiguration(invasion)?.MinimumCapitalGarrison ?? 0}"));
            }
        }

        private static string SkipReason(InvasionReinforcementOutcome outcome) =>
            outcome == InvasionReinforcementOutcome.CapitalUnavailable ? "no_held_capital_to_reinforce" :
            outcome == InvasionReinforcementOutcome.CapitalBesieged ? "capital_besieged" :
            outcome == InvasionReinforcementOutcome.BankFull ? "bank_full" :
            outcome == InvasionReinforcementOutcome.AllWavesSent ? "all_waves_sent" :
            "configuration_changed";

        double IInvasionReinforcementQuery.NowInDays => CampaignTime.Now.ToDays;

        IReadOnlyList<InvasionReinforcementRule>? IInvasionReinforcementQuery.GetRules(Invasion invasion)
        {
            InvasionData? configuration = GetConfiguration(invasion);
            Clan? clan = invasion.FindLeadingClan();

            return configuration == null || clan?.IsEliminated != false
                ? null
                : configuration.ReinforcementRules
                    .Select(rule => new InvasionReinforcementRule(rule.Id, rule.IntervalInDays, rule.MaxWaves))
                    .ToList();
        }

        // Read live for every draw, so a wave just banked counts against the next rule of the same day.
        public InvasionBankLimit GetBankLimit(Invasion invasion)
        {
            InvasionData? configuration = GetConfiguration(invasion);
            Settlement? capital = invasion.CapitalSettlementId == null
                ? null
                : Settlement.Find(invasion.CapitalSettlementId);

            // The per-town bonus is the kingdom's, not the invading clans': a town held by any member of the
            // kingdom raises the limit, however that member came by it.
            return new InvasionBankLimit(configuration?.MaxCapitalGarrison ?? 0,
                configuration?.CapitalGarrisonPerTownHeld ?? 0, _invasionLords.CountTownsHeld(invasion),
                capital?.Town?.GarrisonParty?.MemberRoster.TotalManCount ?? 0);
        }

        bool IInvasionReinforcementQuery.IsCapitalBesieged(Invasion invasion) =>
            invasion.CapitalSettlementId != null && Settlement.Find(invasion.CapitalSettlementId)?.IsUnderSiege == true;

        public Func<Settlement, bool>? FindInitialCoastalTargetFilter(MobileParty party)
        {
            var targetFilter = _invasionState.FindInitialTargetFilter(party.StringId, party.ActualClan?.StringId,
                spawnTemplateId =>
                {
                    InvasionData? configuration = _spawnDao.FindByPartyTemplateId(spawnTemplateId)?.Invasion;
                    return (configuration?.FirstConquestMustBeSupplyBase == true,
                        InvasionSupplyLines.Find(configuration?.SupplyLine));
                },
                supplyLine => Settlement.All.Any(settlement => settlement.MapFaction == party.MapFaction &&
                    supplyLine.IsSupplyBase(_invasionSupplyBases.GetFacts(settlement))));

            if (targetFilter == null)
            {
                return null;
            }

            // An invasion launched against a kingdom before it holds a fief lands on that kingdom's coast and
            // nobody else's, as long as that kingdom has a coast left to land on.
            Invasion? invasion = _invasionState.FindInvasionOfParty(party.StringId);
            string? targetKingdomId = invasion == null || party.MapFaction.Fiefs.Count > 0
                ? null
                : _invasionConquestTargets.FindConquestTargetKingdomId(invasion);
            bool landsOnTarget = targetKingdomId != null && Settlement.All.Any(settlement =>
                settlement.MapFaction?.StringId == targetKingdomId &&
                targetFilter(settlement.StringId, _invasionSupplyBases.GetFacts(settlement)));

            return settlement => (!landsOnTarget || settlement.MapFaction?.StringId == targetKingdomId) &&
                                 targetFilter(settlement.StringId, _invasionSupplyBases.GetFacts(settlement));
        }

        private InvasionData? GetConfiguration(Invasion invasion) =>
            _spawnDao.FindByPartyTemplateId(invasion.SpawnTemplateId)?.Invasion;
    }
}
