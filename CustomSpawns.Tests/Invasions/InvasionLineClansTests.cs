using System.Collections.Generic;
using CustomSpawns.Invasions;
using NUnit.Framework;

namespace CustomSpawns.Tests.Invasions
{
    public class InvasionLineClansTests
    {
        private InvasionLineClans _invasionLineClans = new();

        [SetUp]
        public void SetUp()
        {
            _invasionLineClans = new InvasionLineClans();
        }

        [Test]
        public void TheFirstCopyOfAClanIsNumberedOne()
        {
            Assert.That(_invasionLineClans.NextCloneClanId("cs_humar"), Is.EqualTo("cs_humar_line_1"));
        }

        [Test]
        public void EachCopyOfAClanTakesTheNextNumber()
        {
            _invasionLineClans.Register("cs_humar_line_1", "cs_humar", "cs_humar");
            _invasionLineClans.Register("cs_other_line_1", "cs_other", "cs_other");

            Assert.That(_invasionLineClans.NextCloneClanId("cs_humar"), Is.EqualTo("cs_humar_line_2"));
        }

        [Test]
        public void TheNextCopyNumberCountsOnlyCopiesOfTheSameClan()
        {
            _invasionLineClans.Register("cs_humar_line_1", "cs_humar", "cs_humar");
            _invasionLineClans.Register("cs_other_line_1", "cs_other", "cs_other");

            Assert.That(_invasionLineClans.NextCloneNumber("cs_humar"), Is.EqualTo(2));
        }

        [Test]
        public void ACopyKnowsTheInvasionItWasLaunchedFor()
        {
            _invasionLineClans.Register("cs_humar_line_1", "cs_humar_2", "cs_humar");

            Assert.That(_invasionLineClans.IsClone("cs_humar_line_1"), Is.True);
            Assert.That(_invasionLineClans.FindLauncherClanId("cs_humar_line_1"), Is.EqualTo("cs_humar_2"));
        }

        [Test]
        public void AConfiguredClanIsNoCopy()
        {
            Assert.That(_invasionLineClans.IsClone("cs_humar"), Is.False);
            Assert.That(_invasionLineClans.FindLauncherClanId("cs_humar"), Is.Null);
            Assert.That(_invasionLineClans.IsClone(null), Is.False);
        }

        [Test]
        public void ACopyFollowsOnlyTheInvasionItWasLaunchedFor()
        {
            _invasionLineClans.Register("cs_humar_line_1", "cs_humar", "cs_humar");

            Assert.That(_invasionLineClans.CanLead("cs_humar_line_1", "cs_humar", false), Is.True);
            Assert.That(_invasionLineClans.CanLead("cs_humar_line_1", "cs_humar_2", true), Is.False);
        }

        [Test]
        public void AConfiguredFollowerFollowsTheInvasionRunningItsLeaderTemplate()
        {
            Assert.That(_invasionLineClans.CanLead("cs_humar_2", "cs_humar", true), Is.True);
            Assert.That(_invasionLineClans.CanLead("cs_humar_2", "cs_other", false), Is.False);
        }

        [Test]
        public void AConfiguredFollowerNeverTakesACopyOfItsLeaderForTheLeader()
        {
            _invasionLineClans.Register("cs_humar_line_1", "cs_humar", "cs_humar");

            Assert.That(_invasionLineClans.CanLead("cs_humar_2", "cs_humar_line_1", true), Is.False);
        }

        [Test]
        public void CopiesRoundTripThroughTheSave()
        {
            _invasionLineClans.Register("cs_humar_line_1", "cs_humar", "cs_humar");
            _invasionLineClans.Register("cs_humar_line_2", "cs_humar_line_1", "cs_humar");
            InvasionLineClans restored = new();

            restored.Restore(_invasionLineClans.Export());

            Assert.That(restored.FindLauncherClanId("cs_humar_line_2"), Is.EqualTo("cs_humar_line_1"));
            Assert.That(restored.All, Is.EquivalentTo(new[]
            {
                ("cs_humar_line_1", "cs_humar"), ("cs_humar_line_2", "cs_humar")
            }));
            Assert.That(restored.NextCloneClanId("cs_humar"), Is.EqualTo("cs_humar_line_3"));
        }

        [Test]
        public void ASaveWithoutCopiesRestoresNone()
        {
            _invasionLineClans.Register("cs_humar_line_1", "cs_humar", "cs_humar");

            _invasionLineClans.Restore(null);

            Assert.That(_invasionLineClans.All, Is.Empty);
        }

        [Test]
        public void AMalformedSavedCopyIsSkipped()
        {
            _invasionLineClans.Restore(new Dictionary<string, string>
            {
                { "cs_humar_line_1", "cs_humar" }, { "cs_humar_line_2", "cs_humar|cs_humar" }
            });

            Assert.That(_invasionLineClans.IsClone("cs_humar_line_1"), Is.False);
            Assert.That(_invasionLineClans.IsClone("cs_humar_line_2"), Is.True);
        }
    }
}
