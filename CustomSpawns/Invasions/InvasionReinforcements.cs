using System;
using System.Collections.Generic;
using System.Linq;

namespace CustomSpawns.Invasions
{
    // One configured reinforcement stream of an invasion: its own cadence, and how many waves it may send.
    public sealed class InvasionReinforcementRule
    {
        public InvasionReinforcementRule(string id, double intervalInDays, int maxWaves = 0)
        {
            Id = id;
            IntervalInDays = intervalInDays;
            MaxWaves = maxWaves;
        }

        public string Id { get; }
        public double IntervalInDays { get; }

        // How many waves the stream ever delivers. Zero is what a configuration that does not mention it
        // means: the stream keeps sending for as long as the invasion lives.
        public int MaxWaves { get; }
    }

    public enum InvasionReinforcementOutcome
    {
        // Nothing left to send with: no configuration, a retired spawn template, or no clan.
        Unavailable,
        // No capital to send to: none taken yet, or the one taken has fallen. Nothing arrives while that is true.
        CapitalUnavailable,
        // The stream has delivered every wave it was allowed. It sends no more men, but still pays.
        AllWavesSent,
        FactionStrengthReached,
        // The wave is paid into the capital's garrison, which is the only place reinforcements go.
        Banking
    }

    /**
     * What bounds how much an invasion may still field: its configured ceiling, what each town it holds
     * adds to that ceiling, the towns it holds now, and the strength it already has on the map.
     */
    public readonly struct InvasionStrengthBudget
    {
        public InvasionStrengthBudget(double maxFactionStrength, double strengthPerTownHeld, int townsHeld,
            double currentStrength)
        {
            MaxFactionStrength = maxFactionStrength;
            StrengthPerTownHeld = strengthPerTownHeld;
            TownsHeld = townsHeld;
            CurrentStrength = currentStrength;
        }

        public double MaxFactionStrength { get; }
        public double StrengthPerTownHeld { get; }
        public int TownsHeld { get; }
        public double CurrentStrength { get; }

        /**
         * What the invasion may still field under its shared ceiling. An unconfigured ceiling leaves it
         * unbounded; every town its kingdom holds right now raises the ceiling, and losing one lowers it again.
         */
        public double Headroom => MaxFactionStrength <= 0
            ? double.PositiveInfinity
            : MaxFactionStrength + StrengthPerTownHeld * TownsHeld - CurrentStrength;
    }

    public readonly struct InvasionReinforcementDraw
    {
        public InvasionReinforcementDraw(Invasion invasion, string ruleId,
            InvasionReinforcementOutcome outcome)
        {
            Invasion = invasion;
            RuleId = ruleId;
            Outcome = outcome;
        }

        public Invasion Invasion { get; }
        // Which stream drew. The wave is rolled off this rule's own party template.
        public string RuleId { get; }
        public InvasionReinforcementOutcome Outcome { get; }
    }

    // Campaign facts the reinforcement rules read. Bannerlord access stays behind this boundary.
    public interface IInvasionReinforcementQuery
    {
        double NowInDays { get; }
        // In configuration order. Null when the invasion has nothing left to send with.
        IReadOnlyList<InvasionReinforcementRule>? GetRules(Invasion invasion);
        // Read again for every draw, so that a wave banked a moment ago is already part of the strength.
        InvasionStrengthBudget GetStrengthBudget(Invasion invasion);
    }

    /**
     * Reinforcements come on a fixed cadence: taking the capital winds one clock per configured rule, and
     * every time one comes due the invasion is sent a wave of men for that capital. The clocks are kept on
     * the invasion so that they survive a save; every rule that winds or reads one lives here.
     */
    public sealed class InvasionReinforcements
    {
        private readonly InvasionState _invasionState;
        private readonly IInvasionReinforcementQuery _invasionReinforcementQuery;

        public InvasionReinforcements(InvasionState state, IInvasionReinforcementQuery campaign)
        {
            _invasionState = state;
            _invasionReinforcementQuery = campaign;
        }

