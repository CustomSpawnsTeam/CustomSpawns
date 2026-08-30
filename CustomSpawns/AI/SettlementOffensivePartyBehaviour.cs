using System;
using System.Collections.Generic;
using CustomSpawns.AI.Model;
using CustomSpawns.Data.Dto;
using CustomSpawns.Spawn;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace CustomSpawns.AI
{
    /**
     * The phase sequencing every settlement offensive follows, whichever settlement it attacks and however
     * it attacks it: cooldown, target observation, recon, approach, the operation itself and its recovery.
     * Only the steps that depend on what is being attacked are left to the concrete behaviours.
     */
    public abstract class SettlementOffensivePartyBehaviour<TConfiguration>
        : PriorityPartyBehaviour<TConfiguration> where TConfiguration : SettlementOffensiveData
    {
        private readonly Func<float, float, float> _roll;
        private readonly string _saveKey;
        private Dictionary<MobileParty, SettlementOffensiveBehaviourState> _states = new();

        protected SettlementOffensivePartyBehaviour(Func<float, float, float> roll, string saveKey)
        {
            _roll = roll;
            _saveKey = saveKey;
        }

        private protected abstract IPartyStrengthObserver PartyStrength { get; }

        private protected abstract IOffensiveOperationStatus OperationStatus { get; }

        private protected abstract IPatrolOrders PatrolOrders { get; }

        public override PartyBehaviourResult GetPriorityBehaviour(MobileParty party, CsPartyData partyData,
            TConfiguration configuration)
        {
            if (!_states.TryGetValue(party, out SettlementOffensiveBehaviourState? state))
            {
                state = new SettlementOffensiveBehaviourState();
                _states.Add(party, state);
            }

            if (SettlementOffensiveBehaviour.TickCooldown(state))
            {
                return PartyBehaviourResult.NotApplicable;
            }

            var settings = new SettlementOffensiveSettings(configuration.MinimumRequiredClearTimeInDays,
                configuration.MaximumRequiredClearTimeInDays, configuration.MinimumOperationWindowInDays,
                configuration.MaximumOperationWindowInDays, configuration.GracePeriodInDays,
                configuration.MaximumFailedAttempts, configuration.CooldownInDays);
            SettlementOffensiveBehaviour.BeginEpisode(state, settings, _roll);

            float attackerStrength = PartyStrength.GetAttackerStrength(party);
            float visibleHostileStrength = PartyStrength.GetVisibleHostileStrength(party);
            bool safetyPasses = SettlementOffensiveBehaviour.MeetsStrengthRatio(attackerStrength,
                visibleHostileStrength, configuration.MinimumAttackerToRoamingHostilesRatio);

            if (state.Target is not null)
            {
                Settlement observedTarget = state.Target;
                TrackTarget(state);
                bool matchingOperationActive = IsMatchingOperationActive(party, observedTarget);
                ActiveTargetObservation observation = SettlementOffensiveBehaviour.ObserveTarget(state,
                    IsCurrentTargetValid(party, state, matchingOperationActive),
                    IsTargetComplete(party, observedTarget), matchingOperationActive,
                    OperationStatus.HasNativeContinuation(party, observedTarget),
                    configuration.MaximumFailedAttempts, configuration.CooldownInDays);
                if (observation == ActiveTargetObservation.EnteredCooldown)
                {
                    return PartyBehaviourResult.NotApplicable;
                }
            }

            if (state.Target is not null &&
                ShouldAbandonTarget(party, state.Target, attackerStrength, configuration))
            {
                SettlementOffensiveBehaviour.ClearTarget(state);
            }

            if (state.Phase == SettlementOffensivePhase.Active)
            {
                return Attack(state.Target!, party);
            }

            if (state.Phase == SettlementOffensivePhase.Recovery)
            {
                Settlement? recoveryTarget = state.Target;
                if (!SettlementOffensiveBehaviour.TickRecovery(state))
                {
                    if (IsNativeSafetyInterruption(OperationStatus.GetShortTermBehavior(party)))
                    {
                        return PartyBehaviourResult.ApplicableWithoutBehavior;
                    }
                    return Attack(recoveryTarget!, party);
                }
            }

            if (state.Target is null)
            {
                state.Target = FindTarget(party, attackerStrength, configuration);
                if (state.Target is not null)
                {
                    TrackTarget(state);
                    float targetRatio = StrengthRatio(attackerStrength,
                        GetTargetStrength(party, state.Target));
                    SettlementOffensiveBehaviour.AssignTarget(state,
                        safetyPasses && targetRatio >= configuration.SkipWhenTargetStrengthRatio);
                }
            }

            if (state.Target is null)
            {
                state.Phase = SettlementOffensivePhase.Search;
                if (SettlementOffensiveBehaviour.AdvanceOperationClock(state, configuration.CooldownInDays))
                {
                    return PartyBehaviourResult.NotApplicable;
                }
                return PatrolOrRemainApplicable(party);
            }

            float currentTargetRatio = StrengthRatio(attackerStrength,
                GetTargetStrength(party, state.Target));
            if (state.Phase == SettlementOffensivePhase.Approach && !safetyPasses)
            {
                SettlementOffensiveBehaviour.ReturnApproachToRecon(state);
            }
            if (state.Phase == SettlementOffensivePhase.Recon)
            {
                SettlementOffensiveBehaviour.UpdateRecon(state, safetyPasses,
                    safetyPasses && currentTargetRatio >= configuration.SkipWhenTargetStrengthRatio);
            }

            if (SettlementOffensiveBehaviour.AdvanceOperationClock(state, configuration.CooldownInDays))
            {
                return PartyBehaviourResult.NotApplicable;
            }

            return state.Phase == SettlementOffensivePhase.Approach
                ? Attack(state.Target, party)
                : PatrolOrRemainApplicable(party);
        }

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData(_saveKey, ref _states);
            _states ??= new Dictionary<MobileParty, SettlementOffensiveBehaviourState>();
        }

        internal SettlementOffensiveBehaviourState GetState(MobileParty party) => _states[party];

        /**
         * Records whatever the behaviour must remember about a target it has just observed or just selected.
         */
        private protected virtual void TrackTarget(SettlementOffensiveBehaviourState state)
        {
        }

        private protected abstract bool IsMatchingOperationActive(MobileParty party, Settlement target);

        private protected abstract bool IsCurrentTargetValid(MobileParty party,
            SettlementOffensiveBehaviourState state, bool matchingOperationActive);

        private protected abstract bool IsTargetComplete(MobileParty party, Settlement target);

        /**
         * Whether the held target must be given up before the offensive presses on with it.
         */
        private protected virtual bool ShouldAbandonTarget(MobileParty party, Settlement target,
            float attackerStrength, TConfiguration configuration)
        {
            return false;
        }

        private protected abstract Settlement? FindTarget(MobileParty party, float attackerStrength,
            TConfiguration configuration);

        private protected abstract float GetTargetStrength(MobileParty party, Settlement target);

        private protected abstract AIBehaviorData CreateOffensiveBehavior(MobileParty party,
            Settlement target);

        private static bool IsNativeSafetyInterruption(AiBehavior shortTermBehavior)
        {
            return shortTermBehavior == AiBehavior.EngageParty ||
                   shortTermBehavior == AiBehavior.FleeToPoint;
        }

        private static float StrengthRatio(float attackerStrength, float defenderStrength)
        {
            return defenderStrength <= 0f ? float.PositiveInfinity : attackerStrength / defenderStrength;
        }

        private PartyBehaviourResult PatrolOrRemainApplicable(MobileParty party)
        {
            Settlement? patrolTarget = PatrolOrders.FindClosestNonHostileSettlement(party);
            return patrolTarget is null
                ? PartyBehaviourResult.ApplicableWithoutBehavior
                : PartyBehaviourResult.From(PatrolOrders.CreatePatrolBehavior(party, patrolTarget));
        }

        private PartyBehaviourResult Attack(Settlement target, MobileParty party)
        {
            return PartyBehaviourResult.From(CreateOffensiveBehavior(party, target));
        }
    }
}
