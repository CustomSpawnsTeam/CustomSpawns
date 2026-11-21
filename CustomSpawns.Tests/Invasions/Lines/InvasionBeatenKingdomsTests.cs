using System.Collections.Generic;
using CustomSpawns.Invasions.Lines;
using NUnit.Framework;

namespace CustomSpawns.Tests.Invasions.Lines
{
    public class InvasionBeatenKingdomsTests
    {
        private const string Humar = "cs_humar_invasion_force";
        private const string Makko = "cs_makko_invasion_force";
        private const int MaxKingdoms = 3;

        private readonly InvasionLineRefill _invasionLineRefill = new();

        [Test]
        public void OnlyTheLineThatBeatItsTargetLandsASecondRealm()
        {
            var beatenKingdoms = new InvasionBeatenKingdoms();
            beatenKingdoms.ObserveFall(Humar, "vlandia");

            var fallen = new List<string> { "vlandia" };
            bool humarLaunches = _invasionLineRefill.ShouldLaunch(beatenKingdoms.CountBeatenBy(Humar, fallen), 1,
                true, MaxKingdoms);
            bool makkoLaunches = _invasionLineRefill.ShouldLaunch(beatenKingdoms.CountBeatenBy(Makko, fallen), 1,
                true, MaxKingdoms);

            Assert.That(humarLaunches, Is.True);
            Assert.That(makkoLaunches, Is.False);
        }

        [Test]
        public void ALineHasBeatenAKingdomOnlyOnceOneFellToIt()
        {
            var beatenKingdoms = new InvasionBeatenKingdoms();
            bool before = beatenKingdoms.HasBeatenAny(Humar);
            beatenKingdoms.ObserveFall(Humar, "vlandia");

            Assert.That(before, Is.False);
            Assert.That(beatenKingdoms.HasBeatenAny(Humar), Is.True);
            Assert.That(beatenKingdoms.HasBeatenAny(Makko), Is.False);
        }

        [Test]
        public void AKingdomNoInvasionTargetedCountsForNoLine()
        {
            var beatenKingdoms = new InvasionBeatenKingdoms();
            beatenKingdoms.ObserveFall(Humar, "vlandia");

            var fallen = new List<string> { "battania" };

            Assert.That(beatenKingdoms.CountBeatenBy(Humar, fallen), Is.EqualTo(0));
            Assert.That(beatenKingdoms.CountBeatenBy(Makko, fallen), Is.EqualTo(0));
        }

        [Test]
        public void ASingleLineLandsNothingForAKingdomItDidNotTarget()
        {
            var beatenKingdoms = new InvasionBeatenKingdoms();

            int beaten = beatenKingdoms.CountBeatenBy(Humar, new List<string> { "battania" });

            Assert.That(_invasionLineRefill.ShouldLaunch(beaten, 1, true, MaxKingdoms), Is.False);
        }

        [Test]
        public void EveryLineConqueringTheKingdomWhenItFellBeatIt()
        {
            var beatenKingdoms = new InvasionBeatenKingdoms();
            beatenKingdoms.ObserveFall(Humar, "vlandia");
            beatenKingdoms.ObserveFall(Makko, "vlandia");

            var fallen = new List<string> { "vlandia" };

            Assert.That(beatenKingdoms.CountBeatenBy(Humar, fallen), Is.EqualTo(1));
            Assert.That(beatenKingdoms.CountBeatenBy(Makko, fallen), Is.EqualTo(1));
        }

        [Test]
        public void AFallSeenOnSeveralDaysCountsOnce()
        {
            var beatenKingdoms = new InvasionBeatenKingdoms();
            beatenKingdoms.ObserveFall(Humar, "vlandia");
            beatenKingdoms.ObserveFall(Humar, "vlandia");

            Assert.That(beatenKingdoms.CountBeatenBy(Humar, new List<string> { "vlandia" }), Is.EqualTo(1));
        }

        [Test]
        public void ABeatenKingdomNoLongerFallenCountsForNoLine()
        {
            var beatenKingdoms = new InvasionBeatenKingdoms();
            beatenKingdoms.ObserveFall(Humar, "vlandia");

            Assert.That(beatenKingdoms.CountBeatenBy(Humar, new List<string>()), Is.EqualTo(0));
        }

        [Test]
        public void TheBeatenKingdomsSurviveASave()
        {
            var beatenKingdoms = new InvasionBeatenKingdoms();
            beatenKingdoms.ObserveFall(Humar, "vlandia");
            beatenKingdoms.ObserveFall(Makko, "vlandia");
            beatenKingdoms.ObserveFall(Makko, "sturgia");

            var restored = new InvasionBeatenKingdoms();
            restored.ObserveFall(Humar, "battania");
            restored.Restore(beatenKingdoms.Export());

            Assert.That(restored.WasBeatenBy(Humar, "vlandia"), Is.True);
            Assert.That(restored.WasBeatenBy(Makko, "vlandia"), Is.True);
            Assert.That(restored.WasBeatenBy(Makko, "sturgia"), Is.True);
            Assert.That(restored.WasBeatenBy(Humar, "sturgia"), Is.False);
            Assert.That(restored.WasBeatenBy(Humar, "battania"), Is.False);
        }

        [Test]
        public void ASaveWithoutBeatenKingdomsRestoresNone()
        {
            var beatenKingdoms = new InvasionBeatenKingdoms();
            beatenKingdoms.ObserveFall(Humar, "vlandia");

            beatenKingdoms.Restore(null);

            Assert.That(beatenKingdoms.Export(), Is.Empty);
        }
    }
}
