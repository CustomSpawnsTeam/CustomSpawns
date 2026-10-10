using System;
using System.Collections.Generic;
using System.Linq;
using CustomSpawns.Data.Dao;
using CustomSpawns.Data.Dto;
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
    internal sealed class InvasionReinforcementArrivals : IInvasionReinforcementQuery
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly InvasionState _invasionState;
        private readonly SpawnDao _spawnDao;
        private readonly InvasionSupplyBases _invasionSupplyBases;
        private readonly InvasionReinforcements _invasionReinforcements;
        private readonly InvasionTroopBanking _invasionTroopBanking;

        internal InvasionReinforcementArrivals(InvasionState state, SpawnDao spawns,
            InvasionSupplyBases supplyBases, InvasionTroopBanking troopBanking)
        {
            _invasionState = state;
            _spawnDao = spawns;
            _invasionSupplyBases = supplyBases;
            _invasionTroopBanking = troopBanking;
            _invasionReinforcements = new InvasionReinforcements(state, this);
        }

        internal void StartSchedule(long captureId)
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

        internal void SendDue()
        {
            foreach (InvasionReinforcementDraw draw in _invasionReinforcements.DrawDue())
            {
                if (draw.Outcome != InvasionReinforcementOutcome.Banking)
                {
                    Logger.Info("Invasion reinforcement slot skipped: invasion=" + draw.Invasion.ClanId +
                                " rule=" + draw.RuleId + " reason=" + SkipReason(draw.Outcome));
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

        bool IInvasionReinforcementQuery.IsUnderSiege(string settlementId) =>
            Settlement.Find(settlementId)?.SiegeEvent != null;

        /**
         * Vanilla's own measure of what a faction fields -- the parties on the map plus the garrisons,
         * which is where a banked wave goes -- narrowed to the clans of this invasion. The measure is
         * vanilla's to define, so it is read off the clan rather than summed again here. It is a cached
         * total, and refreshed first the way vanilla refreshes it before reading it, so that a wave banked
         * a moment ago already counts against the next rule of the same day.
         */
        InvasionStrengthBudget IInvasionReinforcementQuery.GetStrengthBudget(Invasion invasion)
        {
            InvasionData? configuration = GetConfiguration(invasion);
            List<Clan> clans = Clan.All.Where(clan => invasion.HoldsClan(clan.StringId)).ToList();
            double currentStrength = 0;

            foreach (Clan clan in clans)
            {
                clan.UpdateCurrentStrength();
                currentStrength += clan.CurrentTotalStrength;
            }

            // The per-town bonus is the kingdom's, not the invading clans': a town held by any member of the
            // kingdom raises the ceiling, however that member came by it. The engine keeps the kingdom's fief
            // list live, so a town lost stops counting the moment it changes hands.
            Kingdom? kingdom = clans.Select(clan => clan.Kingdom).FirstOrDefault(candidate => candidate != null);
            int townsHeld = kingdom == null
                ? clans.Sum(clan => clan.Fiefs.Count(fief => fief.IsTown))
                : kingdom.Fiefs.Count(fief => fief.IsTown);

            return new InvasionStrengthBudget(configuration?.MaxFactionStrength ?? 0,
                configuration?.StrengthPerTownHeld ?? 0, townsHeld, currentStrength);
        }

        internal Func<Settlement, bool>? FindInitialCoastalTargetFilter(MobileParty party)
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
