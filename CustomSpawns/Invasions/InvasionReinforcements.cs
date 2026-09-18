using System;
using System.Collections.Generic;

namespace CustomSpawns.Invasions
{
    // Configured cadence and ceiling of an invasion's reinforcement stream.
    internal sealed class InvasionReinforcementSettings
    {
        internal InvasionReinforcementSettings(double intervalInDays, int activePartyCap)
        {
            IntervalInDays = intervalInDays;
            ActivePartyCap = activePartyCap;
        }

        internal double IntervalInDays { get; }
        internal int ActivePartyCap { get; }
    }

    internal enum InvasionReinforcementOutcome
    {
        // Nothing left to send with: no configuration, a retired spawn template, or no clan.
        Unavailable,
        ActivePartyCapReached,
        NoLandingSite,
        Landing
    }

    internal readonly struct InvasionReinforcementDraw
    {
        internal InvasionReinforcementDraw(InvasionGroupState group, InvasionReinforcementOutcome outcome,
            string? landingSettlementId)
        {
            Group = group;
            Outcome = outcome;
            LandingSettlementId = landingSettlementId;
        }

        internal InvasionGroupState Group { get; }
        internal InvasionReinforcementOutcome Outcome { get; }
        internal string? LandingSettlementId { get; }
    }

    // Campaign facts the reinforcement rules read. Bannerlord access stays behind this boundary.
    internal interface IInvasionReinforcementQuery
    {
        double NowInDays { get; }
        InvasionReinforcementSettings? GetSettings(InvasionGroupState group);
        bool IsCoastalTown(string settlementId);
        // Credited towns the group still holds that can take a landing.
        IReadOnlyList<string> GetLandingSites(InvasionGroupState group);
        int CountActiveReinforcements(InvasionGroupState group);
    }

    /**
     * Reinforcements come by sea on a fixed cadence: taking a coastal town winds one clock per invasion,
     * and every time it comes due the invasion draws a party at a port it still holds. The clock is kept
     * on the group so that it survives a save; every rule that winds or reads it lives here.
     */
    internal sealed class InvasionReinforcements
    {
        private readonly InvasionState _state;
        private readonly IInvasionReinforcementQuery _campaign;
        private readonly Func<int, int> _selectIndex;

        internal InvasionReinforcements(InvasionState state, IInvasionReinforcementQuery campaign,
            Func<int, int> selectIndex)
        {
            _state = state;
            _campaign = campaign;
            _selectIndex = selectIndex;
        }

        // The clock is wound once, by the first coastal town the invasion takes and holds.
        internal InvasionGroupState? StartSchedule(long captureContextId)
        {
            InvasionCaptureContext? context = _state.FindCapture(captureContextId);
            InvasionGroupState? group = _state.FindGroup(context?.OwnershipGroupId);
            if (context?.Joined != true || group == null || group.NextReinforcementDeadlineDays != 0) return null;

            double intervalDays = _campaign.GetSettings(group)?.IntervalInDays ?? 0;
            if (intervalDays <= 0 || !_campaign.IsCoastalTown(context.SettlementId)) return null;

            group.ReinforcementIntervalDays = intervalDays;
            group.NextReinforcementDeadlineDays = _campaign.NowInDays + intervalDays;
            return group;
        }

        /**
         * One draw per invasion whose clock has come due. The slot is claimed before anything else is
         * checked, so a refused or a failed landing waits for the next interval rather than retrying daily.
         */
        internal IReadOnlyList<InvasionReinforcementDraw> DrawDue()
        {
            double nowDays = _campaign.NowInDays;
            var draws = new List<InvasionReinforcementDraw>();
            foreach (InvasionGroupState group in new List<InvasionGroupState>(_state.Groups))
                if (TryClaimSlot(group, nowDays)) draws.Add(Draw(group));
            return draws;
        }

        private static bool TryClaimSlot(InvasionGroupState group, double nowDays)
        {
            if (group.NextReinforcementDeadlineDays == 0 || group.NextReinforcementDeadlineDays > nowDays ||
                group.ReinforcementIntervalDays <= 0)
            {
                return false;
            }

            group.NextReinforcementDeadlineDays = nowDays + group.ReinforcementIntervalDays;
            return true;
        }

        private InvasionReinforcementDraw Draw(InvasionGroupState group)
        {
            InvasionReinforcementSettings? settings = _campaign.GetSettings(group);
            if (settings == null || group.IsSuspended)
                return new InvasionReinforcementDraw(group, InvasionReinforcementOutcome.Unavailable, null);
            if (_campaign.CountActiveReinforcements(group) >= settings.ActivePartyCap)
                return new InvasionReinforcementDraw(group, InvasionReinforcementOutcome.ActivePartyCapReached, null);

            IReadOnlyList<string> landingSites = _campaign.GetLandingSites(group);
            return landingSites.Count == 0
                ? new InvasionReinforcementDraw(group, InvasionReinforcementOutcome.NoLandingSite, null)
                : new InvasionReinforcementDraw(group, InvasionReinforcementOutcome.Landing,
                    landingSites[_selectIndex(landingSites.Count)]);
        }
    }
}
