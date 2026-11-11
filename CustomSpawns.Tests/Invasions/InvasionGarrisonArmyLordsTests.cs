using System.Collections.Generic;
using CustomSpawns.Invasions;
using NUnit.Framework;

namespace CustomSpawns.Tests.Invasions
{
    public class InvasionGarrisonArmyLordsTests
    {
        private const string ClanId = "cs_humar_clan";
        private const string HeroId = "cs_humar_lord_7";
        private InvasionGarrisonArmyLords _invasionGarrisonArmyLords = new();

        [SetUp]
        public void SetUp()
        {
            _invasionGarrisonArmyLords = new InvasionGarrisonArmyLords();
        }

        [Test]
        public void ARecordedLordLeadsAnArmyOfHisLimit()
        {
            _invasionGarrisonArmyLords.Record(ClanId, HeroId, 1001);

            Assert.Multiple(() =>
            {
                Assert.That(_invasionGarrisonArmyLords.IsArmyOut(ClanId), Is.True);
                Assert.That(_invasionGarrisonArmyLords.LeadsGarrisonArmy(HeroId), Is.True);
                Assert.That(_invasionGarrisonArmyLords.FindPartySizeLimit(HeroId), Is.EqualTo(1001));
                Assert.That(_invasionGarrisonArmyLords.Find(ClanId)!.Value.HasGathered, Is.False);
            });
        }

        [Test]
        public void AnyOtherLordLeadsNoGarrisonArmy()
        {
            _invasionGarrisonArmyLords.Record(ClanId, HeroId, 1001);

            Assert.Multiple(() =>
            {
                Assert.That(_invasionGarrisonArmyLords.LeadsGarrisonArmy("cs_humar_lord_1"), Is.False);
                Assert.That(_invasionGarrisonArmyLords.LeadsGarrisonArmy(null), Is.False);
                Assert.That(_invasionGarrisonArmyLords.FindPartySizeLimit("cs_humar_lord_1"), Is.Null);
            });
        }

        [Test]
        public void GatheringIsRemembered()
        {
            _invasionGarrisonArmyLords.Record(ClanId, HeroId, 1001);

            _invasionGarrisonArmyLords.MarkGathered(ClanId);

            Assert.Multiple(() =>
            {
                Assert.That(_invasionGarrisonArmyLords.Find(ClanId)!.Value.HasGathered, Is.True);
                Assert.That(_invasionGarrisonArmyLords.FindPartySizeLimit(HeroId), Is.EqualTo(1001));
            });
        }

        [Test]
        public void AForgottenLordLeadsNothing()
        {
            _invasionGarrisonArmyLords.Record(ClanId, HeroId, 1001);

            _invasionGarrisonArmyLords.Forget(ClanId);

            Assert.Multiple(() =>
            {
                Assert.That(_invasionGarrisonArmyLords.IsArmyOut(ClanId), Is.False);
                Assert.That(_invasionGarrisonArmyLords.LeadsGarrisonArmy(HeroId), Is.False);
                Assert.That(_invasionGarrisonArmyLords.FindClanIds(), Is.Empty);
            });
        }

        [Test]
        public void ALordInTheFieldIsKnownAgainAfterALoad()
        {
            _invasionGarrisonArmyLords.Record(ClanId, HeroId, 1001);
            _invasionGarrisonArmyLords.MarkGathered(ClanId);
            InvasionGarrisonArmyLords loaded = new();

            loaded.Restore(_invasionGarrisonArmyLords.Export());

            Assert.Multiple(() =>
            {
                Assert.That(loaded.Find(ClanId)!.Value.HeroId, Is.EqualTo(HeroId));
                Assert.That(loaded.FindPartySizeLimit(HeroId), Is.EqualTo(1001));
                Assert.That(loaded.Find(ClanId)!.Value.HasGathered, Is.True);
            });
        }

        [Test]
        public void AnUnreadableEntryIsDropped()
        {
            _invasionGarrisonArmyLords.Restore(new Dictionary<string, string>
            {
                [ClanId] = HeroId + "|1001|False",
                ["cs_other_clan"] = "cs_other_lord|many|False",
                ["cs_third_clan"] = "|1001|False",
                ["cs_fourth_clan"] = "cs_fourth_lord"
            });

            Assert.That(_invasionGarrisonArmyLords.FindClanIds(), Is.EqualTo(new[] { ClanId }));
        }

        [Test]
        public void NothingSavedRestoresNoLords()
        {
            _invasionGarrisonArmyLords.Record(ClanId, HeroId, 1001);

            _invasionGarrisonArmyLords.Restore(null);

            Assert.That(_invasionGarrisonArmyLords.FindClanIds(), Is.Empty);
        }
    }
}
