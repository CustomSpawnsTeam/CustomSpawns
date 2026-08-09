using CustomSpawns.Spawn;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace CustomSpawns.AI
{
    public class CustomSpawnsPatrolBehaviour : CampaignBehaviorBase
    {
        internal const float DominantPatrolScore = 1000000f;

        private readonly DynamicSpawnData _dynamicSpawnData;

        public CustomSpawnsPatrolBehaviour(DynamicSpawnData dynamicSpawnData)
        {
            _dynamicSpawnData = dynamicSpawnData;
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
            var homeSettlement = party.HomeSettlement;
            if (!ShouldAddPatrolScore(_dynamicSpawnData.GetDynamicSpawnData(party) is not null,
                    homeSettlement is not null))
            {
                return;
            }

            AIBehaviorData patrolBehavior = new AIBehaviorData(homeSettlement,
                AiBehavior.PatrolAroundPoint, party.NavigationCapability, false, false, false);
            ApplyDominantPatrolScore(new PartyThinkParamsPatrolScoreSink(thinkParams), patrolBehavior);
        }

        internal static bool ShouldAddPatrolScore(bool isRegisteredCustomSpawnsParty, bool hasHomeSettlement)
        {
            return isRegisteredCustomSpawnsParty && hasHomeSettlement;
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
    }

    internal interface IPatrolScoreSink
    {
        bool TryGetBehaviorScore(AIBehaviorData behavior, out float score);
        void SetBehaviorScore(AIBehaviorData behavior, float score);
        void AddBehaviorScore(AIBehaviorData behavior, float score);
    }
}
