using System;
using CustomSpawns.Invasions;
using NUnit.Framework;

namespace CustomSpawns.Tests.Invasions
{
    public class InvasionPartyStatisticsTests
    {
        [Test]
        public void CountsEachPartyOnceAndReportsHealthyMenSeparately()
        {
            var statistics = new InvasionPartyStatistics(new[]
            {
                (Men: 150, Healthy: 120, Limit: 200, Strength: 220f),
                (Men: 50, Healthy: 30, Limit: 100, Strength: 60f),
                (Men: 100, Healthy: 100, Limit: 200, Strength: 140f)
            });

            Assert.Multiple(() =>
            {
                Assert.That(statistics.Count, Is.EqualTo(3));
                Assert.That(statistics.Men, Is.EqualTo(300));
                Assert.That(statistics.Healthy, Is.EqualTo(250));
                Assert.That(statistics.Strength, Is.EqualTo(420));
                Assert.That(statistics.MinimumSize, Is.EqualTo(50));
                Assert.That(statistics.MaximumSize, Is.EqualTo(150));
                Assert.That(statistics.AverageSize, Is.EqualTo(100));
                Assert.That(statistics.AverageLimit, Is.EqualTo(500d / 3));
                Assert.That(statistics.FillPercent, Is.EqualTo(60));
            });
        }

        [Test]
        public void FillUsesTotalCapacityAndCanExposeOverfullParties()
        {
            var uneven = new InvasionPartyStatistics(new[] { (50, 50, 200, 70f), (100, 100, 100, 140f) });
            var overfull = new InvasionPartyStatistics(new[] { (150, 120, 100, 200f) });

            Assert.That(uneven.FillPercent, Is.EqualTo(50));
            Assert.That(overfull.FillPercent, Is.EqualTo(150));
        }

        [Test]
        public void NoFieldPartiesProducesZerosInsteadOfUndefinedAverages()
        {
            var statistics = new InvasionPartyStatistics(Array.Empty<(int, int, int, float)>());

            Assert.Multiple(() =>
            {
                Assert.That(statistics.Count, Is.Zero);
                Assert.That(statistics.Men, Is.Zero);
                Assert.That(statistics.Healthy, Is.Zero);
                Assert.That(statistics.Strength, Is.Zero);
                Assert.That(statistics.MinimumSize, Is.Zero);
                Assert.That(statistics.MaximumSize, Is.Zero);
                Assert.That(statistics.AverageSize, Is.Zero);
                Assert.That(statistics.AverageLimit, Is.Zero);
                Assert.That(statistics.FillPercent, Is.Zero);
            });
        }

        [Test]
        public void APartyWithoutACapacityStillReportsItsMen()
        {
            var statistics = new InvasionPartyStatistics(new[] { (25, 20, 0, 30f) });

            Assert.That(statistics.Count, Is.EqualTo(1));
            Assert.That(statistics.AverageSize, Is.EqualTo(25));
            Assert.That(statistics.FillPercent, Is.Zero);
        }
    }
}
