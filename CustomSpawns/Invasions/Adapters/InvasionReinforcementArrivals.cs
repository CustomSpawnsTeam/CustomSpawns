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

                    // A strong invasion, or one whose stream has run dry, is sent no men, but its army still has
                    // to be paid till the next slot.
                    if (draw.Outcome == InvasionReinforcementOutcome.FactionStrengthReached ||
                        draw.Outcome == InvasionReinforcementOutcome.AllWavesSent)
                    {
                        _invasionTroopBanking.PayOnly(draw);
                    }
                }
                else if (_invasionTroopBanking.Bank(draw))
                {
                    _invasionReinforcements.RecordDelivered(draw);
                }
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

        /**
         * Vanilla's own measure of what a faction fields -- each party's estimated strength, summed over
         * the war parties and the garrisons, which is where a banked wave goes -- narrowed to the clans of
         * this invasion and to what the waves themselves raised. The spawner keeps landing its own forces
         * under the founding clan whatever the waves do, so those parties are left out: counted, they alone
         * hold the invasion over its ceiling and no wave would ever come. Summed live, so that a wave banked
         * a moment ago already counts against the next rule of the same day.
         */
        InvasionStrengthBudget IInvasionReinforcementQuery.GetStrengthBudget(Invasion invasion)
        {
            InvasionData? configuration = GetConfiguration(invasion);
            double currentStrength = 0;

            foreach (Clan clan in Clan.All.Where(clan => invasion.HoldsClan(clan.StringId)))
            {
                // The spawner's own forces are left out; the lords the waves field are adopted, and count
                // like any other.
                currentStrength += clan.WarPartyComponents
                    .Where(component => _dynamicSpawnData.GetDynamicSpawnData(component.MobileParty)?.IsAdopted != false)
                    .Sum(component => component.MobileParty.Party.EstimatedStrength);
                currentStrength += clan.Fiefs.Sum(fief => fief.GarrisonParty?.Party.EstimatedStrength ?? 0f);
            }

            // The per-town bonus is the kingdom's, not the invading clans': a town held by any member of the
            // kingdom raises the ceiling, however that member came by it.
            return new InvasionStrengthBudget(configuration?.MaxFactionStrength ?? 0,
                configuration?.StrengthPerTownHeld ?? 0, _invasionLords.CountTownsHeld(invasion), currentStrength);
        }

        public Func<Settlement, bool>? FindInitialCoastalTargetFilter(MobileParty party)
        {
            Invasion? invasion = _invasionState.FindInvasionOfParty(party.StringId);

            if (invasion == null || invasion.IsSuspended || !invasion.HoldsClan(party.ActualClan?.StringId))
            {
                return null;
            }

            // Losing the founding capital is stronger than the optional first-coastal-conquest rule:
            // every party in the invasion is forced back onto that one objective, and the existing
            // offensive policy pools those parties into the same attacking force.
            if (invasion.CapitalSettlementId != null && !_invasionState.HoldsCapital(invasion))
            {
                string capitalId = invasion.CapitalSettlementId;
                return settlement => settlement.StringId == capitalId;
            }

            InvasionData? configuration = GetConfiguration(invasion);

            // An invasion that depends on no supply base has no base to go back for, so it is left to pick
            // its own targets even where the configuration asks for the restriction.
            if (configuration?.RequiresFirstCoastalConquest != true ||
                !_invasionSupplyBases.DependsOnASupplyBase(configuration))
            {
                return null;
            }

            bool factionHasSupplyBase = Settlement.All.Any(settlement =>
                settlement.MapFaction == party.MapFaction &&
                _invasionSupplyBases.IsSupplyBase(settlement, configuration));

            return factionHasSupplyBase
                ? null
                : settlement => _invasionSupplyBases.IsSupplyBase(settlement, configuration);
        }

        private InvasionData? GetConfiguration(Invasion invasion) =>
            _spawnDao.FindByPartyTemplateId(invasion.SpawnTemplateId)?.Invasion;
    }
}
