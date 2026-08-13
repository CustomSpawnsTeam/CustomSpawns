using System;
using System.Collections.Generic;
using System.Linq;
using CustomSpawns.Data.Adapter;
using CustomSpawns.Data.Dto;
using CustomSpawns.Spawn;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace CustomSpawns.AI
{
    public sealed class BesiegeClosestSettlementBehaviour : PriorityPartyBehaviour<BesiegeClosestSettlementData>
    {
        internal const string SaveKey = "_customSpawnsBesiegeClosestSettlementStates";
        private readonly IBesiegeClosestSettlementContext _context;
        private readonly Func<float, float, float> _roll;
        private Dictionary<MobileParty, SettlementOffensiveBehaviourState> _states = new();

        public BesiegeClosestSettlementBehaviour() : this(new BannerlordBesiegeClosestSettlementContext(),
            MBRandom.RandomFloatRanged)
        {
        }

        internal BesiegeClosestSettlementBehaviour(IBesiegeClosestSettlementContext context,
            Func<float, float, float> roll)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _roll = roll;
        }

        public override PartyBehaviourResult GetPriorityBehaviour(MobileParty party, CsPartyData partyData,
            BesiegeClosestSettlementData configuration)
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
                state.TargetMapFaction ??= _context.GetTargetMapFaction(state.Target);
                bool matchingOperationActive = _context.IsMatchingSiegeActive(party, state.Target) ||
                                               _context.IsMatchingAssaultActive(party, state.Target);
                ActiveTargetObservation observation = SettlementOffensiveBehaviour.ObserveTarget(state,
                    _context.IsCurrentTargetValid(party, state.Target, matchingOperationActive,
                        state.TargetMapFaction),
                    _context.IsTargetComplete(party, state.Target), matchingOperationActive,
                    _context.HasNativeContinuation(party, state.Target),
                    configuration.MaximumFailedAttempts, configuration.CooldownInDays);
                if (observation == ActiveTargetObservation.EnteredCooldown)
                {
                    return PartyBehaviourResult.NotApplicable;
                }
            }

            if (state.Phase == SettlementOffensivePhase.Active)
            {
                return Besiege(state.Target!, party);
            }

            if (state.Phase == SettlementOffensivePhase.Recovery)
            {
                Settlement? recoveryTarget = state.Target;
                if (!SettlementOffensiveBehaviour.TickRecovery(state))
                {
                    if (RaidClosestVillageBehaviour.IsNativeSafetyInterruption(
                            _context.GetShortTermBehavior(party)))
                    {
                        return PartyBehaviourResult.ApplicableWithoutBehavior;
                    }
                    return Besiege(recoveryTarget!, party);
                }
            }

            if (state.Target is null)
            {
                state.Target = _context.FindClosestSiegeTarget(party, attackerStrength,
                    configuration.MinimumAttackerToTargetRatio, configuration.TargetTypes);
                if (state.Target is not null)
                {
                    state.TargetMapFaction = _context.GetTargetMapFaction(state.Target);
                    float targetRatio = StrengthRatio(attackerStrength,
                        _context.GetTargetStrength(party, state.Target));
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
                _context.GetTargetStrength(party, state.Target));
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
                ? Besiege(state.Target, party)
                : PatrolOrRemainApplicable(party);
        }

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData(SaveKey, ref _states);
            _states ??= new Dictionary<MobileParty, SettlementOffensiveBehaviourState>();
        }

        internal SettlementOffensiveBehaviourState GetState(MobileParty party) => _states[party];

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

        private PartyBehaviourResult Besiege(Settlement target, MobileParty party)
        {
            return PartyBehaviourResult.From(_context.CreateSiegeBehavior(party, target));
        }
    }

    internal interface IBesiegeClosestSettlementContext
    {
        float GetAttackerStrength(MobileParty party);
        float GetVisibleHostileStrength(MobileParty party);
        Settlement? FindClosestSiegeTarget(MobileParty party, float attackerStrength,
            float minimumRatio, IReadOnlyList<SpawnSettlementType> targetTypes);
        Settlement? FindClosestNonHostileSettlement(MobileParty party);
        float GetTargetStrength(MobileParty party, Settlement target);
        IFaction GetTargetMapFaction(Settlement target);
        bool IsMatchingSiegeActive(MobileParty party, Settlement target);
        bool IsMatchingAssaultActive(MobileParty party, Settlement target);
        bool IsCurrentTargetValid(MobileParty party, Settlement target, bool matchingOperationActive,
            IFaction? intendedTargetFaction);
        bool IsTargetComplete(MobileParty party, Settlement target);
        bool HasNativeContinuation(MobileParty party, Settlement target);
        AiBehavior GetShortTermBehavior(MobileParty party);
        AIBehaviorData CreatePatrolBehavior(MobileParty party, Settlement target);
        AIBehaviorData CreateSiegeBehavior(MobileParty party, Settlement target);
    }

    internal readonly struct SiegeTargetCandidate
    {
        public Settlement Target { get; }
        public bool IsActive { get; }
        public bool IsHostile { get; }
        public SpawnSettlementType Type { get; }
        public bool HasCompatibleSiege { get; }
        public float Distance { get; }
        public float DefenderStrength { get; }

        public SiegeTargetCandidate(Settlement target, bool isActive, bool isHostile,
            SpawnSettlementType type, bool hasCompatibleSiege, float distance, float defenderStrength)
        {
            Target = target;
            IsActive = isActive;
            IsHostile = isHostile;
            Type = type;
            HasCompatibleSiege = hasCompatibleSiege;
            Distance = distance;
            DefenderStrength = defenderStrength;
        }
    }

    internal readonly struct SettlementDefenderPartyStrength
    {
        public float Strength { get; }
        public bool IsActive { get; }
        public bool IsHostileToAttacker { get; }
        public bool IsVillager { get; }
        public bool IsCaravan { get; }
        public bool IsGarrison { get; }
        public bool IsMilitia { get; }
        public bool IsLordParty { get; }
        public bool IsSettlementFaction { get; }

        public SettlementDefenderPartyStrength(float strength, bool isActive = true,
            bool isHostileToAttacker = true, bool isVillager = false, bool isCaravan = false,
            bool isGarrison = false, bool isMilitia = false, bool isLordParty = true,
            bool isSettlementFaction = true)
        {
            Strength = strength;
            IsActive = isActive;
            IsHostileToAttacker = isHostileToAttacker;
            IsVillager = isVillager;
            IsCaravan = isCaravan;
            IsGarrison = isGarrison;
            IsMilitia = isMilitia;
            IsLordParty = isLordParty;
            IsSettlementFaction = isSettlementFaction;
        }
    }

    internal sealed class BannerlordBesiegeClosestSettlementContext : IBesiegeClosestSettlementContext
    {
        private readonly Func<MobileParty, float> _getSeeingRange;
        private readonly Func<MobileParty, IEnumerable<RoamingPartyStrength>> _getRoamingParties;
        private readonly Func<MobileParty, IEnumerable<SiegeTargetCandidate>> _getCandidates;

        public BannerlordBesiegeClosestSettlementContext() : this(observer => observer.SeeingRange,
            ObserveRoamingParties, ObserveCandidates)
        {
        }

        internal BannerlordBesiegeClosestSettlementContext(Func<MobileParty, float> getSeeingRange,
            Func<MobileParty, IEnumerable<RoamingPartyStrength>> getRoamingParties,
            Func<MobileParty, IEnumerable<SiegeTargetCandidate>> getCandidates)
        {
            _getSeeingRange = getSeeingRange;
            _getRoamingParties = getRoamingParties;
            _getCandidates = getCandidates;
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

        public Settlement? FindClosestSiegeTarget(MobileParty party, float attackerStrength,
            float minimumRatio, IReadOnlyList<SpawnSettlementType> targetTypes)
        {
            return _getCandidates(party)
                .Where(candidate => candidate.IsActive && candidate.IsHostile &&
                                    candidate.HasCompatibleSiege && targetTypes.Contains(candidate.Type) &&
                                    SettlementOffensiveBehaviour.MeetsStrengthRatio(attackerStrength,
                                        candidate.DefenderStrength, minimumRatio))
                .OrderBy(candidate => candidate.Distance)
                .Select(candidate => candidate.Target)
                .FirstOrDefault();
        }

        public Settlement? FindClosestNonHostileSettlement(MobileParty party)
        {
            return Settlement.All.Where(s => s.IsActive && (s.IsTown || s.IsCastle || s.IsVillage) &&
                                             !party.MapFaction.IsAtWarWith(s.MapFaction))
                .OrderBy(s => party.Position.Distance(s.GatePosition))
                .FirstOrDefault();
        }

        public float GetTargetStrength(MobileParty party, Settlement target) =>
            GetDefenderStrength(party, target);

        public IFaction GetTargetMapFaction(Settlement target) => target.MapFaction;

        public bool IsMatchingSiegeActive(MobileParty party, Settlement target)
        {
            var camp = target.SiegeEvent?.BesiegerCamp;
            return camp is not null && camp.SiegeEvent.BesiegedSettlement == target &&
                   (party.BesiegerCamp == camp || camp.IsBesiegerSideParty(party) ||
                    camp.HasInvolvedPartyForEventType(party.Party));
        }

        public bool IsMatchingAssaultActive(MobileParty party, Settlement target)
        {
            return party.MapEvent is not null && party.MapEvent.IsSiegeAssault &&
                   party.MapEvent.MapEventSettlement == target &&
                   party.MapEventSide == party.MapEvent.AttackerSide;
        }

        public bool IsCurrentTargetValid(MobileParty party, Settlement target,
            bool matchingOperationActive, IFaction? intendedTargetFaction)
        {
            return target.IsActive && (target.IsTown || target.IsCastle) &&
                   party.MapFaction.IsAtWarWith(target.MapFaction) &&
                   IsIntendedTargetFaction(target.MapFaction, intendedTargetFaction) &&
                   (target.SiegeEvent is null || matchingOperationActive);
        }

        internal static bool IsIntendedTargetFaction(IFaction currentFaction,
            IFaction? intendedTargetFaction)
        {
            return currentFaction == intendedTargetFaction;
        }

        public bool IsTargetComplete(MobileParty party, Settlement target)
        {
            return target.MapFaction == party.MapFaction;
        }

        public bool HasNativeContinuation(MobileParty party, Settlement target)
        {
            return (party.DefaultBehavior == AiBehavior.BesiegeSettlement ||
                    party.DefaultBehavior == AiBehavior.AssaultSettlement) &&
                   party.TargetSettlement == target;
        }

        public AiBehavior GetShortTermBehavior(MobileParty party) => party.ShortTermBehavior;

        public AIBehaviorData CreatePatrolBehavior(MobileParty party, Settlement target)
        {
            return new AIBehaviorData(target, AiBehavior.PatrolAroundPoint,
                party.NavigationCapability, false, false, false);
        }

        public AIBehaviorData CreateSiegeBehavior(MobileParty party, Settlement target)
        {
            return new AIBehaviorData(target, AiBehavior.BesiegeSettlement,
                party.NavigationCapability, false, false, false);
        }

        internal static float CalculateDefenderStrength(float militiaStrength, float garrisonStrength,
            IEnumerable<SettlementDefenderPartyStrength> occupants)
        {
            return militiaStrength + garrisonStrength + occupants
                .Where(p => p.IsActive && p.IsHostileToAttacker && !p.IsVillager && !p.IsCaravan &&
                            !p.IsGarrison && !p.IsMilitia)
                .Sum(p => p.Strength);
        }

        private static IEnumerable<RoamingPartyStrength> ObserveRoamingParties(MobileParty observer)
        {
            return MobileParty.All
                .Where(p => p.CurrentSettlement is null && p.MapEvent is null)
                .Select(p => new RoamingPartyStrength(p == observer, p.IsActive,
                    p.MapFaction.IsAtWarWith(observer.MapFaction), p.Army,
                    observer.Position.Distance(p.Position), p.Party.CalculateCurrentStrength()));
        }

        private static IEnumerable<SiegeTargetCandidate> ObserveCandidates(MobileParty party)
        {
            return Settlement.All.Where(s => s.IsTown || s.IsCastle).Select(settlement =>
                new SiegeTargetCandidate(settlement, settlement.IsActive,
                    party.MapFaction.IsAtWarWith(settlement.MapFaction),
                    settlement.IsTown ? SpawnSettlementType.Town : SpawnSettlementType.Castle,
                    settlement.SiegeEvent is null || IsMatchingCamp(party, settlement),
                    party.Position.Distance(settlement.GatePosition), GetDefenderStrength(party, settlement)));
        }

        private static bool IsMatchingCamp(MobileParty party, Settlement settlement)
        {
            var camp = settlement.SiegeEvent?.BesiegerCamp;
            return camp is not null && camp.SiegeEvent.BesiegedSettlement == settlement &&
                   (party.BesiegerCamp == camp || camp.IsBesiegerSideParty(party) ||
                    camp.HasInvolvedPartyForEventType(party.Party));
        }

        private static float GetDefenderStrength(MobileParty attacker, Settlement settlement)
        {
            float garrisonStrength = settlement.Town.GarrisonParty?.Party.CalculateCurrentStrength() ?? 0f;
            IEnumerable<SettlementDefenderPartyStrength> occupants = settlement.Parties.Select(p =>
                new SettlementDefenderPartyStrength(p.Party.CalculateCurrentStrength(), p.IsActive,
                    p.MapFaction.IsAtWarWith(attacker.MapFaction), p.IsVillager, p.IsCaravan,
                    p.IsGarrison, p.IsMilitia, p.IsLordParty, p.MapFaction == settlement.MapFaction));
            return CalculateDefenderStrength(settlement.Militia, garrisonStrength, occupants);
        }
    }
}
