using CustomSpawns.Invasions;
using NUnit.Framework;

namespace CustomSpawns.Tests.Invasions
{
    public class InvasionCoastalTargetingPolicyTests
    {
        [Test]
        public void NativePortMetadataAllowsTownsButNeverCastles()
        {
            var targeting = new InvasionCoastalTargeting(false);

            Assert.True(targeting.AllowsFirstConquestTarget("native-port", true, true));
            Assert.False(targeting.AllowsFirstConquestTarget("coastal-castle", false, true));
            Assert.False(targeting.AllowsFirstConquestTarget("inland-town", true, false));
        }

        [Test]
        public void NearbyCoastalOrOpenSeaFacesAllowTowns()
        {
            var settlement = new InvasionCoastalPoint(0f, 0f);
            Assert.True(InvasionCoastalTownClassification.IsCoastalTown(true, false,
                InvasionCoastalTownClassification.HasMaritimeFaceWithinRadius(new[]
                {
                    new InvasionCoastalPoint(30f, 0f)
                }, settlement, settlement, 30f, out _)));
            Assert.True(InvasionCoastalTownClassification.IsCoastalTown(true, false,
                InvasionCoastalTownClassification.HasMaritimeFaceWithinRadius(new[]
                {
                    new InvasionCoastalPoint(30f, 0f)
                }, settlement, settlement, 30f, out _)));
        }

        [Test]
        public void LakesRiversAndLandDoNotAllowTowns()
        {
            var indexedFaces = InvasionCoastalTownClassification.IndexMaritimeFaceCenters(new[]
            {
                new InvasionCoastalMapFace(InvasionCoastalFaceTerrain.Lake, new InvasionCoastalPoint(1f, 0f)),
                new InvasionCoastalMapFace(InvasionCoastalFaceTerrain.River, new InvasionCoastalPoint(1f, 0f)),
                new InvasionCoastalMapFace(InvasionCoastalFaceTerrain.Land, new InvasionCoastalPoint(1f, 0f))
            });
            var settlement = new InvasionCoastalPoint(0f, 0f);
            bool hasMaritimeFace = InvasionCoastalTownClassification.HasMaritimeFaceWithinRadius(indexedFaces,
                settlement, settlement, 30f, out _);

            Assert.False(InvasionCoastalTownClassification.IsCoastalTown(true, false, hasMaritimeFace));
        }

        [Test]
        public void MaritimeFacesOutsideTheScanRadiusDoNotAllowTowns()
        {
            bool hasMaritimeFace = InvasionCoastalTownClassification.HasMaritimeFaceWithinRadius(new[]
            {
                new InvasionCoastalPoint(30.0002f, 0f)
            }, new InvasionCoastalPoint(0f, 0f), new InvasionCoastalPoint(0f, 0f), 30f, out _);

            Assert.False(InvasionCoastalTownClassification.IsCoastalTown(true, false, hasMaritimeFace));
        }

        [Test]
        public void CoastalEvidenceNeverAllowsANonTown()
        {
            Assert.False(InvasionCoastalTownClassification.IsCoastalTown(false, true, true));
        }

        [Test]
        public void MaritimeFaceIndexKeepsOnlyCoastalAndOpenSeaCentersForSettlementGateQuery()
        {
            var indexedFaces = InvasionCoastalTownClassification.IndexMaritimeFaceCenters(new[]
            {
                new InvasionCoastalMapFace(InvasionCoastalFaceTerrain.Lake, new InvasionCoastalPoint(2f, 0f)),
                new InvasionCoastalMapFace(InvasionCoastalFaceTerrain.CoastalSea, new InvasionCoastalPoint(40f, 0f)),
                new InvasionCoastalMapFace(InvasionCoastalFaceTerrain.OpenSea, new InvasionCoastalPoint(10f, 0f))
            });

            Assert.That(indexedFaces, Has.Count.EqualTo(2));
            Assert.True(InvasionCoastalTownClassification.HasMaritimeFaceWithinRadius(indexedFaces,
                new InvasionCoastalPoint(100f, 0f), new InvasionCoastalPoint(0f, 0f), 15f,
                out float nearestDistanceSquared));
            Assert.That(nearestDistanceSquared, Is.EqualTo(100f));
        }

        [Test]
        public void CreditedCoastalConquestUnlocksTheExistingTargetTypes()
        {
            var targeting = new InvasionCoastalTargeting(true);

            Assert.False(targeting.RequiresFirstCoastalConquest);
            Assert.True(targeting.AllowsFirstConquestTarget("inland-castle", false, false));
        }

        [Test]
        public void CaptureClassificationUsesTheSameCoastalTownRule()
        {
            Assert.True(InvasionCoastalTargeting.IsCoastalTown(true, true));
            Assert.False(InvasionCoastalTargeting.IsCoastalTown(true, false));
        }

        [Test]
        public void HeldInlandTargetIsRejectedBeforeTheFirstCoastalConquest()
        {
            var targeting = new InvasionCoastalTargeting(false, settlementId => settlementId == "coastal-town");

            Assert.False(targeting.AllowsHeldTarget("inland-town", true, false, false, true));
        }

        [Test]
        public void HeldInlandTargetIsRetainedOnlyForAMatchingNativeAssault()
        {
            var targeting = new InvasionCoastalTargeting(false, settlementId => settlementId == "coastal-town");

            Assert.True(targeting.AllowsHeldTarget("inland-town", true, false, true, false));
            Assert.False(targeting.AllowsHeldTarget("inland-town", true, false, false, false));
        }

        [Test]
        public void HeldCoastalTargetRequiresAReachableLandGate()
        {
            var targeting = new InvasionCoastalTargeting(false, settlementId => settlementId == "coastal-town");

            Assert.True(targeting.AllowsHeldTarget("coastal-town", true, false, false, true));
            Assert.False(targeting.AllowsHeldTarget("coastal-town", true, false, false, false));
        }
    }
}
