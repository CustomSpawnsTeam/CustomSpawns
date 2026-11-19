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

            if (TickCooldown(state))
            {
                return PartyBehaviourResult.NotApplicable;
            }

            BeginEpisode(state, configuration);

            float attackerStrength = PartyStrength.GetAttackerStrength(party);

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
                if (abandonBehavior.HasValue)
                {
                    // Giving up a position already taken is the one abandonment that must not be undone on
                    // the next tick. Nothing about the town has changed, and a force that masses weighs no
                    // strength when it chooses, so it would march straight back and be driven off again for
                    // the rest of the operation window. It sits out the cooldown instead.
                    EnterCooldown(state, configuration.CooldownInDays);
                    return PartyBehaviourResult.From(abandonBehavior.Value);
                }

                // Giving up a target still being marched on is not being driven off anything: the choice
                // that picked it weighed the very strength that has now turned against it, so it will not
                // pick it again, and benching the party would be giving up on a town it could still take.
                ClearTarget(state);
            }

            // A state restored from a save can name a settlement that no longer resolves, which is the one
            // way an operation underway can be left with nothing to attack. It looks for a target again
            // rather than ordering an attack on nothing.
            if (state.Phase == SettlementOffensivePhase.Active)
            {
                if (state.Target is not null)
                {
                    return Attack(state.Target, party);
                }

                ClearTarget(state);
            }

            if (state.Target is null)
            {
                // Giving up an objective takes the party back out of the force that was committed to it, so
                // the figure read while that objective still stood counted siblings this party no longer
                // has anything to do with. What picks the next town is what the party has by itself, as it
                // was on the tick it first chose one.
                attackerStrength = PartyStrength.GetAttackerStrength(party);
                state.Target = FindTarget(party, attackerStrength, configuration);
                if (state.Target is not null)
                {
                    TrackTarget(state);
                    AssignTarget(state);
                    // Joining an objective is what makes a party part of its force, so what the force is
                    // worth has just changed. The figure taken before the objective was recorded counted
                    // this party alone, and everything below weighs the force, not the party.
                    attackerStrength = PartyStrength.GetAttackerStrength(party);
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
            // An operation its own force already has underway at the target needs no reconnaissance: the
            // ground has been judged by those standing on it, and holding back only leaves them to face
            // the relief force short of the very strength they are judged on.
            bool joinsOperationUnderway = JoinsOperationUnderway(party, state.Target);
            bool reconSafetyPasses = joinsOperationUnderway || PassesReconSafety(party, state.Target,
                attackerStrength, configuration);
            // Whether the party was already marching when this tick began. Read before reconnaissance can
            // promote it, so a party that has just finished looking is asked as one that has not set off.
            bool wasApproaching = state.Phase == SettlementOffensivePhase.Approach;
            if ((state.Phase == SettlementOffensivePhase.Approach ||
                 state.Phase == SettlementOffensivePhase.Gather) && !reconSafetyPasses)
            {
                state.Phase = SettlementOffensivePhase.Recon;
                state.AccumulatedClearReconDays = 0f;
            }
            if (state.Phase == SettlementOffensivePhase.Recon)
            {
                if (!reconSafetyPasses && ReselectTargetWhenUnsafe(party))
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
                    joinsOperationUnderway ||
                    reconSafetyPasses && currentTargetRatio >= configuration.SkipWhenTargetStrengthRatio);
            }

            // A party that is done with recon either goes in or holds near the target waiting for the rest
            // of its force, and it keeps re-asking: a hold ends the moment enough of the force has come up.
            if (state.Phase == SettlementOffensivePhase.Approach ||
                state.Phase == SettlementOffensivePhase.Gather)
            {
                state.Phase = ShouldHoldShortOfTarget(party, state.Target, wasApproaching,
                        configuration)
                    ? SettlementOffensivePhase.Gather
                    : SettlementOffensivePhase.Approach;
            }

            // The operation window runs while a party gathers, so a hold nobody joins ends in cooldown.
            if (AdvanceOperationClock(state, configuration.CooldownInDays))
            {
                return PartyBehaviourResult.NotApplicable;
            }

            // Reconnaissance watches the objective: it patrols the target itself, the same ground a hold
            // waits on. Patrolling the nearest friendly settlement instead would march the party away from
            // the town it has just chosen and back again, with the operation clock running throughout.
            return state.Phase switch
            {
                SettlementOffensivePhase.Approach => Attack(state.Target, party),
                SettlementOffensivePhase.Gather => PartyBehaviourResult.From(
                    PatrolOrders.CreatePatrolBehavior(party, state.Target)),
                SettlementOffensivePhase.Recon => PartyBehaviourResult.From(
                    PatrolOrders.CreatePatrolBehavior(party, state.Target)),
                _ => PatrolOrRemainApplicable(party)
            };
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

        /**
         * Whether the party should hold near the target it has committed to instead of attacking now,
         * so that the rest of its force can come up. A party that has already set off is told so, since
         * turning a march back into a hold is a different question from starting one.
         */
        private protected virtual bool ShouldHoldShortOfTarget(MobileParty party, Settlement target,
            bool alreadyApproaching, TConfiguration configuration)
        {
            return false;
        }

        private protected virtual AIBehaviorData? CreateTargetAbandonmentBehavior(
            MobileParty party, Settlement target, TConfiguration configuration)
        {
            return null;
        }

        /**
         * Whether the ground around the target is clear enough to press on with the operation: which
         * hostiles count, and what the party can actually bring against them, are the offensive's own.
         * The sweep of the surrounding ground is read here rather than handed in, because an offensive
         * that asks about the ground around its target instead would otherwise pay for a sweep of the
         * whole map it never looks at, once an hour, for every party it steers.
         */
        private protected virtual bool PassesReconSafety(MobileParty party, Settlement target,
            float attackerStrength, TConfiguration configuration)
        {
            return OffensiveStrengthRules.MeetsStrengthRatio(attackerStrength,
                PartyStrength.GetVisibleHostileStrength(party),
                configuration.MinimumAttackerToRoamingHostilesRatio);
        }

        private protected virtual bool JoinsOperationUnderway(MobileParty party, Settlement target) => false;

        private protected virtual bool ReselectTargetWhenUnsafe(MobileParty party) => false;

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
