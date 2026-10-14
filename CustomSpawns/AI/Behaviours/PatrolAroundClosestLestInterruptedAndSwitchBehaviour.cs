using System.Collections.Generic;
using CustomSpawns.AI.Adapters;
using CustomSpawns.Data.Dto;
using CustomSpawns.Spawn;
using CustomSpawns.Utils;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.SaveSystem;

namespace CustomSpawns.AI.Behaviours
{
    public sealed class PatrolAroundClosestLestInterruptedAndSwitchBehaviour :
        PriorityPartyBehaviour<PatrolAroundClosestLestInterruptedAndSwitchData>
    {
        private const float PatrolDaysPerHour = 1f / 24f;
        public const string SaveKey = "_customSpawnsPatrolAroundClosestStates";

        private readonly System.Func<MobileParty, MobileParty.NavigationType> _getNavigationCapability;
        private readonly PartyStateStore<PatrolState> _patrolStates;

        public PatrolAroundClosestLestInterruptedAndSwitchBehaviour()
            : this(party => party.NavigationCapability, new PartyStateStore<PatrolState>(SaveKey))
        {
        }

        public PatrolAroundClosestLestInterruptedAndSwitchBehaviour(
            System.Func<MobileParty, MobileParty.NavigationType> getNavigationCapability,
            PartyStateStore<PatrolState> patrolStates)
        {
            _getNavigationCapability = getNavigationCapability;
            _patrolStates = patrolStates;
        }

        public override PartyBehaviourResult GetPriorityBehaviour(MobileParty party, CsPartyData partyData,
            PatrolAroundClosestLestInterruptedAndSwitchData patrolConfig)
        {
            Settlement? patrolTarget = GetPatrolTarget(party, patrolConfig);
            if (patrolTarget is null)
            {
                return PartyBehaviourResult.ApplicableWithoutBehavior;
            }

            return PartyBehaviourResult.From(new AIBehaviorData(patrolTarget,
                AiBehavior.PatrolAroundPoint, _getNavigationCapability(party), false, false, false));
        }

        public override void SyncData(IDataStore dataStore)
        {
            _patrolStates.SyncData(dataStore);
        }

        private Settlement? GetPatrolTarget(MobileParty party,
            PatrolAroundClosestLestInterruptedAndSwitchData patrolConfig)
        {
            PatrolState patrolState = _patrolStates.GetOrAdd(party);

            bool hasExistingTarget = patrolState.Target is not null;
            if (!hasExistingTarget)
            {
                patrolState.Target = GetNextPatrolTarget(party, patrolConfig, null);
            }
            if (patrolState.Target is not null)
            {
                patrolState.StablePatrolDays = RetainOrRollStablePatrolDays(hasExistingTarget,
                    patrolState.StablePatrolDays, () => RollStablePatrolDays(patrolConfig));
            }

            bool isInterrupted = IsPatrolInterrupted(party.DefaultBehavior, party.ShortTermBehavior);
            patrolState.UninterruptedPatrolDays = UpdateUninterruptedPatrolDays(
                patrolState.UninterruptedPatrolDays, isInterrupted);
            if (patrolState.Target is not null &&
                ShouldSwitchPatrolTarget(patrolState.UninterruptedPatrolDays, patrolState.StablePatrolDays,
                    isInterrupted))
            {
                Settlement? nextTarget = GetNextPatrolTarget(party, patrolConfig, patrolState.Target);
                if (nextTarget is not null)
                {
                    patrolState.Target = nextTarget;
                }
                patrolState.UninterruptedPatrolDays = 0f;
                patrolState.StablePatrolDays = RollStablePatrolDays(patrolConfig);
            }

            return patrolState.Target;
        }

        private static Settlement? GetNextPatrolTarget(MobileParty party,
            PatrolAroundClosestLestInterruptedAndSwitchData patrolConfig, Settlement? currentTarget)
        {
            return CampaignUtils.GetClosestNonHostileCityAmong(party,
                patrolConfig.PreferredSettlementTypes, currentTarget!);
        }

        private static float RollStablePatrolDays(PatrolAroundClosestLestInterruptedAndSwitchData patrolConfig)
        {
            return MBRandom.RandomFloatRanged(patrolConfig.MinStableDays, patrolConfig.MaxStableDays);
        }

        public static bool IsPatrolInterrupted(AiBehavior defaultBehavior, AiBehavior shortTermBehavior)
        {
            return IsInterruptingBehavior(defaultBehavior) || IsInterruptingBehavior(shortTermBehavior);
        }

        private static bool IsInterruptingBehavior(AiBehavior behavior)
        {
            return behavior == AiBehavior.EngageParty || behavior == AiBehavior.GoAroundParty ||
                   behavior == AiBehavior.JoinParty || behavior == AiBehavior.FleeToPoint;
        }

        public static float UpdateUninterruptedPatrolDays(float currentDays, bool isInterrupted)
        {
            return isInterrupted ? 0f : currentDays + PatrolDaysPerHour;
        }

        public static bool ShouldSwitchPatrolTarget(float uninterruptedPatrolDays, float stablePatrolDays,
            bool isInterrupted)
        {
            return !isInterrupted && uninterruptedPatrolDays >= stablePatrolDays;
        }

        public static float RetainOrRollStablePatrolDays(bool hasExistingTarget, float stablePatrolDays,
            System.Func<float> rollStablePatrolDays)
        {
            return hasExistingTarget ? stablePatrolDays : rollStablePatrolDays();
        }

        public sealed class PatrolState
        {
            [SaveableField(1)]
            private Settlement? _target;

            [SaveableField(2)]
            private float _uninterruptedPatrolDays;

            [SaveableField(3)]
            private float _stablePatrolDays;

            public Settlement? Target
            {
                get => _target;
                set => _target = value;
            }

            public float UninterruptedPatrolDays
            {
                get => _uninterruptedPatrolDays;
                set => _uninterruptedPatrolDays = value;
            }

            public float StablePatrolDays
            {
                get => _stablePatrolDays;
                set => _stablePatrolDays = value;
            }
        }
    }
}
