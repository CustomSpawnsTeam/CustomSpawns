using CustomSpawns.Data;
using NUnit.Framework;

namespace CustomSpawns.Tests.Spawn
{
    public class DynamicSpawnCoeffTests
    {
        [Test]
        public void TheDynamicSpawnCoefficientRepeatsEveryPeriod()
        {
            Assert.AreEqual(DataUtils.DynamicSpawnCoeff(100f, 150f), DataUtils.DynamicSpawnCoeff(550f, 150f), 0.0001f);
        }

        [Test]
        public void TheDynamicSpawnCoefficientStaysBelowItsPeakLateInACampaign()
        {
            // The peak is the curve's value at the end of a period, about 8.6.
            Assert.LessOrEqual(DataUtils.DynamicSpawnCoeff(533f, 150f), 8.6f);
        }
    }
}
