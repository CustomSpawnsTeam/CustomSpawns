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
    public class CustomSpawnsPatrolBehaviour : CampaignBehaviorBase
    {
        internal const float DominantPatrolScore = 1000000f;
        private const float PatrolDaysPerHour = 1f / 24f;

        private readonly DynamicSpawnData _dynamicSpawnData;
        private readonly SpawnDao _spawnDao;
        private readonly Dictionary<MobileParty, PatrolState> _patrolStates = new();

        public CustomSpawnsPatrolBehaviour(DynamicSpawnData dynamicSpawnData, SpawnDao spawnDao)
        {
            _dynamicSpawnData = dynamicSpawnData;
            _spawnDao = spawnDao;
        }

        public override void RegisterEvents()
        {
            CampaignEvents.AiHourlyTickEvent.AddNonSerializedListener(this, AddPatrolScore);
        }

        public override void SyncData(IDataStore dataStore)
        {
        }

        private void AddPatrolScore(MobileParty party, PartyThinkParams thinkParams)
        {
            CsPartyData? partyData = _dynamicSpawnData.GetDynamicSpawnData(party);
            Settlement? patrolTarget = partyData is null ? null : GetPatrolTarget(party, partyData);
            if (!ShouldAddPatrolScore(partyData is not null, patrolTarget is not null))
            {
                return;
            }

            AIBehaviorData patrolBehavior = new AIBehaviorData(patrolTarget!,
                AiBehavior.PatrolAroundPoint, party.NavigationCapability, false, false, false);
            ApplyDominantPatrolScore(new PartyThinkParamsPatrolScoreSink(thinkParams), patrolBehavior);
        }

        private Settlement? GetPatrolTarget(MobileParty party, CsPartyData partyData)
        {
            PatrolAroundClosestLestInterruptedAndSwitchData? patrolConfig = partyData.OwningSpawnTemplateId is null
                ? null
                : _spawnDao.FindByPartyTemplateId(partyData.OwningSpawnTemplateId)
                    ?.PatrolAroundClosestLestInterruptedAndSwitch;
            if (patrolConfig is null)
            {
                return party.HomeSettlement;
            }

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

        internal static bool ShouldAddPatrolScore(bool isRegisteredCustomSpawnsParty, bool hasPatrolTarget)
        {
            return isRegisteredCustomSpawnsParty && hasPatrolTarget;
        }

        internal static void ApplyDominantPatrolScore(IPatrolScoreSink scoreSink,
            AIBehaviorData patrolBehavior)
        {
            if (scoreSink.TryGetBehaviorScore(patrolBehavior, out _))
            {
                scoreSink.SetBehaviorScore(patrolBehavior, DominantPatrolScore);
                return;
            }

            scoreSink.AddBehaviorScore(patrolBehavior, DominantPatrolScore);
        }

        private sealed class PartyThinkParamsPatrolScoreSink : IPatrolScoreSink
        {
            private readonly PartyThinkParams _thinkParams;

            public PartyThinkParamsPatrolScoreSink(PartyThinkParams thinkParams)
            {
                _thinkParams = thinkParams;
            }

            public bool TryGetBehaviorScore(AIBehaviorData behavior, out float score)
            {
                return _thinkParams.TryGetBehaviorScore(behavior, out score);
            }

            public void SetBehaviorScore(AIBehaviorData behavior, float score)
            {
                _thinkParams.SetBehaviorScore(behavior, score);
            }

            public void AddBehaviorScore(AIBehaviorData behavior, float score)
            {
                _thinkParams.AddBehaviorScore((behavior, score));
            }
        }

        private sealed class PatrolState
        {
            public Settlement? Target { get; set; }
            public float UninterruptedPatrolDays { get; set; }
            public float StablePatrolDays { get; set; }
        }
    }

    internal interface IPatrolScoreSink
    {
        bool TryGetBehaviorScore(AIBehaviorData behavior, out float score);
        void SetBehaviorScore(AIBehaviorData behavior, float score);
        void AddBehaviorScore(AIBehaviorData behavior, float score);
    }
}
