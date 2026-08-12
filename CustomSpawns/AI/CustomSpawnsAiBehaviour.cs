using CustomSpawns.Spawn;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace CustomSpawns.AI
{
    public class CustomSpawnsAiBehaviour : CampaignBehaviorBase
    {
        internal const float DominantAiBehaviorScore = 1000000f;

        private readonly DynamicSpawnData _dynamicSpawnData;
        private readonly PartyBehaviourResolver _partyBehaviourResolver;

        public CustomSpawnsAiBehaviour(DynamicSpawnData dynamicSpawnData,
            PartyBehaviourResolver partyBehaviourResolver)
        {
            _dynamicSpawnData = dynamicSpawnData;
            _partyBehaviourResolver = partyBehaviourResolver;
        }

        public override void RegisterEvents()
        {
            CampaignEvents.AiHourlyTickEvent.AddNonSerializedListener(this, AddBehaviorScore);
        }

        public override void SyncData(IDataStore dataStore)
        {
            _partyBehaviourResolver.SyncData(dataStore);
        }

        private void AddBehaviorScore(MobileParty party, PartyThinkParams thinkParams)
        {
            CsPartyData? partyData = _dynamicSpawnData.GetDynamicSpawnData(party);
            if (partyData is null)
            {
                return;
            }

            AIBehaviorData? behavior = _partyBehaviourResolver.Resolve(party, partyData).AIBehavior;
            if (!behavior.HasValue)
            {
                return;
            }

            ApplyDominantBehaviorScore(new PartyThinkParamsAiBehaviorScoreSink(thinkParams), behavior.Value);
        }

        internal static void ApplyDominantBehaviorScore(IAiBehaviorScoreSink scoreSink,
            AIBehaviorData behavior)
        {
            if (scoreSink.TryGetBehaviorScore(behavior, out _))
            {
                scoreSink.SetBehaviorScore(behavior, DominantAiBehaviorScore);
                return;
            }

            scoreSink.AddBehaviorScore(behavior, DominantAiBehaviorScore);
        }

        private sealed class PartyThinkParamsAiBehaviorScoreSink : IAiBehaviorScoreSink
        {
            private readonly PartyThinkParams _thinkParams;

            public PartyThinkParamsAiBehaviorScoreSink(PartyThinkParams thinkParams)
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

    internal interface IAiBehaviorScoreSink
    {
        bool TryGetBehaviorScore(AIBehaviorData behavior, out float score);
        void SetBehaviorScore(AIBehaviorData behavior, float score);
        void AddBehaviorScore(AIBehaviorData behavior, float score);
    }
}
