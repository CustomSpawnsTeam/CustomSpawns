using System;
using System.Collections.Generic;
using System.Linq;

namespace CustomSpawns.Invasions
{
    // One configured reinforcement stream of an invasion: its own cadence and its own ceiling.
    internal sealed class InvasionReinforcementRule
    {
        internal InvasionReinforcementRule(string id, double intervalInDays, int maxConcurrentParties)
        {
            Id = id;
            IntervalInDays = intervalInDays;
            MaxConcurrentParties = maxConcurrentParties;
        }

        internal string Id { get; }
        internal double IntervalInDays { get; }
        internal int MaxConcurrentParties { get; }
    }

    internal enum InvasionReinforcementOutcome
    {
        // Nothing left to send with: no configuration, a retired spawn template, or no clan.
        Unavailable,
        MaxConcurrentPartiesReached,
        NoLandingSite,
        Landing
    }

    internal readonly struct InvasionReinforcementDraw
    {
        internal InvasionReinforcementDraw(InvasionGroupState group, string ruleId,
            InvasionReinforcementOutcome outcome, string? landingSettlementId, string? clanId)
        {
            Group = group;
            RuleId = ruleId;
            Outcome = outcome;
            LandingSettlementId = landingSettlementId;
            ClanId = clanId;
        }

        internal InvasionGroupState Group { get; }
        // Which stream drew. The arrival is counted against this rule alone and spawns its template.
        internal string RuleId { get; }
        internal InvasionReinforcementOutcome Outcome { get; }
        internal string? LandingSettlementId { get; }
        // No clan on a landing means every clan of the invasion is full and the arrival founds another one.
        internal string? ClanId { get; }
    }

    // Campaign facts the reinforcement rules read. Bannerlord access stays behind this boundary.
    internal interface IInvasionReinforcementQuery
    {
        double NowInDays { get; }
        // In configuration order. Null when the invasion has nothing left to send with.
        IReadOnlyList<InvasionReinforcementRule>? GetRules(InvasionGroupState group);
        bool IsCoastalTown(string settlementId);
        // Towns the group holds that can take a landing.
        IReadOnlyList<string> GetLandingSites(InvasionGroupState group);
        int CountActiveReinforcements(InvasionGroupState group, string ruleId);
        // Vanilla caps war parties per clan tier; a clan at its limit cannot take another arrival.
        bool HasPartySlot(string clanId);
    }

    /**
     * Reinforcements come by sea on a fixed cadence: taking a coastal town winds one clock per configured
     * rule, and every time one comes due the invasion draws a party at a port it still holds. The clocks
     * are kept on the group so that they survive a save; every rule that winds or reads one lives here.
     */
    internal sealed class InvasionReinforcements
    {
        private readonly InvasionState _invasionState;
        private readonly IInvasionReinforcementQuery _invasionReinforcementQuery;
        private readonly Func<int, int> _selectIndex;

        internal InvasionReinforcements(InvasionState state, IInvasionReinforcementQuery campaign,
            Func<int, int> selectIndex)
        {
            _invasionState = state;
            _invasionReinforcementQuery = campaign;
            _selectIndex = selectIndex;
        }

        // The clocks are wound once, by the first coastal town the invasion takes and holds.
        internal InvasionGroupState? StartSchedule(long captureId)
        {
            InvasionCapture? capture = _invasionState.FindCapture(captureId);
            InvasionGroupState? group = _invasionState.FindGroup(capture?.CapturingGroupId);
            if (capture?.IsConfirmed != true || group == null) return null;

            IReadOnlyList<InvasionReinforcementRule>? rules = _invasionReinforcementQuery.GetRules(group);
            if (rules == null || !_invasionReinforcementQuery.IsCoastalTown(capture.SettlementId)) return null;

            double nowDays = _invasionReinforcementQuery.NowInDays;
            bool wound = false;

            foreach (InvasionReinforcementRule rule in rules.Where(rule =>
                         group.FindReinforcementSchedule(rule.Id) == null))
            {
                group.WindReinforcementSchedule(rule.Id, rule.IntervalInDays, nowDays + rule.IntervalInDays);
                wound = true;
            }

            return wound ? group : null;
        }

        /**
         * One draw per rule whose clock has come due. The slot is claimed before anything else is checked,
         * so a refused or a failed landing waits for the next interval rather than retrying daily.
         */
        internal IReadOnlyList<InvasionReinforcementDraw> DrawDue()
        {
            double nowDays = _invasionReinforcementQuery.NowInDays;
            var draws = new List<InvasionReinforcementDraw>();

            foreach (InvasionGroupState group in new List<InvasionGroupState>(_invasionState.Groups))
            {
                IReadOnlyList<InvasionReinforcementRule> rules = _invasionReinforcementQuery.GetRules(group) ??
                                                                 new List<InvasionReinforcementRule>();

                foreach (InvasionReinforcementSchedule schedule in DrawOrder(group, rules))
                {
                    if (TryClaimSlot(schedule, nowDays))
                    {
                        draws.Add(Draw(group, rules.FirstOrDefault(rule => rule.Id == schedule.RuleId), schedule));
                    }
                }
            }

            return draws;
        }

        /**
         * Rules draw in configuration order, so that an invasion configured with several of them is
         * contended in the order it was written. A clock whose rule the configuration no longer declares
         * draws last, and only to spend its slot.
         */
        private static IReadOnlyList<InvasionReinforcementSchedule> DrawOrder(InvasionGroupState group,
            IReadOnlyList<InvasionReinforcementRule> rules)
        {
            var ordered = rules.Select(rule => group.FindReinforcementSchedule(rule.Id))
                .Where(schedule => schedule != null).Select(schedule => schedule!).ToList();
            ordered.AddRange(group.ReinforcementSchedules.Where(schedule =>
                rules.All(rule => rule.Id != schedule.RuleId)));
            return ordered;
        }

        private static bool TryClaimSlot(InvasionReinforcementSchedule schedule, double nowDays)
        {
            if (schedule.NextDeadlineDays > nowDays)
            {
                return false;
            }

            schedule.NextDeadlineDays = nowDays + schedule.IntervalDays;
            return true;
        }

        private InvasionReinforcementDraw Draw(InvasionGroupState group, InvasionReinforcementRule? rule,
            InvasionReinforcementSchedule schedule)
        {
            if (rule == null || group.IsSuspended)
                return new InvasionReinforcementDraw(group, schedule.RuleId,
                    InvasionReinforcementOutcome.Unavailable, null, null);
            if (_invasionReinforcementQuery.CountActiveReinforcements(group, rule.Id) >= rule.MaxConcurrentParties)
                return new InvasionReinforcementDraw(group, rule.Id,
                    InvasionReinforcementOutcome.MaxConcurrentPartiesReached, null, null);

            IReadOnlyList<string> landingSites = _invasionReinforcementQuery.GetLandingSites(group);
            // The arrival joins the first clan of the invasion that still has room for a war party.
            return landingSites.Count == 0
                ? new InvasionReinforcementDraw(group, rule.Id, InvasionReinforcementOutcome.NoLandingSite, null, null)
                : new InvasionReinforcementDraw(group, rule.Id, InvasionReinforcementOutcome.Landing,
                    landingSites[_selectIndex(landingSites.Count)],
                    group.ClanIds.FirstOrDefault(_invasionReinforcementQuery.HasPartySlot));
        }
    }
}
