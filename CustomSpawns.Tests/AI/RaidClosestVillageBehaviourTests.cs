using CustomSpawns.AI;
using CustomSpawns.Data.Dto;
using NUnit.Framework;

namespace CustomSpawns.Tests.AI
{
    public class RaidClosestVillageBehaviourTests
    {
        [Test]
        public void Provider_HandlesTypedRaidConfiguration()
        {
            Assert.That(new RaidClosestVillageBehaviour().ConfigurationType,
                Is.EqualTo(typeof(RaidClosestVillageData)));
        }

        [Test]
        public void TargetRatio_ZeroMilitiaIsAlwaysAQualifiedTarget()
        {
            Assert.That(SettlementOffensiveBehaviour.MeetsStrengthRatio(0f, 0f, float.MaxValue),
                Is.True);
        }
    }
}
