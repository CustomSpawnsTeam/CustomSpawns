using System.Collections.Generic;
using CustomSpawns.Spawn;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace CustomSpawns.AI
{
    public class CustomSpawnsAiBehaviour : CampaignBehaviorBase
    {
        internal const float DominantAiBehaviorScore = 1000000f;

        private readonly DynamicSpawnData _dynamicSpawnData;
        private readonly IReadOnlyList<IPriorityPartyBehaviour> _priorityPartyBehaviours;

        public CustomSpawnsAiBehaviour(DynamicSpawnData dynamicSpawnData,
            IReadOnlyList<IPriorityPartyBehaviour> priorityPartyBehaviours)
        {
            _dynamicSpawnData = dynamicSpawnData;
            _priorityPartyBehaviours = priorityPartyBehaviours;
        }

        public override void RegisterEvents()
        {
            CampaignEvents.AiHourlyTickEvent.AddNonSerializedListener(this, AddBehaviorScore);
        }

        public override void SyncData(IDataStore dataStore)
        {
        }

        private void AddBehaviorScore(MobileParty party, PartyThinkParams thinkParams)
        {
            CsPartyData? partyData = _dynamicSpawnData.GetDynamicSpawnData(party);
            if (partyData is null)
            {
                return;
            }

            AIBehaviorData? behavior = ResolvePriorityBehaviour(_priorityPartyBehaviours, party, partyData);
            if (!behavior.HasValue)
            {
                return;
            }

            ApplyDominantBehaviorScore(new PartyThinkParamsAiBehaviorScoreSink(thinkParams), behavior.Value);
        }

        internal static AIBehaviorData? ResolvePriorityBehaviour(IEnumerable<IPriorityPartyBehaviour> priorityPartyBehaviours,
            MobileParty party, CsPartyData partyData)
        {
            foreach (IPriorityPartyBehaviour priorityPartyBehaviour in priorityPartyBehaviours)
            {
                PartyBehaviourResult result = priorityPartyBehaviour.GetPriorityBehaviour(party, partyData);
                if (result.IsApplicable)
                {
                    return result.AIBehavior;
                }
            }

            return null;
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
