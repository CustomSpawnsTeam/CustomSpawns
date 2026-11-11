using System.Linq;
using CustomSpawns.Invasions;
using NUnit.Framework;

namespace CustomSpawns.Tests.Invasions
{
    public class InvasionTargetChoiceTests
    {
        private InvasionConquests _invasionConquests = new();
        private InvasionTargetChoice _invasionTargetChoice = new();

        [SetUp]
        public void SetUp()
        {
            _invasionConquests = new InvasionConquests();
            _invasionTargetChoice = new InvasionTargetChoice();
        }

        [Test]
        public void AnInvaderTurnsOnItsWeakestNeighbour()
        {
            string? next = _invasionTargetChoice.FindNextTarget(new[]
            {
                new InvasionNeighbour("empire_w", 12900f),
                new InvasionNeighbour("khuzait", 4800f),
                new InvasionNeighbour("aserai", 7300f)
            });

            Assert.That(next, Is.EqualTo("khuzait"));
        }

        [Test]
        public void AnInvaderWithoutNeighboursHasNothingToTurnOn()
        {
            Assert.That(_invasionTargetChoice.FindNextTarget(new InvasionNeighbour[0]), Is.Null);
        }

        [Test]
        public void AnInvaderTurnsOnTheNeighbourWithTheMostFortificationsInReachForItsStrength()
        {
            string? next = _invasionTargetChoice.FindNextTarget(new[]
            {
                new InvasionNeighbour("khuzait", 4800f, 1),
                new InvasionNeighbour("battania", 6000f, 5)
            });

            Assert.That(next, Is.EqualTo("battania"));
        }

        [Test]
        public void AnInvaderTurnsOnANeighbourItAlreadyFightsOrThatAnAllyConquers()
        {
            InvasionNeighbour[] neighbours =
            {
                new("khuzait", 4800f, 2),
                new("battania", 6000f, 2, isAtWar: true),
                new("aserai", 6000f, 2)
            };

            Assert.Multiple(() =>
            {
                Assert.That(_invasionTargetChoice.FindNextTarget(neighbours), Is.EqualTo("battania"));
                Assert.That(_invasionTargetChoice.FindNextTarget(neighbours.Where(n => n.KingdomId != "battania"),
                    new[] { "aserai" }), Is.EqualTo("aserai"));
            });
        }

        [Test]
        public void AnInvaderTurnsOnANeighbourThatHasBecomeAMuchBetterPrey()
        {
            InvasionConquestRules rules = new(0.05f, 0f, 4f, 30f);
            InvasionConquest conquest = _invasionConquests.Begin("humar", "empire_w", 10)!;

            string? next = _invasionTargetChoice.Reconsider(conquest, rules, new[]
            {
                new InvasionNeighbour("empire_w", 9000f, 1, isAtWar: true),
                new InvasionNeighbour("battania", 7000f, 6, isAtWar: true)
            }, new string[0], 40);

            Assert.That(next, Is.EqualTo("battania"));
        }

        [Test]
        public void AnInvaderKeepsItsTargetUnlessAnotherIsMuchTheBetterPrey()
        {
            InvasionConquestRules rules = new(0.05f, 0f, 4f, 30f);
            InvasionConquest conquest = _invasionConquests.Begin("humar", "empire_w", 10)!;

            string? next = _invasionTargetChoice.Reconsider(conquest, rules, new[]
            {
                new InvasionNeighbour("empire_w", 9000f, 3, isAtWar: true),
                new InvasionNeighbour("battania", 8000f, 4, isAtWar: true)
            }, new string[0], 40);

            Assert.Multiple(() =>
            {
                Assert.That(next, Is.Null);
                Assert.That(conquest.LastReconsideredDay, Is.EqualTo(40));
            });
        }

        [Test]
        public void AnInvaderTurnsAwayFromATargetNoLongerOnItsBorders()
        {
            InvasionConquestRules rules = new(0.05f, 0f, 4f, 30f);
            InvasionConquest conquest = _invasionConquests.Begin("humar", "vlandia", 10)!;

            string? next = _invasionTargetChoice.Reconsider(conquest, rules, new[]
            {
                new InvasionNeighbour("battania", 12000f, 1, isAtWar: true)
            }, new string[0], 40);

            Assert.That(next, Is.EqualTo("battania"));
        }

        [TestCase(0f, 400)]
        [TestCase(30f, 39)]
        public void AnInvaderOnlyReconsidersItsTargetOnceEveryInterval(float intervalInDays, double today)
        {
            InvasionConquestRules rules = new(0.05f, 0f, 4f, intervalInDays);
            InvasionConquest conquest = _invasionConquests.Begin("humar", "vlandia", 10)!;

            string? next = _invasionTargetChoice.Reconsider(conquest, rules, new[]
            {
                new InvasionNeighbour("battania", 1000f, 6, isAtWar: true)
            }, new string[0], today);

            Assert.Multiple(() =>
            {
                Assert.That(next, Is.Null);
                Assert.That(conquest.LastReconsideredDay, Is.EqualTo(10));
            });
        }
    }
}
