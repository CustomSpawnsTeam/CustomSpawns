using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CustomSpawns.AI.Adapters;
using CustomSpawns.AI.Model;
using CustomSpawns.CampaignData.Implementations;
using CustomSpawns.Spawn.PartyComponents;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace CustomSpawns.UtilityBehaviours
{
    public class AiDecisionLoggerBehaviour : CampaignBehaviorBase
    {
        private readonly DailyLogger _dailyLogger;
        private readonly PartyStateStore<SettlementOffensiveBehaviourState> _siegeStates;
        private readonly Func<MobileParty, IReadOnlyList<MobileParty>> _findAttackingForce;
        private readonly Func<MobileParty, IEnumerable<MobileParty>> _findAccompanyingParties;
        private readonly Dictionary<MobileParty, string> _previousStates = new();

        internal AiDecisionLoggerBehaviour(DailyLogger dailyLogger,
            PartyStateStore<SettlementOffensiveBehaviourState> siegeStates,
            Func<MobileParty, IReadOnlyList<MobileParty>> findAttackingForce,
            Func<MobileParty, IEnumerable<MobileParty>> findAccompanyingParties)
        {
            _dailyLogger = dailyLogger;
            _siegeStates = siegeStates;
            _findAttackingForce = findAttackingForce;
            _findAccompanyingParties = findAccompanyingParties;
        }

        public override void RegisterEvents()
        {
            CampaignEvents.TickPartialHourlyAiEvent.AddNonSerializedListener(this, OnPartialHourlyAiTick);
            CampaignEvents.MobilePartyDestroyed.AddNonSerializedListener(this, OnMobilePartyDestroyed);
        }

        public override void SyncData(IDataStore dataStore)
        {
        }

        private void OnPartialHourlyAiTick(MobileParty party)
        {
            if (party.PartyComponent is not CustomSpawnsPartyComponent)
            {
                return;
            }

            string currentState = DescribeState(party);
            if (!_previousStates.TryGetValue(party, out string previousState))
            {
                _previousStates.Add(party, currentState);
                _dailyLogger.Info("[" + party.StringId + "][" + party.Name + "] AI state observed" +
                    " | source=partial_hourly_ai" +
                    " | " + currentState +
                    DescribeTargetPositions(party) +
                    DescribeObjectiveForce(party));
                return;
            }

            if (previousState == currentState)
            {
                return;
            }

            _previousStates[party] = currentState;
            _dailyLogger.Info("[" + party.StringId + "][" + party.Name + "] AI state changed" +
                " | source=partial_hourly_ai" +
                " | previous={" + previousState + "}" +
                " | current={" + currentState + "}" +
                DescribeTargetPositions(party) +
                DescribeObjectiveForce(party));
        }

        private void OnMobilePartyDestroyed(MobileParty party, PartyBase destroyer)
        {
            _previousStates.Remove(party);
        }

        /**
         * What a change is judged on. The behaviour target positions are left out: a patrol order moves its
         * point continually, so a gathering party would write a line an hour instead of one per decision.
         * They are reported alongside whenever a change is logged.
         */
        private string DescribeState(MobileParty party)
        {
            return "default_behavior=" + party.DefaultBehavior +
                " | short_term_behavior=" + party.ShortTermBehavior +
                " | default_target_settlement_id=" + GetSettlementId(party.TargetSettlement) +
                " | default_target_party_id=" + GetPartyId(party.TargetParty) +
                " | short_target_settlement_id=" + GetSettlementId(party.ShortTermTargetSettlement) +
                " | short_target_party_id=" + GetPartyId(party.ShortTermTargetParty) +
                DescribeObjective(party);
        }

        private static string DescribeTargetPositions(MobileParty party)
        {
            return " | default_target_position=" + party.TargetPosition +
                " | short_target_position=" + party.AiBehaviorTarget;
        }

        // The objective facts a change is judged on. Distances, strengths and clocks are left out for the
        // same reason the positions are: they move every tick.
        private string DescribeObjective(MobileParty party)
        {
            SettlementOffensiveBehaviourState? state = _siegeStates.Find(party);
            if (state is null)
            {
                return "";
            }

            // Whether the party has come up to the gate is deliberately not among these facts: it flips as
            // the party walks the last stretch, which would log a line an hour for a force whose whole job
            // is to stand still and wait. The force description carries it instead.
            return " | objective_phase=" + state.Phase +
                " | objective_id=" + GetSettlementId(state.Target);
        }

        /**
         * What the gathering is doing at the moment of a change: the parties that have recorded this
         * objective, which of them have come up to the gate, and the strengths the decisions read. The
         * decisions themselves are read off the phase -- Gather means the arrived force was still short of
         * the target, Approach means it passed and the assault is going in.
         * The set counted here is the one the decisions weigh: the parties that recorded this objective
         * plus whatever travels with each of them, since an escort never runs the siege behaviour and so
         * records no objective of its own. The one figure this cannot reproduce is an army the deciding
         * party leads, which the pooled assessment counts whole.
         */
        private string DescribeObjectiveForce(MobileParty party)
        {
            SettlementOffensiveBehaviourState? state = _siegeStates.Find(party);
            if (state?.Target is null)
            {
                return "";
            }

            Settlement target = state.Target;
            List<MobileParty> force = _findAttackingForce(party)
                .SelectMany(member => _findAccompanyingParties(member).Prepend(member))
                .Distinct()
                .ToList();
            List<MobileParty> arrived = force.Where(member => HasArrived(member, target)).ToList();

            return " | objective_name=" + target.Name +
                " | objective_parties=" + force.Count +
                " | objective_arrived_parties=" + arrived.Count +
                " | objective_strength=" + FormatNumber(SumStrength(force)) +
                " | objective_arrived_strength=" + FormatNumber(SumStrength(arrived)) +
                " | party_strength=" + FormatNumber(party.Party.CalculateCurrentStrength()) +
                " | distance_to_gate=" + FormatNumber(target.GatePosition.Distance(party.Position)) +
                " | seeing_range=" + FormatNumber(party.SeeingRange) +
                " | operation_days=" + FormatNumber(state.ElapsedOperationDays) +
                "/" + FormatNumber(state.OperationWindowDays) +
                " | recon_days=" + FormatNumber(state.AccumulatedClearReconDays) +
                "/" + FormatNumber(state.RequiredClearReconDays);
        }

        // Arrival as the launch gate counts it: a party within its own sight of the land gate, neither
        // resting in a settlement nor pinned in a battle of its own.
        private static bool HasArrived(MobileParty party, Settlement target)
        {
            return party.CurrentSettlement is null && party.MapEvent is null && !party.IsFleeing() &&
                   OffensiveStrengthRules.IsWithinSeeingRange(
                       target.GatePosition.Distance(party.Position), party.SeeingRange);
        }

        private static float SumStrength(IEnumerable<MobileParty> parties)
        {
            return parties.Sum(member => member.Party.CalculateCurrentStrength());
        }

        private static string FormatNumber(float value)
        {
            return value.ToString("0.00", CultureInfo.InvariantCulture);
        }

        private static string GetSettlementId(Settlement? settlement)
        {
            return settlement?.StringId ?? "none";
        }

        private static string GetPartyId(MobileParty party)
        {
            return party?.StringId ?? "none";
        }
    }
}
