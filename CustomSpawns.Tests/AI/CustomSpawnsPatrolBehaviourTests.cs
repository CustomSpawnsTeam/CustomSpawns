using System.Collections.Generic;
using CustomSpawns.AI;
using NUnit.Framework;
using TaleWorlds.CampaignSystem;

namespace CustomSpawns.Tests.AI
{
    public class CustomSpawnsPatrolBehaviourTests
    {
        [Test]
        public void ShouldAddPatrolScore_IgnoresUnregisteredParties()
        {
            Assert.False(CustomSpawnsPatrolBehaviour.ShouldAddPatrolScore(false, true));
        }

        [Test]
        public void ShouldAddPatrolScore_RequiresHomeSettlement()
        {
            Assert.False(CustomSpawnsPatrolBehaviour.ShouldAddPatrolScore(true, false));
        }

        [Test]
        public void ShouldAddPatrolScore_UsesDominantScoreForRegisteredPartyWithHome()
        {
            Assert.True(CustomSpawnsPatrolBehaviour.ShouldAddPatrolScore(true, true));
            Assert.AreEqual(1000000f, CustomSpawnsPatrolBehaviour.DominantPatrolScore);
        }

        [Test]
        public void ShouldAddPatrolScore_DoesNotDependOnCurrentDefaultBehavior()
        {
            Assert.True(CustomSpawnsPatrolBehaviour.ShouldAddPatrolScore(true, true));
        }

        [Test]
        public void ApplyDominantPatrolScore_AddsMissingPatrolCandidate()
        {
            AIBehaviorData patrolBehavior = default;
            PatrolScoreSink scoreSink = new PatrolScoreSink();

            CustomSpawnsPatrolBehaviour.ApplyDominantPatrolScore(scoreSink, patrolBehavior);

            Assert.That(scoreSink.Scores, Has.Count.EqualTo(1));
            Assert.That(scoreSink.Scores[0].Score,
                Is.EqualTo(CustomSpawnsPatrolBehaviour.DominantPatrolScore));
        }

        [Test]
        public void ApplyDominantPatrolScore_OverwritesExistingPatrolCandidateWithoutDuplication()
        {
            AIBehaviorData patrolBehavior = default;
            PatrolScoreSink scoreSink = new PatrolScoreSink();
            scoreSink.Scores.Add((patrolBehavior, 1f));

            CustomSpawnsPatrolBehaviour.ApplyDominantPatrolScore(scoreSink, patrolBehavior);

            Assert.That(scoreSink.Scores, Has.Count.EqualTo(1));
            Assert.That(scoreSink.Scores[0].Score,
                Is.EqualTo(CustomSpawnsPatrolBehaviour.DominantPatrolScore));
        }

        private sealed class PatrolScoreSink : IPatrolScoreSink
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
