using System.Collections.Generic;
using CustomSpawns.AI;
using CustomSpawns.Spawn;
using NUnit.Framework;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace CustomSpawns.Tests.AI
{
    public class CustomSpawnsAiBehaviourTests
    {
        [Test]
        public void ResolvePriorityBehaviour_UsesFirstApplicableImplementation()
        {
            AIBehaviorData expected = default;
            PriorityPartyBehaviourStub skipped = new(PartyBehaviourResult.NotApplicable);
            PriorityPartyBehaviourStub selected = new(PartyBehaviourResult.From(expected));
            PriorityPartyBehaviourStub fallback = new(PartyBehaviourResult.From(expected));

            AIBehaviorData? actual = CustomSpawnsAiBehaviour.ResolvePriorityBehaviour(
                new IPriorityPartyBehaviour[] { skipped, selected, fallback }, null!, CreatePartyData());

            Assert.That(actual, Is.EqualTo(expected));
            Assert.That(skipped.CallCount, Is.EqualTo(1));
            Assert.That(selected.CallCount, Is.EqualTo(1));
            Assert.That(fallback.CallCount, Is.Zero);
        }

        [Test]
        public void ResolvePriorityBehaviour_DoesNotFallBackWhenApplicableImplementationHasNoBehavior()
        {
            PriorityPartyBehaviourStub selected = new(PartyBehaviourResult.ApplicableWithoutBehavior);
            PriorityPartyBehaviourStub fallback = new(PartyBehaviourResult.From(default));

            AIBehaviorData? actual = CustomSpawnsAiBehaviour.ResolvePriorityBehaviour(
                new IPriorityPartyBehaviour[] { selected, fallback }, null!, CreatePartyData());

            Assert.That(actual, Is.Null);
            Assert.That(selected.CallCount, Is.EqualTo(1));
            Assert.That(fallback.CallCount, Is.Zero);
        }

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

        private sealed class PriorityPartyBehaviourStub : IPriorityPartyBehaviour
        {
            private readonly PartyBehaviourResult _result;

            public PriorityPartyBehaviourStub(PartyBehaviourResult result)
            {
                _result = result;
            }

            public int CallCount { get; private set; }

            public PartyBehaviourResult GetPriorityBehaviour(MobileParty party, CsPartyData partyData)
            {
                CallCount++;
                return _result;
            }
        }

        private static CsPartyData CreatePartyData()
        {
            return new CsPartyData("party", "spawn", true, null);
        }
    }
}
