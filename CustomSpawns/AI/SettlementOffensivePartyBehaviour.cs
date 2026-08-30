using System;
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
        private readonly PartyStateStore<SettlementOffensiveBehaviourState> _states;

        private protected SettlementOffensivePartyBehaviour(Func<float, float, float> roll,
            PartyStateStore<SettlementOffensiveBehaviourState> states)
        {
            _roll = roll;
            _states = states;
        }

        private protected abstract IPartyStrengthObserver PartyStrength { get; }

        private protected abstract IOffensiveOperationStatus OperationStatus { get; }

        private protected abstract IPatrolOrders PatrolOrders { get; }

        public override PartyBehaviourResult GetPriorityBehaviour(MobileParty party, CsPartyData partyData,
            TConfiguration configuration)
        {
            SettlementOffensiveBehaviourState state = _states.GetOrAdd(party);
            var offensive = new SettlementOffensiveStateMachine(state);

            if (offensive.TickCooldown())
            {
                return PartyBehaviourResult.NotApplicable;
            }

            var settings = new SettlementOffensiveSettings(configuration.MinimumRequiredClearTimeInDays,
                configuration.MaximumRequiredClearTimeInDays, configuration.MinimumOperationWindowInDays,
                configuration.MaximumOperationWindowInDays, configuration.GracePeriodInDays,
                configuration.MaximumFailedAttempts, configuration.CooldownInDays);
            offensive.BeginEpisode(settings, _roll);

            float attackerStrength = PartyStrength.GetAttackerStrength(party);
            float visibleHostileStrength = PartyStrength.GetVisibleHostileStrength(party);
            bool safetyPasses = OffensiveStrengthRules.MeetsStrengthRatio(attackerStrength,
                visibleHostileStrength, configuration.MinimumAttackerToRoamingHostilesRatio);

            if (state.Target is not null)
            {
                Settlement observedTarget = state.Target;
                TrackTarget(state);
                bool matchingOperationActive = IsMatchingOperationActive(party, observedTarget);
                ActiveTargetObservation observation = offensive.ObserveTarget(
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
                offensive.ClearTarget();
            }

            if (state.Phase == SettlementOffensivePhase.Active)
            {
                return Attack(state.Target!, party);
            }

            if (state.Phase == SettlementOffensivePhase.Recovery)
            {
                Settlement? recoveryTarget = state.Target;
                if (!offensive.TickRecovery())
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
                    offensive.AssignTarget(
                        safetyPasses && targetRatio >= configuration.SkipWhenTargetStrengthRatio);
                }
            }

            if (state.Target is null)
            {
                state.Phase = SettlementOffensivePhase.Search;
                if (offensive.AdvanceOperationClock(configuration.CooldownInDays))
                {
                    return PartyBehaviourResult.NotApplicable;
                }
                return PatrolOrRemainApplicable(party);
            }

            float currentTargetRatio = StrengthRatio(attackerStrength,
                GetTargetStrength(party, state.Target));
            if (state.Phase == SettlementOffensivePhase.Approach && !safetyPasses)
            {
                offensive.ReturnApproachToRecon();
            }
            if (state.Phase == SettlementOffensivePhase.Recon)
            {
                offensive.UpdateRecon(safetyPasses,
                    safetyPasses && currentTargetRatio >= configuration.SkipWhenTargetStrengthRatio);
            }

            if (offensive.AdvanceOperationClock(configuration.CooldownInDays))
            {
                return PartyBehaviourResult.NotApplicable;
            }

            return state.Phase == SettlementOffensivePhase.Approach
                ? Attack(state.Target, party)
                : PatrolOrRemainApplicable(party);
        }

        public override void SyncData(IDataStore dataStore)
        {
            _states.SyncData(dataStore);
        }

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