        // The clocks are wound once, by the capture that gave the invasion its capital.
        public Invasion? StartSchedule(long captureId)
        {
            InvasionCapture? capture = _invasionState.FindCapture(captureId);
            Invasion? invasion = _invasionState.FindInvasion(capture?.CapturingInvasionClanId);
            if (capture?.IsConfirmed != true || invasion == null) return null;

            IReadOnlyList<InvasionReinforcementRule>? rules = _invasionReinforcementQuery.GetRules(invasion);
            if (rules == null || capture.SettlementId != invasion.CapitalSettlementId) return null;

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
         * so a refused wave waits for the next interval rather than retrying daily.
         *
         * Drawn one at a time on purpose: the rules of one invasion share a single strength ceiling, and
         * each draw is computed only after the previous one has been paid into the garrison, so that the
         * first rule of a day takes its room and the next ones have to make do with what is left. Walking
         * the draws is therefore what spends the slots: it is walked once, by the caller that pays them in.
         */
        public IEnumerable<InvasionReinforcementDraw> DrawDue()
        {
            double nowDays = _invasionReinforcementQuery.NowInDays;

            foreach (Invasion invasion in new List<Invasion>(_invasionState.Invasions))
            {
                IReadOnlyList<InvasionReinforcementRule> rules = _invasionReinforcementQuery.GetRules(invasion) ??
                                                                 new List<InvasionReinforcementRule>();

                foreach (InvasionReinforcementSchedule schedule in DrawOrder(invasion, rules))
                {
                    InvasionReinforcementRule? rule = rules.FirstOrDefault(candidate =>
                        candidate.Id == schedule.RuleId);

                    if (!TryClaimSlot(schedule, nowDays))
                    {
                        continue;
                    }

                    yield return Draw(invasion, rule, schedule);
                }
            }
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

        /**
         * Only waves that actually arrive are counted, so an invasion whose one wave fell due while its
         * capital was lost, or while it was already at full strength, keeps that wave and is sent it
         * an interval later instead of losing it to timing it had no hand in.
         */
        private static bool HasWavesLeft(InvasionReinforcementRule? rule,
            InvasionReinforcementSchedule schedule) =>
            rule == null || rule.MaxWaves <= 0 || schedule.WavesSent < rule.MaxWaves;

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
                    InvasionReinforcementOutcome.Unavailable);

            /**
             * A wave has one destination: the capital's own garrison, and losing the capital is the only
             * thing that stops the waves. An invasion that has yet to take a capital, or that has lost the
             * one it took, is sent nothing and has to fight on with what it landed with; a besieged capital
             * is still held, and is reinforced like any other. The clock runs through all of it, so nothing
             * accumulates to be paid later.
             */
            if (invasion.CapitalSettlementId == null || !_invasionState.HoldsCapital(invasion))
                return new InvasionReinforcementDraw(invasion, rule.Id,
                    InvasionReinforcementOutcome.CapitalUnavailable);

            // A stream that has delivered all the waves it was allowed sends no more men, but the army it
            // brought still has to be paid for as long as the capital stands.
            if (!HasWavesLeft(rule, schedule))
                return new InvasionReinforcementDraw(invasion, rule.Id, InvasionReinforcementOutcome.AllWavesSent);

            // The bank itself is bottomless. This is the one valve: waves stop once the invasion is as
            // strong as it is allowed to be, and start again as soon as it is not.
            if (_invasionReinforcementQuery.GetStrengthBudget(invasion).Headroom <= 0)
                return new InvasionReinforcementDraw(invasion, rule.Id,
                    InvasionReinforcementOutcome.FactionStrengthReached);

            return new InvasionReinforcementDraw(invasion, rule.Id, InvasionReinforcementOutcome.Banking);
        }

        /**
         * A wave counts only once it has actually been paid in. One the capital could not take after all --
         * its garrison party gone with the men that were in it -- is kept, so a rule allowed a single wave
         * still owes it and sends it an interval later.
         */
        public void RecordDelivered(InvasionReinforcementDraw draw)
        {
            InvasionReinforcementSchedule? schedule = draw.Invasion.FindReinforcementSchedule(draw.RuleId);

            if (schedule != null)
            {
                schedule.WavesSent++;
            }
        }
    }
}
