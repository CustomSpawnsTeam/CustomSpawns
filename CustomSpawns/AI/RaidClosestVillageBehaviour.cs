using System;
using System.Collections.Generic;
using System.Linq;
using CustomSpawns.Data.Dto;
using CustomSpawns.Spawn;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace CustomSpawns.AI
{
    public sealed class RaidClosestVillageBehaviour : PriorityPartyBehaviour<RaidClosestVillageData>
    {
        private const string SaveKey = "_customSpawnsRaidClosestVillageStates";
        private readonly Func<float, float, float> _roll;
        private Dictionary<MobileParty, SettlementOffensiveBehaviourState> _states = new();

        public RaidClosestVillageBehaviour() : this(MBRandom.RandomFloatRanged)
        {
        }

        internal RaidClosestVillageBehaviour(Func<float, float, float> roll)
        {
            _roll = roll;
        }

        public override PartyBehaviourResult GetPriorityBehaviour(MobileParty party, CsPartyData partyData,
            RaidClosestVillageData configuration)
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

            float attackerStrength = GetAttackerStrength(party);
            float visibleHostileStrength = GetVisibleHostileStrength(party);
            bool safetyPasses = SettlementOffensiveBehaviour.MeetsStrengthRatio(attackerStrength,
                visibleHostileStrength, configuration.MinimumAttackerToRoamingHostilesRatio);

            if (state.Target is not null)
            {
                bool matchingRaidActive = IsMatchingRaidActive(party, state.Target);
                bool isValid = IsCurrentTargetValid(party, state.Target, matchingRaidActive);
                bool isComplete = state.Target.IsRaided;
                bool hasContinuation = party.DefaultBehavior == AiBehavior.RaidSettlement &&
                                       party.TargetSettlement == state.Target;
                ActiveTargetObservation observation = SettlementOffensiveBehaviour.ObserveTarget(state,
                    isValid, isComplete, matchingRaidActive, hasContinuation,
                    configuration.MaximumFailedAttempts, configuration.CooldownInDays);
                if (observation == ActiveTargetObservation.EnteredCooldown)
                {
                    return PartyBehaviourResult.NotApplicable;
                }
            }

            if (state.Phase == SettlementOffensivePhase.Active)
            {
                return Raid(state.Target!, party);
            }

            if (state.Phase == SettlementOffensivePhase.Recovery)
            {
                Settlement? recoveryTarget = state.Target;
                if (!SettlementOffensiveBehaviour.TickRecovery(state))
                {
                    return Raid(recoveryTarget!, party);
                }
            }

            if (state.Target is null)
            {
                state.Target = FindClosestRaidTarget(party, attackerStrength,
                    configuration.MinimumAttackerToTargetRatio);
                if (state.Target is not null)
                {
                    float targetRatio = StrengthRatio(attackerStrength, state.Target.Militia);
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

            float currentTargetRatio = StrengthRatio(attackerStrength, state.Target.Militia);
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
                ? Raid(state.Target, party)
                : PatrolOrRemainApplicable(party);
        }

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData(SaveKey, ref _states);
            _states ??= new Dictionary<MobileParty, SettlementOffensiveBehaviourState>();
        }

        private static Settlement? FindClosestRaidTarget(MobileParty party, float attackerStrength,
            float minimumRatio)
        {
            IEnumerable<RaidTargetCandidate<Settlement>> candidates = Settlement.All.Select(settlement =>
                new RaidTargetCandidate<Settlement>(settlement, IsCandidateValid(party, settlement),
                    party.Position.Distance(settlement.GatePosition), settlement.Militia));
            return SettlementOffensiveBehaviour.ChooseClosestTarget(candidates, attackerStrength, minimumRatio);
        }

        private static bool IsCandidateValid(MobileParty party, Settlement settlement)
        {
            return settlement.IsActive && settlement.IsVillage && !settlement.IsRaided &&
                   !settlement.IsUnderRaid && settlement.Party.MapEvent is null &&
                   party.MapFaction.IsAtWarWith(settlement.MapFaction);
        }

        private static bool IsCurrentTargetValid(MobileParty party, Settlement settlement,
            bool matchingRaidActive)
        {
            return settlement.IsActive && settlement.IsVillage && !settlement.IsRaided &&
                   (settlement.Party.MapEvent is null || matchingRaidActive) &&
                   party.MapFaction.IsAtWarWith(settlement.MapFaction);
        }

        private static Settlement? FindClosestNonHostileSettlement(MobileParty party)
        {
            return Settlement.All.Where(s => s.IsActive && (s.IsTown || s.IsCastle || s.IsVillage) &&
                                             !party.MapFaction.IsAtWarWith(s.MapFaction))
                .OrderBy(s => party.Position.Distance(s.GatePosition))
                .FirstOrDefault();
        }

        private static float GetAttackerStrength(MobileParty party)
        {
            return party.Army is not null && party.Army.LeaderParty == party
                ? party.Army.CalculateCurrentStrength()
                : party.Party.CalculateCurrentStrength();
        }

        private static float GetVisibleHostileStrength(MobileParty observer)
        {
            IEnumerable<RoamingPartyStrength> observations = MobileParty.All
                .Where(p => p.CurrentSettlement is null && p.MapEvent is null)
                .Select(p => new RoamingPartyStrength(p == observer, p.IsActive,
                    p.MapFaction.IsAtWarWith(observer.MapFaction), p.AttachedTo is not null,
                    observer.Position.Distance(p.Position), p.Party.CalculateCurrentStrength(),
                    p.Army is not null && p.Army.LeaderParty == p,
                    p.Army is not null && p.Army.LeaderParty == p
                        ? p.Army.CalculateCurrentStrength()
                        : 0f));
            return SettlementOffensiveBehaviour.SumVisibleHostileStrength(observations,
                observer.SeeingRange);
        }

        private static bool IsMatchingRaidActive(MobileParty party, Settlement target)
        {
            return party.MapEvent is not null && party.MapEvent.IsRaid &&
                   party.MapEvent.MapEventSettlement == target;
        }

        private static float StrengthRatio(float attackerStrength, float defenderStrength)
        {
            return defenderStrength <= 0f ? float.PositiveInfinity : attackerStrength / defenderStrength;
        }

        private static PartyBehaviourResult PatrolOrRemainApplicable(MobileParty party)
        {
            Settlement? patrolTarget = FindClosestNonHostileSettlement(party);
            return patrolTarget is null
                ? PartyBehaviourResult.ApplicableWithoutBehavior
                : PartyBehaviourResult.From(new AIBehaviorData(patrolTarget,
                    AiBehavior.PatrolAroundPoint, party.NavigationCapability, false, false, false));
        }

        private static PartyBehaviourResult Raid(Settlement target, MobileParty party)
        {
            return PartyBehaviourResult.From(new AIBehaviorData(target, AiBehavior.RaidSettlement,
                party.NavigationCapability, false, false, false));
        }
    }
}
