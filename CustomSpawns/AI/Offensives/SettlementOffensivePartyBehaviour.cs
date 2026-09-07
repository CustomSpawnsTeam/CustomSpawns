using System;
using CustomSpawns.AI.Adapters;
using CustomSpawns.AI.Behaviours;
using CustomSpawns.AI.Model;
using CustomSpawns.Data.Dto;
using CustomSpawns.Spawn;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace CustomSpawns.AI.Offensives
{
    /**
     * The phase sequencing every settlement offensive follows, whichever settlement it attacks and however
     * it attacks it: cooldown, target observation, recon, approach and the operation itself.
     * Only the steps that depend on what is being attacked are left to the concrete behaviours.
     */
    public abstract class SettlementOffensivePartyBehaviour<TConfiguration>
        : PriorityPartyBehaviour<TConfiguration> where TConfiguration : SettlementOffensiveData
    {
        private const float DaysPerHour = 1f / 24f;
        private const int LegacyRecoveryPhase = 5;
        private readonly Func<float, float, float> _roll;
        private readonly PartyStateStore<SettlementOffensiveBehaviourState> _states;

        private protected SettlementOffensivePartyBehaviour(Func<float, float, float> roll,
            PartyStateStore<SettlementOffensiveBehaviourState> states)
        {
            _roll = roll;
            _states = states;
        }

        private protected abstract IPartyStrengthObserver PartyStrength { get; }

        private protected abstract IPatrolOrders PatrolOrders { get; }

        public override PartyBehaviourResult GetPriorityBehaviour(MobileParty party, CsPartyData partyData,
            TConfiguration configuration)
        {
            SettlementOffensiveBehaviourState state = _states.GetOrAdd(party);
            if ((int)state.Phase == LegacyRecoveryPhase)
            {
                state.Phase = state.Target is null
                    ? SettlementOffensivePhase.Search
                    : SettlementOffensivePhase.Approach;
            }
            if (TickCooldown(state))
            {
                return PartyBehaviourResult.NotApplicable;
            }

            BeginEpisode(state, configuration);

            float attackerStrength = PartyStrength.GetAttackerStrength(party);
            float visibleHostileStrength = PartyStrength.GetVisibleHostileStrength(party);

            if (state.Target is not null)
            {
                Settlement observedTarget = state.Target;
                TrackTarget(state);
                bool matchingOperationActive = IsMatchingOperationActive(party, observedTarget);
                if (state.Phase == SettlementOffensivePhase.Active && IsTargetComplete(party, observedTarget))
                {
                    EnterCooldown(state, configuration.CooldownInDays);
                    return PartyBehaviourResult.NotApplicable;
                }

                if (!IsCurrentTargetValid(party, state, matchingOperationActive))
                {
                    ClearTarget(state);
                }
                else if (matchingOperationActive)
                {
                    state.Phase = SettlementOffensivePhase.Active;
                }
                else if (state.Phase == SettlementOffensivePhase.Active)
                {
                    state.Phase = SettlementOffensivePhase.Approach;
                }
            }

            if (state.Target is not null &&
                ShouldAbandonTarget(party, state.Target, attackerStrength, configuration))
            {
                AIBehaviorData? abandonBehavior = CreateTargetAbandonmentBehavior(
                    party, state.Target, configuration);
                ClearTarget(state);
                if (abandonBehavior.HasValue)
                {
                    return PartyBehaviourResult.From(abandonBehavior.Value);
                }
            }

            if (state.Phase == SettlementOffensivePhase.Active)
            {
                return Attack(state.Target!, party);
            }

            if (state.Target is null)
            {
                state.Target = FindTarget(party, attackerStrength, configuration);
                if (state.Target is not null)
                {
                    TrackTarget(state);
                    AssignTarget(state);
                }
            }

            if (state.Target is null)
            {
                state.Phase = SettlementOffensivePhase.Search;
                if (AdvanceOperationClock(state, configuration.CooldownInDays))
                {
                    return PartyBehaviourResult.NotApplicable;
                }
                return PatrolOrRemainApplicable(party);
            }

            float currentTargetRatio = StrengthRatio(attackerStrength,
                GetTargetStrength(party, state.Target));
            float reconVisibleHostileStrength = GetReconVisibleHostileStrength(party, state.Target,
                visibleHostileStrength);
            bool reconSafetyPasses = OffensiveStrengthRules.MeetsStrengthRatio(attackerStrength,
                reconVisibleHostileStrength, configuration.MinimumAttackerToRoamingHostilesRatio);
            if (state.Phase == SettlementOffensivePhase.Approach && !reconSafetyPasses)
            {
                state.Phase = SettlementOffensivePhase.Recon;
                state.AccumulatedClearReconDays = 0f;
            }
            if (state.Phase == SettlementOffensivePhase.Recon)
            {
                if (!reconSafetyPasses && ReselectTargetWhenUnsafe)
                {
                    Settlement? alternative = FindAlternativeTarget(party, state.Target,
                        attackerStrength, configuration);
                    ClearTarget(state);
                    if (alternative is not null)
                    {
                        state.Target = alternative;
                        TrackTarget(state);
                        AssignTarget(state);
                    }
                    if (AdvanceOperationClock(state, configuration.CooldownInDays))
                    {
                        return PartyBehaviourResult.NotApplicable;
                    }
                    return PatrolOrRemainApplicable(party);
                }
                UpdateRecon(state, reconSafetyPasses,
                    reconSafetyPasses && currentTargetRatio >= configuration.SkipWhenTargetStrengthRatio);
            }

            if (AdvanceOperationClock(state, configuration.CooldownInDays))
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

        private protected virtual AIBehaviorData? CreateTargetAbandonmentBehavior(
            MobileParty party, Settlement target, TConfiguration configuration)
        {
            return null;
        }

        private protected virtual float GetReconVisibleHostileStrength(MobileParty party,
            Settlement target, float visibleHostileStrength)
        {
            return visibleHostileStrength;
        }

        private protected virtual bool ReselectTargetWhenUnsafe => false;

        private protected virtual Settlement? FindAlternativeTarget(MobileParty party, Settlement target,
            float attackerStrength, TConfiguration configuration)
        {
            return null;
        }

        private protected abstract Settlement? FindTarget(MobileParty party, float attackerStrength,
            TConfiguration configuration);

        private protected abstract float GetTargetStrength(MobileParty party, Settlement target);

        private protected abstract AIBehaviorData CreateOffensiveBehavior(MobileParty party,
            Settlement target);

        private void BeginEpisode(SettlementOffensiveBehaviourState state, TConfiguration configuration)
        {
            if (state.Phase != SettlementOffensivePhase.Idle)
                return;

            state.Target = null;
            state.TargetMapFaction = null;
            state.Phase = SettlementOffensivePhase.Search;
            state.RequiredClearReconDays = _roll(configuration.MinimumRequiredClearTimeInDays,
                configuration.MaximumRequiredClearTimeInDays);
            state.AccumulatedClearReconDays = 0f;
            state.OperationWindowDays = _roll(configuration.MinimumOperationWindowInDays,
                configuration.MaximumOperationWindowInDays);
            state.ElapsedOperationDays = 0f;
            state.CooldownRemainingDays = 0f;
        }

        private static bool TickCooldown(SettlementOffensiveBehaviourState state)
        {
            if (state.Phase != SettlementOffensivePhase.Cooldown)
                return false;

            state.CooldownRemainingDays = Math.Max(0f, state.CooldownRemainingDays - DaysPerHour);
            if (state.CooldownRemainingDays <= 0f)
                state.Phase = SettlementOffensivePhase.Idle;
            return true;
        }

        private static void AssignTarget(SettlementOffensiveBehaviourState state)
        {
            state.AccumulatedClearReconDays = 0f;
            state.Phase = SettlementOffensivePhase.Recon;
        }

        private static void UpdateRecon(SettlementOffensiveBehaviourState state, bool safetyRatioPasses,
            bool skipRecon)
        {
            if (!safetyRatioPasses)
            {
                state.AccumulatedClearReconDays = 0f;
                return;
            }

            if (skipRecon)
            {
                state.Phase = SettlementOffensivePhase.Approach;
                return;
            }

            state.AccumulatedClearReconDays += DaysPerHour;
            if (state.AccumulatedClearReconDays >= state.RequiredClearReconDays)
                state.Phase = SettlementOffensivePhase.Approach;
        }

        private static bool AdvanceOperationClock(SettlementOffensiveBehaviourState state, float cooldownInDays)
        {
            if (state.Phase == SettlementOffensivePhase.Active || state.Phase == SettlementOffensivePhase.Cooldown)
                return false;

            state.ElapsedOperationDays += DaysPerHour;
            if (state.ElapsedOperationDays < state.OperationWindowDays)
                return false;

            EnterCooldown(state, cooldownInDays);
            return true;
        }

        private static void ClearTarget(SettlementOffensiveBehaviourState state)
        {
            state.Target = null;
            state.TargetMapFaction = null;
            state.Phase = SettlementOffensivePhase.Search;
            state.AccumulatedClearReconDays = 0f;
        }

        private static void EnterCooldown(SettlementOffensiveBehaviourState state, float cooldownInDays)
        {
            state.Target = null;
            state.TargetMapFaction = null;
            state.Phase = SettlementOffensivePhase.Cooldown;
            state.CooldownRemainingDays = cooldownInDays;
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
