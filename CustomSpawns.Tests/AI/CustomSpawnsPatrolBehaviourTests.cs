using System.Collections.Generic;
using CustomSpawns.AI;
using NUnit.Framework;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

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
        public void ShouldAddPatrolScore_RequiresPatrolTarget()
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

        [TestCase(AiBehavior.EngageParty)]
        [TestCase(AiBehavior.GoAroundParty)]
        [TestCase(AiBehavior.JoinParty)]
        [TestCase(AiBehavior.FleeToPoint)]
        public void IsPatrolInterrupted_RecognizesTemporaryPartyActions(AiBehavior behavior)
        {
            Assert.True(CustomSpawnsPatrolBehaviour.IsPatrolInterrupted(behavior, AiBehavior.None));
            Assert.True(CustomSpawnsPatrolBehaviour.IsPatrolInterrupted(AiBehavior.None, behavior));
        }

        [Test]
        public void UpdateUninterruptedPatrolDays_ResetsWhenInterrupted()
        {
            Assert.That(CustomSpawnsPatrolBehaviour.UpdateUninterruptedPatrolDays(3f, true), Is.Zero);
        }

        [Test]
        public void UpdateUninterruptedPatrolDays_AdvancesOneHourWhenUninterrupted()
        {
            Assert.That(CustomSpawnsPatrolBehaviour.UpdateUninterruptedPatrolDays(1f, false),
                Is.EqualTo(1f + 1f / 24f));
        }

        [Test]
        public void ShouldSwitchPatrolTarget_RequiresStablePeriodToElapse()
        {
            Assert.False(CustomSpawnsPatrolBehaviour.ShouldSwitchPatrolTarget(1.99f, 2f, false));
            Assert.True(CustomSpawnsPatrolBehaviour.ShouldSwitchPatrolTarget(2f, 2f, false));
        }

        [Test]
        public void ShouldSwitchPatrolTarget_DoesNotSwitchWhileInterrupted()
        {
            Assert.False(CustomSpawnsPatrolBehaviour.ShouldSwitchPatrolTarget(0f, 0f, true));
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
