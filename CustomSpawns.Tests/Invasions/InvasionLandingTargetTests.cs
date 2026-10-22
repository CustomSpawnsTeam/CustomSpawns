using System;
using CustomSpawns.Invasions;
using NUnit.Framework;

namespace CustomSpawns.Tests.Invasions
{
    public class InvasionLandingTargetTests
    {
        private InvasionState _invasionState = null!;
        private Invasion _invasion = null!;

        [SetUp]
        public void SetUp()
        {
            _invasionState = new InvasionState();
            _invasion = _invasionState.RegisterMember("template", "clan", "party");
        }

        [TestCase("unknown", "clan", false)]
        [TestCase("party", "stranger", false)]
        [TestCase("party", null, false)]
        [TestCase("party", "clan", true)]
        public void OnlyAMemberOfALiveInvasionReceivesLandingRestrictions(string partyId,
            string? partyClanId, bool suspended)
        {
            _invasion.IsSuspended = suspended;

            var filter = _invasionState.FindInitialTargetFilter(partyId, partyClanId,
                _ => throw new InvalidOperationException("An unrelated party needs no invasion configuration."),
                _ => throw new InvalidOperationException("An unrelated party needs no supply-base scan."));

            Assert.IsNull(filter);
        }

        [Test]
        public void ALostCapitalOverridesSupplyRulesWithoutReadingConfigurationOrScanningTheMap()
        {
            _invasion.CapitalSettlementId = "capital";

            var filter = _invasionState.FindInitialTargetFilter("party", "clan",
                _ => throw new InvalidOperationException("Recovering the capital needs no configuration."),
                _ => throw new InvalidOperationException("Recovering the capital needs no supply-base scan."));
            var facts = new InvasionSettlementFacts(false,
                () => throw new InvalidOperationException("Recovering the capital needs no coastal classification."));

            Assert.IsNotNull(filter);
            Assert.IsTrue(filter!("capital", facts));
            Assert.IsFalse(filter("elsewhere", facts));
        }

        [TestCase(false, "Coastal")]
        [TestCase(false, null)]
        [TestCase(true, "None")]
        [TestCase(true, "unknown")]
        public void AnOptionalOrIndependentSupplyLineDoesNotRestrictTargetsOrScanTheMap(bool required,
            string? supplyLineName)
        {
            var filter = _invasionState.FindInitialTargetFilter("party", "clan",
                _ => (required, InvasionSupplyLines.Find(supplyLineName)),
                _ => throw new InvalidOperationException("An unrestricted invasion needs no supply-base scan."));

            Assert.IsNull(filter);
        }

        [Test]
        public void AHeldCapitalDoesNotForcePartiesBackToIt()
        {
            _invasion.CapitalSettlementId = "capital";
            _invasionState.FootholdIndex["capital"] = new InvasionFoothold("capital", "clan", 1);

            var filter = _invasionState.FindInitialTargetFilter("party", "clan",
                _ => (false, new CoastalSupplyLine()),
                _ => throw new InvalidOperationException("An unrestricted invasion needs no supply-base scan."));

            Assert.IsNull(filter);
        }

        [Test]
        public void AnExistingFactionSupplyBaseReleasesTheInitialRestriction()
        {
            var supplyLine = new CoastalSupplyLine();
            int scans = 0;

            var filter = _invasionState.FindInitialTargetFilter("party", "clan",
                templateId =>
                {
                    Assert.AreEqual("template", templateId);
                    return (true, supplyLine);
                },
                queriedSupplyLine =>
                {
                    Assert.AreSame(supplyLine, queriedSupplyLine);
                    scans++;
                    return true;
                });

            Assert.IsNull(filter);
            Assert.AreEqual(1, scans);
        }

        [TestCase("Coastal", true, true, true)]
        [TestCase("Coastal", true, false, false)]
        [TestCase("Coastal", false, false, false)]
        [TestCase("Any", true, false, true)]
        [TestCase("Any", false, false, false)]
        public void WithoutAFactionSupplyBaseOnlyTheConfiguredSupplyTargetsAreAllowed(string supplyLineName,
            bool isTown, bool isCoastalTown, bool allowed)
        {
            var filter = _invasionState.FindInitialTargetFilter("party", "clan",
                _ => (true, InvasionSupplyLines.Find(supplyLineName)), _ => false);

            Assert.IsNotNull(filter);
            Assert.AreEqual(allowed, filter!("candidate", new InvasionSettlementFacts(isTown, () => isCoastalTown)));
        }

        [Test]
        public void APartyOfAnExpandedInvasionClanUsesTheSameLandingRule()
        {
            _invasion.AddClan("expanded");

            var filter = _invasionState.FindInitialTargetFilter("party", "expanded",
                _ => (true, new AnySupplyLine()), _ => false);

            Assert.IsNotNull(filter);
            Assert.IsTrue(filter!("town", new InvasionSettlementFacts(true,
                () => throw new InvalidOperationException("Overland supplies need no coastal classification."))));
        }
    }
}
