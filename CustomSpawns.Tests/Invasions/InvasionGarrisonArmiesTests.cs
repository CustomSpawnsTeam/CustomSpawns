using CustomSpawns.Invasions;
using Moq;
using NUnit.Framework;

namespace CustomSpawns.Tests.Invasions
{
    public class InvasionGarrisonArmiesTests
    {
        private const string ClanId = "cs_humar_clan";
        private const string HeroId = "cs_humar_lord_7";
        private static readonly InvasionGarrisonArmyRules Rules = new(90f, 0.5f, 1000);
        private Mock<IInvasionGarrisonArmyWorld> _world = new();
        private InvasionGarrisonArmyClock _invasionGarrisonArmyClock = new();
        private InvasionGarrisonArmyLords _invasionGarrisonArmyLords = new();
        private InvasionGarrisonArmies _invasionGarrisonArmies = null!;

        [SetUp]
        public void SetUp()
        {
            _world = new Mock<IInvasionGarrisonArmyWorld>();
            _world.Setup(world => world.FindInvasionClanIds()).Returns(new[] { ClanId });
            _world.Setup(world => world.FindRules(ClanId)).Returns(Rules);
            _world.Setup(world => world.FindMenPerFoodPerDay()).Returns(20);
            _world.Setup(world => world.FindMinimumFoodDays()).Returns(7f);
            _world.Setup(world => world.CreateLord(ClanId)).Returns(HeroId);
            _world.Setup(world => world.Raise(ClanId, HeroId, It.IsAny<int>(), It.IsAny<int>())).Returns(true);
            _invasionGarrisonArmyClock = new InvasionGarrisonArmyClock();
            _invasionGarrisonArmyLords = new InvasionGarrisonArmyLords();
            InvasionGarrisonArmySizing sizing = new();
            _invasionGarrisonArmies = new InvasionGarrisonArmies(_world.Object, new InvasionGarrisonArmyMuster(sizing),
                sizing, _invasionGarrisonArmyClock, _invasionGarrisonArmyLords);
        }

        [Test]
        public void AGarrisonOfThreeThousandRaisesFifteenHundredUnderAOneTimeLord()
        {
            Observe(3000);

            _invasionGarrisonArmies.UpdateDaily(10);

            Assert.Multiple(() =>
            {
                _world.Verify(world => world.Raise(ClanId, HeroId, 1500, 2250), Times.Once);
                Assert.That(_invasionGarrisonArmyLords.FindPartySizeLimit(HeroId), Is.EqualTo(1501));
                Assert.That(_invasionGarrisonArmyClock.FindDaysSinceLastRaise(ClanId, 10), Is.EqualTo(0));
            });
        }

        [Test]
        public void TheLordIsKnownBeforeHisPartyIsMade()
        {
            int? limitWhileRaising = null;
            Observe(3000);
            _world.Setup(world => world.Raise(ClanId, HeroId, It.IsAny<int>(), It.IsAny<int>()))
                .Callback(() => limitWhileRaising = _invasionGarrisonArmyLords.FindPartySizeLimit(HeroId))
                .Returns(true);

            _invasionGarrisonArmies.UpdateDaily(10);

            Assert.That(limitWhileRaising, Is.EqualTo(1501));
        }

        [Test]
        public void ARefusedArmyEndsItsLordAndLeavesTheClockAlone()
        {
            Observe(3000);
            _world.Setup(world => world.Raise(ClanId, HeroId, It.IsAny<int>(), It.IsAny<int>())).Returns(false);

            _invasionGarrisonArmies.UpdateDaily(10);

            Assert.Multiple(() =>
            {
                _world.Verify(world => world.End(ClanId, HeroId), Times.Once);
                Assert.That(_invasionGarrisonArmyLords.IsArmyOut(ClanId), Is.False);
                Assert.That(_invasionGarrisonArmyClock.FindDaysSinceLastRaise(ClanId, 10), Is.Null);
            });
        }

        [Test]
        public void WithoutALordNoArmyIsRaised()
        {
            Observe(3000);
            _world.Setup(world => world.CreateLord(ClanId)).Returns((string?)null);

            _invasionGarrisonArmies.UpdateDaily(10);

            Assert.Multiple(() =>
            {
                _world.Verify(world => world.ReportSkip(ClanId, InvasionGarrisonArmyMuster.NoLord), Times.Once);
                _world.Verify(world => world.Raise(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(),
                    It.IsAny<int>()), Times.Never);
            });
        }

