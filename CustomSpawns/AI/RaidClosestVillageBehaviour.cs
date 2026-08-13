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
        internal const string SaveKey = "_customSpawnsRaidClosestVillageStates";
        private readonly IRaidClosestVillageContext _context;
        private readonly Func<float, float, float> _roll;
        private Dictionary<MobileParty, SettlementOffensiveBehaviourState> _states = new();

        public RaidClosestVillageBehaviour() : this(new BannerlordRaidClosestVillageContext(),
            MBRandom.RandomFloatRanged)
        {
        }

        internal RaidClosestVillageBehaviour(IRaidClosestVillageContext context,
            Func<float, float, float> roll)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
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

            float attackerStrength = _context.GetAttackerStrength(party);
            float visibleHostileStrength = _context.GetVisibleHostileStrength(party);
            bool safetyPasses = SettlementOffensiveBehaviour.MeetsStrengthRatio(attackerStrength,
                visibleHostileStrength, configuration.MinimumAttackerToRoamingHostilesRatio);

            if (state.Target is not null)
            {
                bool matchingRaidActive = _context.IsMatchingRaidActive(party, state.Target);
                bool isValid = _context.IsCurrentTargetValid(party, state.Target, matchingRaidActive);
                bool isComplete = _context.IsTargetComplete(state.Target);
                bool hasContinuation = _context.HasNativeContinuation(party, state.Target);
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
                    if (IsNativeSafetyInterruption(_context.GetShortTermBehavior(party)))
                    {
                        return PartyBehaviourResult.ApplicableWithoutBehavior;
                    }
                    return Raid(recoveryTarget!, party);
                }
            }

            if (state.Target is null)
            {
                state.Target = _context.FindClosestRaidTarget(party, attackerStrength,
                    configuration.MinimumAttackerToTargetRatio);
                if (state.Target is not null)
                {
                    float targetRatio = StrengthRatio(attackerStrength,
                        _context.GetTargetStrength(state.Target));
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
                _context.GetTargetStrength(state.Target));
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

        internal SettlementOffensiveBehaviourState GetState(MobileParty party)
        {
            return _states[party];
        }

        internal static bool IsNativeSafetyInterruption(AiBehavior shortTermBehavior)
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
            Settlement? patrolTarget = _context.FindClosestNonHostileSettlement(party);
            return patrolTarget is null
                ? PartyBehaviourResult.ApplicableWithoutBehavior
                : PartyBehaviourResult.From(_context.CreatePatrolBehavior(party, patrolTarget));
        }

        private PartyBehaviourResult Raid(Settlement target, MobileParty party)
        {
            return PartyBehaviourResult.From(_context.CreateRaidBehavior(party, target));
        }
    }

    internal interface IRaidClosestVillageContext
    {
        float GetAttackerStrength(MobileParty party);
        float GetVisibleHostileStrength(MobileParty party);
        Settlement? FindClosestRaidTarget(MobileParty party, float attackerStrength,
            float minimumRatio);
        Settlement? FindClosestNonHostileSettlement(MobileParty party);
        float GetTargetStrength(Settlement target);
        bool IsMatchingRaidActive(MobileParty party, Settlement target);
        bool IsCurrentTargetValid(MobileParty party, Settlement target, bool matchingRaidActive);
        bool IsTargetComplete(Settlement target);
        bool HasNativeContinuation(MobileParty party, Settlement target);
        AiBehavior GetShortTermBehavior(MobileParty party);
        AIBehaviorData CreatePatrolBehavior(MobileParty party, Settlement target);
        AIBehaviorData CreateRaidBehavior(MobileParty party, Settlement target);
    }

    internal sealed class BannerlordRaidClosestVillageContext : IRaidClosestVillageContext
    {
        private readonly Func<MobileParty, float> _getSeeingRange;
        private readonly Func<MobileParty, IEnumerable<RoamingPartyStrength>> _getRoamingParties;

        public BannerlordRaidClosestVillageContext() : this(observer => observer.SeeingRange,
            ObserveRoamingParties)
        {
        }

        internal BannerlordRaidClosestVillageContext(Func<MobileParty, float> getSeeingRange,
            Func<MobileParty, IEnumerable<RoamingPartyStrength>> getRoamingParties)
        {
            _getSeeingRange = getSeeingRange;
            _getRoamingParties = getRoamingParties;
        }

        public float GetAttackerStrength(MobileParty party)
        {
            return party.Army is not null && party.Army.LeaderParty == party
                ? party.Army.CalculateCurrentStrength()
                : party.Party.CalculateCurrentStrength();
        }

        public float GetVisibleHostileStrength(MobileParty observer)
        {
            return SettlementOffensiveBehaviour.SumVisibleHostileStrength(
                _getRoamingParties(observer), _getSeeingRange(observer));
        }

        private static IEnumerable<RoamingPartyStrength> ObserveRoamingParties(
            MobileParty observer)
        {
            return MobileParty.All
                .Where(p => p.CurrentSettlement is null && p.MapEvent is null)
                .Select(p => new RoamingPartyStrength(p == observer, p.IsActive,
                    p.MapFaction.IsAtWarWith(observer.MapFaction), p.Army,
                    observer.Position.Distance(p.Position), p.Party.CalculateCurrentStrength()));
        }

        public Settlement? FindClosestRaidTarget(MobileParty party, float attackerStrength,
            float minimumRatio)
        {
            IEnumerable<RaidTargetCandidate<Settlement>> candidates = Settlement.All.Select(settlement =>
                new RaidTargetCandidate<Settlement>(settlement, IsCandidateValid(party, settlement),
                    party.Position.Distance(settlement.GatePosition), settlement.Militia));
            return SettlementOffensiveBehaviour.ChooseClosestTarget(candidates, attackerStrength, minimumRatio);
        }

        public Settlement? FindClosestNonHostileSettlement(MobileParty party)
        {
            return Settlement.All.Where(s => s.IsActive && (s.IsTown || s.IsCastle || s.IsVillage) &&
                                             !party.MapFaction.IsAtWarWith(s.MapFaction))
                .OrderBy(s => party.Position.Distance(s.GatePosition))
                .FirstOrDefault();
        }

        public float GetTargetStrength(Settlement target)
        {
            return target.Militia;
        }

        public bool IsMatchingRaidActive(MobileParty party, Settlement target)
        {
            return party.MapEvent is not null && party.MapEvent.IsRaid &&
                   party.MapEvent.MapEventSettlement == target;
        }

        public bool IsCurrentTargetValid(MobileParty party, Settlement target,
            bool matchingRaidActive)
        {
            return target.IsActive && target.IsVillage && !target.IsRaided &&
                   (target.Party.MapEvent is null || matchingRaidActive) &&
                   party.MapFaction.IsAtWarWith(target.MapFaction);
        }

        public bool IsTargetComplete(Settlement target)
        {
            return target.IsRaided;
        }

        public bool HasNativeContinuation(MobileParty party, Settlement target)
        {
            return party.DefaultBehavior == AiBehavior.RaidSettlement &&
                   party.TargetSettlement == target;
        }

        public AiBehavior GetShortTermBehavior(MobileParty party)
        {
            return party.ShortTermBehavior;
        }

        public AIBehaviorData CreatePatrolBehavior(MobileParty party, Settlement target)
        {
            return new AIBehaviorData(target, AiBehavior.PatrolAroundPoint,
                party.NavigationCapability, false, false, false);
        }

        public AIBehaviorData CreateRaidBehavior(MobileParty party, Settlement target)
        {
            return new AIBehaviorData(target, AiBehavior.RaidSettlement,
                party.NavigationCapability, false, false, false);
        }

        private static bool IsCandidateValid(MobileParty party, Settlement settlement)
        {
            return settlement.IsActive && settlement.IsVillage && !settlement.IsRaided &&
                   !settlement.IsUnderRaid && settlement.Party.MapEvent is null &&
                   party.MapFaction.IsAtWarWith(settlement.MapFaction);
        }
    }
}
