using System.Collections.Generic;
using CustomSpawns.Data.Dao;
using CustomSpawns.Data.Dto;
using CustomSpawns.Spawn;
using CustomSpawns.Utils;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace CustomSpawns.AI
{
    public sealed class PatrolAroundClosestLestInterruptedAndSwitchBehaviour : IPriorityPartyBehaviour
    {
        private const float PatrolDaysPerHour = 1f / 24f;

        private readonly SpawnDao _spawnDao;
        private readonly Dictionary<MobileParty, PatrolState> _patrolStates = new();

        public PatrolAroundClosestLestInterruptedAndSwitchBehaviour(SpawnDao spawnDao)
        {
            _spawnDao = spawnDao;
        }

        public PartyBehaviourResult GetPriorityBehaviour(MobileParty party, CsPartyData partyData)
        {
            PatrolAroundClosestLestInterruptedAndSwitchData? patrolConfig =
                partyData.OwningSpawnTemplateId is null
                    ? null
                    : _spawnDao.FindByPartyTemplateId(partyData.OwningSpawnTemplateId)
                        ?.PatrolAroundClosestLestInterruptedAndSwitch;
            if (patrolConfig is null)
            {
                return PartyBehaviourResult.NotApplicable;
            }

            Settlement? patrolTarget = GetPatrolTarget(party, patrolConfig);
            if (patrolTarget is null)
            {
                return PartyBehaviourResult.ApplicableWithoutBehavior;
            }

            return PartyBehaviourResult.From(new AIBehaviorData(patrolTarget,
                AiBehavior.PatrolAroundPoint, party.NavigationCapability, false, false, false));
        }

        private Settlement? GetPatrolTarget(MobileParty party,
            PatrolAroundClosestLestInterruptedAndSwitchData patrolConfig)
        {
            if (!_patrolStates.TryGetValue(party, out PatrolState? patrolState))
            {
                patrolState = new PatrolState();
                _patrolStates.Add(party, patrolState);
            }

            if (patrolState.Target is null)
            {
                patrolState.Target = GetNextPatrolTarget(party, patrolConfig, null);
                patrolState.StablePatrolDays = RollStablePatrolDays(patrolConfig);
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

        private sealed class PatrolState
        {
            public Settlement? Target { get; set; }
            public float UninterruptedPatrolDays { get; set; }
            public float StablePatrolDays { get; set; }
        }
    }
}