        [Test]
        public void NoArmyBeforeTheInterval()
        {
            Observe(3000);
            _invasionGarrisonArmyClock.MarkRaised(ClanId, 10);

            _invasionGarrisonArmies.UpdateDaily(99);

            VerifyNoRaise();
        }

        [Test]
        public void AnArmyAfterTheInterval()
        {
            Observe(3000);
            _invasionGarrisonArmyClock.MarkRaised(ClanId, 10);

            _invasionGarrisonArmies.UpdateDaily(100);

            _world.Verify(world => world.Raise(ClanId, HeroId, It.IsAny<int>(), It.IsAny<int>()), Times.Once);
        }

        [Test]
        public void NoSecondArmyWhileOneIsOutHoweverLongAgoItWasRaised()
        {
            Observe(10000);
            _invasionGarrisonArmyLords.Record(ClanId, "cs_humar_lord_3", 5001);

            _invasionGarrisonArmies.UpdateDaily(500);

            VerifyNoRaise();
        }

        [Test]
        public void TheIntervalCountsFromTheRaiseNotFromTheArmysEnd()
        {
            Observe(3000);
            _invasionGarrisonArmies.UpdateDaily(10);
            _world.Setup(world => world.HasEnded(HeroId)).Returns(true);
            _invasionGarrisonArmies.Review();
            _world.Invocations.Clear();

            _invasionGarrisonArmies.UpdateDaily(100);

            _world.Verify(world => world.Raise(ClanId, HeroId, It.IsAny<int>(), It.IsAny<int>()), Times.Once);
        }

        [Test]
        public void AnEndedArmyIsEndedOnceAndItsLordForgotten()
        {
            _invasionGarrisonArmyLords.Record(ClanId, HeroId, 1001);
            _world.Setup(world => world.HasEnded(HeroId)).Returns(true);

            _invasionGarrisonArmies.Review();
            _invasionGarrisonArmies.Review();

            Assert.Multiple(() =>
            {
                _world.Verify(world => world.End(ClanId, HeroId), Times.Once);
                Assert.That(_invasionGarrisonArmyLords.IsArmyOut(ClanId), Is.False);
            });
        }

        [Test]
        public void AnArmyStillInTheFieldIsLeftToFight()
        {
            _invasionGarrisonArmyLords.Record(ClanId, HeroId, 1001);

            _invasionGarrisonArmies.Review();

            Assert.Multiple(() =>
            {
                _world.Verify(world => world.End(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
                Assert.That(_invasionGarrisonArmyLords.IsArmyOut(ClanId), Is.True);
            });
        }

        [Test]
        public void TheEndOfGatheringIsReportedOnce()
        {
            _invasionGarrisonArmyLords.Record(ClanId, HeroId, 1001);
            _world.Setup(world => world.HasGathered(HeroId)).Returns(true);

            _invasionGarrisonArmies.Review();
            _invasionGarrisonArmies.Review();

            _world.Verify(world => world.ReportGathered(ClanId, HeroId), Times.Once);
        }

        [Test]
        public void ASkipIsReportedOnceUntilItsReasonChanges()
        {
            Observe(1000);

            _invasionGarrisonArmies.UpdateDaily(10);
            _invasionGarrisonArmies.UpdateDaily(11);

            _world.Verify(world => world.ReportSkip(ClanId, InvasionGarrisonArmyMuster.GarrisonAtMinimum),
                Times.Once);
            VerifyNoRaise();
        }

        [Test]
        public void AnInvasionWithoutAGarrisonArmyIsLeftAlone()
        {
            Observe(10000);
            _world.Setup(world => world.FindRules(ClanId)).Returns((InvasionGarrisonArmyRules?)null);

            _invasionGarrisonArmies.UpdateDaily(10);

            _world.Verify(world => world.Observe(It.IsAny<string>()), Times.Never);
        }

        [Test]
        public void ALostCapitalRaisesNothing()
        {
            _world.Setup(world => world.Observe(ClanId)).Returns((InvasionGarrisonArmyFacts?)null);

            _invasionGarrisonArmies.UpdateDaily(10);

            Assert.Multiple(() =>
            {
                VerifyNoRaise();
                _world.Verify(world => world.CreateLord(It.IsAny<string>()), Times.Never);
            });
        }

        private void VerifyNoRaise() => _world.Verify(world => world.Raise(It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<int>(), It.IsAny<int>()), Times.Never);

        private void Observe(int garrisonMen) => _world.Setup(world => world.Observe(ClanId))
            .Returns(new InvasionGarrisonArmyFacts(false, garrisonMen, true));
    }
}
