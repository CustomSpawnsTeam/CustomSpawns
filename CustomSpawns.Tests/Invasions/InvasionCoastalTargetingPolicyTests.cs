using CustomSpawns.Invasions;
using NUnit.Framework;

namespace CustomSpawns.Tests.Invasions
{
    public class InvasionCoastalTargetingPolicyTests
    {
        [Test]
        public void ExplicitCoastalTownIdsTakePrecedenceOverNativePortMetadata()
        {
            var targeting = new InvasionCoastalTargeting(false, new[] { "configured-port" });

            Assert.True(targeting.AllowsFirstConquestTarget("configured-port", true, false));
            Assert.False(targeting.AllowsFirstConquestTarget("native-port", true, true));
        }

        [Test]
        public void EmptyCoastalTownIdsUseNativePortMetadataButNeverAllowACastle()
        {
            var targeting = new InvasionCoastalTargeting(false, new string[0]);

            Assert.True(targeting.AllowsFirstConquestTarget("native-port", true, true));
            Assert.False(targeting.AllowsFirstConquestTarget("coastal-castle", false, true));
            Assert.False(targeting.AllowsFirstConquestTarget("inland-town", true, false));
        }

        [Test]
        public void CreditedCoastalConquestUnlocksTheExistingTargetTypes()
        {
            var targeting = new InvasionCoastalTargeting(true, new[] { "configured-port" });

            Assert.False(targeting.RequiresFirstCoastalConquest);
            Assert.True(targeting.AllowsFirstConquestTarget("inland-castle", false, false));
        }

        [Test]
        public void CaptureClassificationUsesTheSameCoastalTownRule()
        {
            Assert.True(InvasionCoastalTargeting.IsCoastalTown(new[] { "configured-port" },
                "configured-port", true, false));
            Assert.False(InvasionCoastalTargeting.IsCoastalTown(new[] { "configured-port" },
                "native-port", true, true));
        }

        [Test]
        public void HeldInlandTargetIsRejectedBeforeTheFirstCoastalConquest()
        {
            var targeting = new InvasionCoastalTargeting(false, new[] { "coastal-town" });

            Assert.False(targeting.AllowsHeldTarget("inland-town", true, false, false, true));
        }

        [Test]
        public void HeldInlandTargetIsRetainedOnlyForAMatchingNativeAssault()
        {
            var targeting = new InvasionCoastalTargeting(false, new[] { "coastal-town" });

            Assert.True(targeting.AllowsHeldTarget("inland-town", true, false, true, false));
            Assert.False(targeting.AllowsHeldTarget("inland-town", true, false, false, false));
        }

        [Test]
        public void HeldCoastalTargetRequiresAReachableLandGate()
        {
            var targeting = new InvasionCoastalTargeting(false, new[] { "coastal-town" });

            Assert.True(targeting.AllowsHeldTarget("coastal-town", true, false, false, true));
            Assert.False(targeting.AllowsHeldTarget("coastal-town", true, false, false, false));
        }
    }
}
