using CustomSpawns.Invasions;
using NUnit.Framework;

namespace CustomSpawns.Tests.Invasions
{
    public class InvasionCoalitionPlayerChoiceTests
    {
        [Test]
        public void ThePlayersKingdomStandsOutsideACoalitionUntilThePlayerJoins()
        {
            InvasionCoalitionPlayerChoice choice = new();

            Assert.That(choice.Includes(true), Is.False);
            Assert.That(choice.Includes(false), Is.True);

            choice.Join();

            Assert.That(choice.Includes(true), Is.True);
        }

        [Test]
        public void ADeclinedInvitationKeepsThePlayersKingdomOut()
        {
            InvasionCoalitionPlayerChoice choice = new();

            choice.Decline();

            Assert.That(choice.Includes(true), Is.False);
        }

        [Test]
        public void AnOpenQuestionIsNotAskedTwice()
        {
            InvasionCoalitionPlayerChoice choice = new();

            choice.Ask();

            Assert.That(choice.ShouldAsk, Is.False);
        }

        [Test]
        public void TheAnswerIsSavedButAnOpenQuestionIsAskedAgainOnceLoaded()
        {
            InvasionCoalitionPlayerChoice joined = new();
            joined.Join();
            InvasionCoalitionPlayerChoice asking = new();
            asking.Ask();
            InvasionCoalitionPlayerChoice restoredJoined = new();
            InvasionCoalitionPlayerChoice restoredAsking = new();

            restoredJoined.Restore(joined.Export());
            restoredAsking.Restore(asking.Export());

            Assert.That(restoredJoined.Answer, Is.EqualTo(InvasionCoalitionAnswer.Joined));
            Assert.That(restoredAsking.ShouldAsk, Is.True);
        }

        [Test]
        public void AMissingSaveLeavesThePlayerUnasked()
        {
            InvasionCoalitionPlayerChoice choice = new();

            choice.Restore(null);

            Assert.That(choice.Answer, Is.EqualTo(InvasionCoalitionAnswer.Unasked));
        }
    }
}
