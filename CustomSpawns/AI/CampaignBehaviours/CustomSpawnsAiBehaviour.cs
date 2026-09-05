using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using CustomSpawns.AI.Behaviours;
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
        private readonly IReadOnlyList<ISyncsCampaignData> _syncTargets;

        public CustomSpawnsAiBehaviour(DynamicSpawnData dynamicSpawnData,
            PartyBehaviourResolver partyBehaviourResolver, IEnumerable<ISyncsCampaignData> syncTargets)
        {
            _dynamicSpawnData = dynamicSpawnData;
            _partyBehaviourResolver = partyBehaviourResolver;

            var distinctTargets = new HashSet<object>(ReferenceEqualityComparer.Instance);
            var targets = new List<ISyncsCampaignData>();
            foreach (ISyncsCampaignData syncTarget in syncTargets ??
                     throw new ArgumentNullException(nameof(syncTargets)))
            {
                if (syncTarget is null)
                {
                    throw new ArgumentException("Campaign data sync targets cannot contain null.",
                        nameof(syncTargets));
                }

                if (distinctTargets.Add(syncTarget))
                {
                    targets.Add(syncTarget);
                }
            }

            _syncTargets = targets;
        }

        public override void RegisterEvents()
        {
            CampaignEvents.AiHourlyTickEvent.AddNonSerializedListener(this, AddBehaviorScore);
        }

        public override void SyncData(IDataStore dataStore)
        {
            foreach (ISyncsCampaignData syncTarget in _syncTargets)
            {
                syncTarget.SyncData(dataStore);
            }
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

    internal sealed class ReferenceEqualityComparer : IEqualityComparer<object>
    {
        internal static ReferenceEqualityComparer Instance { get; } = new();

        public new bool Equals(object? x, object? y)
        {
            return ReferenceEquals(x, y);
        }

        public int GetHashCode(object obj)
        {
            return RuntimeHelpers.GetHashCode(obj);
        }
    }

    internal interface IAiBehaviorScoreSink
    {
        bool TryGetBehaviorScore(AIBehaviorData behavior, out float score);
        void SetBehaviorScore(AIBehaviorData behavior, float score);
        void AddBehaviorScore(AIBehaviorData behavior, float score);
    }
}
