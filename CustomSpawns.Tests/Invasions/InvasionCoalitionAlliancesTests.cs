using System;
using System.Collections.Generic;
using CustomSpawns.Invasions;
using Moq;
using NUnit.Framework;

namespace CustomSpawns.Tests.Invasions
{
    public class InvasionCoalitionAlliancesTests
    {
        private Mock<IInvasionCoalitionMembers> _invasionCoalitionMembers = null!;
        private Mock<IInvasionKingdomAlliances> _invasionKingdomAlliances = null!;
        private Mock<IInvasionCoalitionInvitations> _invasionCoalitionInvitations = null!;
        private InvasionCoalitionPlayerChoice _invasionCoalitionPlayerChoice = null!;
        private Action? _join;
        private Action? _decline;

        [SetUp]
        public void SetUp()
        {
            _invasionCoalitionMembers = new Mock<IInvasionCoalitionMembers>();
            _invasionKingdomAlliances = new Mock<IInvasionKingdomAlliances>();
            _invasionCoalitionInvitations = new Mock<IInvasionCoalitionInvitations>();
            _invasionCoalitionPlayerChoice = new InvasionCoalitionPlayerChoice();
            _invasionCoalitionInvitations.Setup(i => i.AskToJoin(It.IsAny<Action>(), It.IsAny<Action>()))
                .Callback<Action, Action>((join, decline) =>
                {
                    _join = join;
                    _decline = decline;
                });
        }

        [Test]
        public void EveryTwoKingdomsOfACoalitionAreAllied()
        {
            Members("khuzait", "battania", "aserai");

            Alliances().KeepAlliances();

            _invasionKingdomAlliances.Verify(a => a.Ally("khuzait", "battania"), Times.Once);
            _invasionKingdomAlliances.Verify(a => a.Ally("khuzait", "aserai"), Times.Once);
            _invasionKingdomAlliances.Verify(a => a.Ally("battania", "aserai"), Times.Once);
            _invasionKingdomAlliances.VerifyNoOtherCalls();
        }

        [Test]
        public void NoKingdomIsAlliedWhileNoCoalitionHolds()
        {
            Members();

            Alliances().KeepAlliances();

            _invasionKingdomAlliances.VerifyNoOtherCalls();
        }

        [Test]
        public void ALoneKingdomHasNoOneToAllyWith()
        {
            Members("vlandia");

            Alliances().KeepAlliances();

            _invasionKingdomAlliances.VerifyNoOtherCalls();
        }

        [Test]
        public void ThePlayerIsAskedOnceToJoinACoalition()
        {
            Members("khuzait", "battania");
            _invasionCoalitionInvitations.Setup(i => i.FindPlayerKingdomId()).Returns("player_kingdom");
            InvasionCoalitionAlliances alliances = Alliances();

            alliances.KeepAlliances();
            alliances.KeepAlliances();

            _invasionCoalitionInvitations.Verify(i => i.AskToJoin(It.IsAny<Action>(), It.IsAny<Action>()),
                Times.Once);
        }

        [Test]
        public void APlayerWhoRulesNoKingdomIsNotAsked()
        {
            Members("khuzait", "battania");

            Alliances().KeepAlliances();

            _invasionCoalitionInvitations.Verify(i => i.AskToJoin(It.IsAny<Action>(), It.IsAny<Action>()),
                Times.Never);
        }

        [Test]
        public void APlayerWhoJoinsIsAlliedWithTheCoalitionAtOnce()
        {
            Members("khuzait", "battania");
            _invasionCoalitionInvitations.Setup(i => i.FindPlayerKingdomId()).Returns("player_kingdom");
            Alliances().KeepAlliances();
            Members("khuzait", "battania", "player_kingdom");

            _join!();

            Assert.That(_invasionCoalitionPlayerChoice.Answer, Is.EqualTo(InvasionCoalitionAnswer.Joined));
            _invasionKingdomAlliances.Verify(a => a.Ally("khuzait", "player_kingdom"), Times.Once);
            _invasionKingdomAlliances.Verify(a => a.Ally("battania", "player_kingdom"), Times.Once);
        }

        [Test]
        public void APlayerWhoDeclinesIsNotAskedAgainWhileTheCoalitionHolds()
        {
            Members("khuzait", "battania");
            _invasionCoalitionInvitations.Setup(i => i.FindPlayerKingdomId()).Returns("player_kingdom");
            InvasionCoalitionAlliances alliances = Alliances();
            alliances.KeepAlliances();

            _decline!();
            alliances.KeepAlliances();

            Assert.That(_invasionCoalitionPlayerChoice.Answer, Is.EqualTo(InvasionCoalitionAnswer.Declined));
            _invasionCoalitionInvitations.Verify(i => i.AskToJoin(It.IsAny<Action>(), It.IsAny<Action>()),
                Times.Once);
        }

        [Test]
        public void TheNextCoalitionAsksThePlayerAgain()
        {
            _invasionCoalitionInvitations.Setup(i => i.FindPlayerKingdomId()).Returns("player_kingdom");
            InvasionCoalitionAlliances alliances = Alliances();
            Members("khuzait", "battania");
            alliances.KeepAlliances();
            _decline!();

            Members();
            alliances.KeepAlliances();
            Members("khuzait", "battania");
            alliances.KeepAlliances();

            _invasionCoalitionInvitations.Verify(i => i.AskToJoin(It.IsAny<Action>(), It.IsAny<Action>()),
                Times.Exactly(2));
        }

        private void Members(params string[] kingdomIds) =>
            _invasionCoalitionMembers.Setup(m => m.FindCoalitionMembers()).Returns(new List<string>(kingdomIds));

        private InvasionCoalitionAlliances Alliances() =>
            new(_invasionCoalitionMembers.Object, _invasionKingdomAlliances.Object,
                _invasionCoalitionInvitations.Object, _invasionCoalitionPlayerChoice);
    }
}
