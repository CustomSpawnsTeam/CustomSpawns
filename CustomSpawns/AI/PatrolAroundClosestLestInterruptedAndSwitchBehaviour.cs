using System.Collections.Generic;
using CustomSpawns.Data.Dto;
using CustomSpawns.Spawn;
using CustomSpawns.Utils;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.SaveSystem;

namespace CustomSpawns.AI
{
    public sealed class PatrolAroundClosestLestInterruptedAndSwitchBehaviour :
        PriorityPartyBehaviour<PatrolAroundClosestLestInterruptedAndSwitchData>
    {
        private const float PatrolDaysPerHour = 1f / 24f;

        private Dictionary<MobileParty, PatrolState> _patrolStates = new();

        public override PartyBehaviourResult GetPriorityBehaviour(MobileParty party, CsPartyData partyData,
            PatrolAroundClosestLestInterruptedAndSwitchData patrolConfig)
        {
            Settlement? patrolTarget = GetPatrolTarget(party, patrolConfig);
            if (patrolTarget is null)
            {
                return PartyBehaviourResult.ApplicableWithoutBehavior;
            }

            return PartyBehaviourResult.From(new AIBehaviorData(patrolTarget,
                AiBehavior.PatrolAroundPoint, party.NavigationCapability, false, false, false));
        }

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData("_customSpawnsPatrolAroundClosestStates", ref _patrolStates);
            _patrolStates ??= new Dictionary<MobileParty, PatrolState>();
        }

        private Settlement? GetPatrolTarget(MobileParty party,
            PatrolAroundClosestLestInterruptedAndSwitchData patrolConfig)
        {
            if (!_patrolStates.TryGetValue(party, out PatrolState? patrolState))
            {
                patrolState = new PatrolState();
                _patrolStates.Add(party, patrolState);
            }

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

        internal static bool IsPatrolInterrupted(AiBehavior defaultBehavior, AiBehavior shortTermBehavior)
        {
            return IsInterruptingBehavior(defaultBehavior) || IsInterruptingBehavior(shortTermBehavior);
        }

        private static bool IsInterruptingBehavior(AiBehavior behavior)
        {
            return behavior == AiBehavior.EngageParty || behavior == AiBehavior.GoAroundParty ||
                   behavior == AiBehavior.JoinParty || behavior == AiBehavior.FleeToPoint;
        }

        internal static float UpdateUninterruptedPatrolDays(float currentDays, bool isInterrupted)
        {
            return isInterrupted ? 0f : currentDays + PatrolDaysPerHour;
        }

        internal static bool ShouldSwitchPatrolTarget(float uninterruptedPatrolDays, float stablePatrolDays,
            bool isInterrupted)
        {
            return !isInterrupted && uninterruptedPatrolDays >= stablePatrolDays;
        }

        internal static float RetainOrRollStablePatrolDays(bool hasExistingTarget, float stablePatrolDays,
            System.Func<float> rollStablePatrolDays)
        {
            return hasExistingTarget ? stablePatrolDays : rollStablePatrolDays();
        }

        internal sealed class PatrolState
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
