using System.Collections.Generic;
using CustomSpawns.AI;
using NUnit.Framework;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace CustomSpawns.Tests.AI
{
    public class CustomSpawnsAiBehaviourTests
    {
        [Test]
        public void ApplyDominantBehaviorScore_AddsMissingBehaviorScoreCandidate()
        {
            AIBehaviorData behavior = default;
            AiBehaviorScoreSink scoreSink = new AiBehaviorScoreSink();

            CustomSpawnsAiBehaviour.ApplyDominantBehaviorScore(scoreSink, behavior);

            Assert.That(scoreSink.Scores, Has.Count.EqualTo(1));
            Assert.That(scoreSink.Scores[0].Score,
                Is.EqualTo(CustomSpawnsAiBehaviour.DominantAiBehaviorScore));
        }

        [Test]
        public void ApplyDominantBehaviorScore_OverwritesExistingBehaviorScoreCandidateWithoutDuplication()
        {
            AIBehaviorData behavior = default;
            AiBehaviorScoreSink scoreSink = new AiBehaviorScoreSink();
            scoreSink.Scores.Add((behavior, 1f));

            CustomSpawnsAiBehaviour.ApplyDominantBehaviorScore(scoreSink, behavior);

            Assert.That(scoreSink.Scores, Has.Count.EqualTo(1));
            Assert.That(scoreSink.Scores[0].Score,
                Is.EqualTo(CustomSpawnsAiBehaviour.DominantAiBehaviorScore));
        }

        private sealed class AiBehaviorScoreSink : IAiBehaviorScoreSink
        {
            public List<(AIBehaviorData Behavior, float Score)> Scores { get; } = new();

            public bool TryGetBehaviorScore(AIBehaviorData behavior, out float score)
            {
                for (int index = 0; index < Scores.Count; index++)
                {
                    if (Scores[index].Behavior.Equals(behavior))
                    {
                        score = Scores[index].Score;
                        return true;
                    }
                }

                score = 0f;
                return false;
            }

            public void SetBehaviorScore(AIBehaviorData behavior, float score)
            {
                for (int index = 0; index < Scores.Count; index++)
                {
                    if (Scores[index].Behavior.Equals(behavior))
                    {
                        Scores[index] = (behavior, score);
                        return;
                    }
                }

                Assert.Fail("The score sink must only set an existing candidate.");
            }

            public void AddBehaviorScore(AIBehaviorData behavior, float score)
            {
                Scores.Add((behavior, score));
            }
        }

    }
}
