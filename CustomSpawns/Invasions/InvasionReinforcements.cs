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
        FactionStrengthReached,
        NoLandingSite,
        Landing
    }

    /**
     * What bounds how much an invasion may still field: its configured ceiling, what each town it holds
     * adds to that ceiling, the towns it holds now, and the strength it already has on the map.
     */
    internal readonly struct InvasionStrengthBudget
    {
        internal InvasionStrengthBudget(double maxFactionStrength, double strengthPerTownHeld, int townsHeld,
            double currentStrength)
        {
            MaxFactionStrength = maxFactionStrength;
            StrengthPerTownHeld = strengthPerTownHeld;
            TownsHeld = townsHeld;
            CurrentStrength = currentStrength;
        }

        internal double MaxFactionStrength { get; }
        internal double StrengthPerTownHeld { get; }
        internal int TownsHeld { get; }
        internal double CurrentStrength { get; }
    }

    internal readonly struct InvasionReinforcementDraw
    {
        internal InvasionReinforcementDraw(Invasion invasion, string ruleId,
            InvasionReinforcementOutcome outcome, string? landingSettlementId, string? clanId,
            double permittedStrength = 0)
        {
            Invasion = invasion;
            RuleId = ruleId;
            Outcome = outcome;
            LandingSettlementId = landingSettlementId;
            ClanId = clanId;
            PermittedStrength = permittedStrength;
        }

        internal Invasion Invasion { get; }
        // Which stream drew. The arrival is counted against this rule alone and spawns its template.
        internal string RuleId { get; }
        internal InvasionReinforcementOutcome Outcome { get; }
        internal string? LandingSettlementId { get; }
        // No clan on a landing means every clan of the invasion is full and the arrival founds another one.
        internal string? ClanId { get; }
        /**
         * How much strength this arrival may bring: the headroom left under the invasion's shared ceiling
         * once the earlier rules of the same day have taken theirs. Infinite when the invasion is uncapped.
         */
        internal double PermittedStrength { get; }
    }

    // Campaign facts the reinforcement rules read. Bannerlord access stays behind this boundary.
    internal interface IInvasionReinforcementQuery
    {
        double NowInDays { get; }
        // In configuration order. Null when the invasion has nothing left to send with.
        IReadOnlyList<InvasionReinforcementRule>? GetRules(Invasion invasion);
        // Whether a settlement can supply the invasion: wind its clocks, and take its arrivals.
        bool IsSupplyBase(string settlementId);
        // Settlements the invasion holds that can take an arrival.
        IReadOnlyList<string> GetLandingSites(Invasion invasion);
        int CountActiveReinforcements(Invasion invasion, string ruleId);
        // Read again for every draw, so that a party landed a moment ago is already part of the strength.
        InvasionStrengthBudget GetStrengthBudget(Invasion invasion);
        // Vanilla caps war parties per clan tier; a clan at its limit cannot take another arrival.
        bool HasPartySlot(string clanId);
    }

    /**
     * Reinforcements come on a fixed cadence: taking a settlement that can supply the invasion winds one
     * clock per configured rule, and every time one comes due the invasion draws a party at a supply base
     * it still holds. The clocks are kept on the invasion so that they survive a save; every rule that
     * winds or reads one lives here.
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

        // The clocks are wound once, by the first supply base the invasion takes and holds.
        internal Invasion? StartSchedule(long captureId)
        {
            InvasionCapture? capture = _invasionState.FindCapture(captureId);
            Invasion? invasion = _invasionState.FindInvasion(capture?.CapturingInvasionClanId);
            if (capture?.IsConfirmed != true || invasion == null) return null;

            IReadOnlyList<InvasionReinforcementRule>? rules = _invasionReinforcementQuery.GetRules(invasion);
            if (rules == null || !_invasionReinforcementQuery.IsSupplyBase(capture.SettlementId)) return null;

            double nowDays = _invasionReinforcementQuery.NowInDays;
            bool wound = false;

            foreach (InvasionReinforcementRule rule in rules.Where(rule =>
                         invasion.FindReinforcementSchedule(rule.Id) == null))
            {
                invasion.WindReinforcementSchedule(rule.Id, rule.IntervalInDays, nowDays + rule.IntervalInDays);
                wound = true;
            }

            return wound ? invasion : null;
        }

        /**
         * One draw per rule whose clock has come due. The slot is claimed before anything else is checked,
         * so a refused or a failed landing waits for the next interval rather than retrying daily.
         *
         * Drawn one at a time on purpose: the rules of one invasion share a single strength ceiling, and
         * only a party's caller knows what it actually landed with. Each draw is therefore computed after
         * the previous one has been acted on, which is what makes the first rule take its room and the
         * next ones have to make do with what is left.
         */
        internal IEnumerable<InvasionReinforcementDraw> DrawDue()
        {
            double nowDays = _invasionReinforcementQuery.NowInDays;

            foreach (Invasion invasion in new List<Invasion>(_invasionState.Invasions))
            {
                IReadOnlyList<InvasionReinforcementRule> rules = _invasionReinforcementQuery.GetRules(invasion) ??
                                                                 new List<InvasionReinforcementRule>();

                foreach (InvasionReinforcementSchedule schedule in DrawOrder(invasion, rules))
                {
                    if (TryClaimSlot(schedule, nowDays))
                    {
                        yield return Draw(invasion, rules.FirstOrDefault(rule => rule.Id == schedule.RuleId), schedule);
                    }
                }
            }
        }

        /**
         * What the invasion may still field under its shared ceiling. An unconfigured ceiling leaves it
         * unbounded; every town its kingdom holds right now raises the ceiling, and losing one lowers it again.
         */
        internal static double Headroom(InvasionStrengthBudget budget)
        {
            return budget.MaxFactionStrength <= 0
                ? double.PositiveInfinity
                : budget.MaxFactionStrength + budget.StrengthPerTownHeld * budget.TownsHeld - budget.CurrentStrength;
        }

        /**
         * How much of an arrival fits in the headroom left. An arrival worth more than that is cut back to
         * its share rather than turned away, down to a single man, which is the edge case this answers.
         */
        internal static int PermittedMemberCount(int memberCount, double partyStrength, double permittedStrength)
        {
            if (partyStrength <= permittedStrength || partyStrength <= 0)
            {
                return memberCount;
            }

            return Math.Max(1, (int)Math.Floor(memberCount * permittedStrength / partyStrength));
        }

        /**
         * Rules draw in configuration order, so that an invasion configured with several of them is
         * contended in the order it was written. A clock whose rule the configuration no longer declares
         * draws last, and only to spend its slot.
         */
        private static IReadOnlyList<InvasionReinforcementSchedule> DrawOrder(Invasion invasion,
            IReadOnlyList<InvasionReinforcementRule> rules)
        {
            var ordered = rules.Select(rule => invasion.FindReinforcementSchedule(rule.Id))
                .Where(schedule => schedule != null).Select(schedule => schedule!).ToList();
            ordered.AddRange(invasion.ReinforcementSchedules.Where(schedule =>
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

        private InvasionReinforcementDraw Draw(Invasion invasion, InvasionReinforcementRule? rule,
            InvasionReinforcementSchedule schedule)
        {
            if (rule == null || invasion.IsSuspended)
                return new InvasionReinforcementDraw(invasion, schedule.RuleId,
                    InvasionReinforcementOutcome.Unavailable, null, null);
            if (_invasionReinforcementQuery.CountActiveReinforcements(invasion, rule.Id) >= rule.MaxConcurrentParties)
                return new InvasionReinforcementDraw(invasion, rule.Id,
                    InvasionReinforcementOutcome.MaxConcurrentPartiesReached, null, null);

            double headroom = Headroom(_invasionReinforcementQuery.GetStrengthBudget(invasion));
            if (headroom <= 0)
                return new InvasionReinforcementDraw(invasion, rule.Id,
                    InvasionReinforcementOutcome.FactionStrengthReached, null, null);

            IReadOnlyList<string> landingSites = _invasionReinforcementQuery.GetLandingSites(invasion);
            // The arrival joins the first clan of the invasion that still has room for a war party.
            return landingSites.Count == 0
                ? new InvasionReinforcementDraw(invasion, rule.Id, InvasionReinforcementOutcome.NoLandingSite, null, null)
                : new InvasionReinforcementDraw(invasion, rule.Id, InvasionReinforcementOutcome.Landing,
                    landingSites[_selectIndex(landingSites.Count)],
                    invasion.ClanIds.FirstOrDefault(_invasionReinforcementQuery.HasPartySlot), headroom);
        }
    }
}
