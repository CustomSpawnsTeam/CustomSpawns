using System;
using System.Collections.Generic;
using System.Linq;
using CustomSpawns.Data.Dao;
using CustomSpawns.Data.Dto;
using CustomSpawns.Spawn;
using NLog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace CustomSpawns.Invasions.Adapters
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
        private readonly DynamicSpawnData _dynamicSpawnData;
        private readonly IInvasionLords _invasionLords;

        public InvasionReinforcementArrivals(InvasionState state, SpawnDao spawns, DynamicSpawnData parties,
            InvasionSupplyBases supplyBases, InvasionTroopBanking troopBanking, IInvasionLords invasionLords)
        {
            _invasionState = state;
            _spawnDao = spawns;
            _dynamicSpawnData = parties;
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
                InvasionStrengthBudget budget = GetStrengthBudget(invasion);
                Kingdom? kingdom = clans.Select(clan => clan.Kingdom).FirstOrDefault(value => value != null);
                int castles = kingdom?.Fiefs.Count(fief => fief.IsCastle) ??
                              clans.Sum(clan => clan.Fiefs.Count(fief => fief.IsCastle));
                Settlement? capital = invasion.CapitalSettlementId == null
                    ? null
                    : Settlement.Find(invasion.CapitalSettlementId);

                Logger.Info(FormattableString.Invariant(
                                $"Invasion strength summary: cadence={cadence} day={CampaignTime.Now.ToDays:0.00} invasion={invasion.ClanId} kingdom={kingdom?.StringId ?? "none"} clans={clans.Length} suspended={invasion.IsSuspended}") +
                            FormattableString.Invariant(
                                $" towns={budget.TownsHeld} castles={castles} faction_strength={statistics.Strength + garrisonStrength:0.0} budget_strength={budget.CurrentStrength:0.0} strength_limit={budget.CurrentStrength + budget.Headroom:0.0} headroom={budget.Headroom:0.0}") +
                            FormattableString.Invariant(
                                $" field_strength={statistics.Strength:0.0} field_parties={statistics.Count} field_troops={statistics.Men} field_healthy={statistics.Healthy} party_size_avg={statistics.AverageSize:0.0} party_size_min={statistics.MinimumSize} party_size_max={statistics.MaximumSize} party_limit_avg={statistics.AverageLimit:0.0} party_fill_percent={statistics.FillPercent:0.0}") +
                            FormattableString.Invariant(
                                $" living_lords={_invasionLords.CountLivingLords(invasion)} garrison_troops={garrisons.Sum(party => party.MemberRoster.TotalManCount)} garrison_strength={garrisonStrength:0.0} capital={invasion.CapitalSettlementId ?? "none"} capital_held={_invasionState.HoldsCapital(invasion)} capital_besieged={capital?.IsUnderSiege == true} capital_garrison={capital?.Town?.GarrisonParty?.MemberRoster.TotalManCount ?? 0} capital_garrison_floor={GetConfiguration(invasion)?.MinimumCapitalGarrison ?? 0}"));
            }
        }

        private static string SkipReason(InvasionReinforcementOutcome outcome) =>
            outcome == InvasionReinforcementOutcome.CapitalUnavailable ? "no_held_capital_to_reinforce" :
            outcome == InvasionReinforcementOutcome.FactionStrengthReached ? "faction_strength" :
            outcome == InvasionReinforcementOutcome.AllWavesSent ? "all_waves_sent" :
            "configuration_changed";

        double IInvasionReinforcementQuery.NowInDays => CampaignTime.Now.ToDays;

        IReadOnlyList<InvasionReinforcementRule>? IInvasionReinforcementQuery.GetRules(Invasion invasion)
        {
            InvasionData? configuration = GetConfiguration(invasion);
            Clan? clan = Clan.FindFirst(candidate => candidate.StringId == invasion.ClanId);

            return configuration == null || clan?.IsEliminated != false
                ? null
                : configuration.ReinforcementRules
                    .Select(rule => new InvasionReinforcementRule(rule.Id, rule.IntervalInDays, rule.MaxWaves))
                    .ToList();
        }

        // Read live for every draw, so a wave just banked counts against the next rule of the same day.
        public InvasionStrengthBudget GetStrengthBudget(Invasion invasion)
        {
            InvasionData? configuration = GetConfiguration(invasion);
            double currentStrength = invasion.MeasureReinforcementStrength(Clan.All.Select(clan =>
                (clan.StringId,
                    clan.WarPartyComponents.Select(component =>
                        (_dynamicSpawnData.GetDynamicSpawnData(component.MobileParty)?.IsAdopted,
                            (Func<float>)(() => component.MobileParty.Party.EstimatedStrength))),
                    clan.Fiefs.Select(fief => fief.GarrisonParty?.Party.EstimatedStrength ?? 0f))));

            // The per-town bonus is the kingdom's, not the invading clans': a town held by any member of the
            // kingdom raises the ceiling, however that member came by it.
            return new InvasionStrengthBudget(configuration?.MaxFactionStrength ?? 0,
                configuration?.StrengthPerTownHeld ?? 0, _invasionLords.CountTownsHeld(invasion), currentStrength);
        }

        public Func<Settlement, bool>? FindInitialCoastalTargetFilter(MobileParty party)
        {
            var targetFilter = _invasionState.FindInitialTargetFilter(party.StringId, party.ActualClan?.StringId,
                spawnTemplateId =>
                {
                    InvasionData? configuration = _spawnDao.FindByPartyTemplateId(spawnTemplateId)?.Invasion;
                    return (configuration?.RequiresFirstCoastalConquest == true,
                        InvasionSupplyLines.Find(configuration?.SupplyLine));
                },
                supplyLine => Settlement.All.Any(settlement => settlement.MapFaction == party.MapFaction &&
                    supplyLine.IsSupplyBase(_invasionSupplyBases.GetFacts(settlement))));

            return targetFilter == null
                ? null
                : settlement => targetFilter(settlement.StringId, _invasionSupplyBases.GetFacts(settlement));
        }

        private InvasionData? GetConfiguration(Invasion invasion) =>
            _spawnDao.FindByPartyTemplateId(invasion.SpawnTemplateId)?.Invasion;
    }
}
