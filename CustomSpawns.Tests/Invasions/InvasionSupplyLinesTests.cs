using CustomSpawns.Invasions;
using NUnit.Framework;

namespace CustomSpawns.Tests.Invasions
{
    public class InvasionSupplyLinesTests
    {
        private int _coastalQuestions;

        [SetUp]
        public void SetUp()
        {
            _coastalQuestions = 0;
        }

        [Test]
        public void CoastalSupplyFeedsOnlyFromATownTheSeaReaches()
        {
            IInvasionSupplyLine supplyLine = InvasionSupplyLines.Find("Coastal")!;

            Assert.Multiple(() =>
            {
                Assert.IsTrue(supplyLine.IsSupplyBase(Facts(isTown: true, isCoastalTown: true)));
                Assert.IsFalse(supplyLine.IsSupplyBase(Facts(isTown: true, isCoastalTown: false)));
                Assert.IsFalse(supplyLine.IsSupplyBase(Facts(isTown: false, isCoastalTown: false)));
                Assert.IsTrue(supplyLine.DependsOnASupplyBase);
            });
        }

        [Test]
        public void AnySupplyFeedsFromAnyTownWithoutAskingAboutTheSea()
        {
            IInvasionSupplyLine supplyLine = InvasionSupplyLines.Find("Any")!;

            Assert.Multiple(() =>
            {
                Assert.IsTrue(supplyLine.IsSupplyBase(Facts(isTown: true, isCoastalTown: false)));
                Assert.IsFalse(supplyLine.IsSupplyBase(Facts(isTown: false, isCoastalTown: false)));
                Assert.IsTrue(supplyLine.DependsOnASupplyBase);
                Assert.AreEqual(0, _coastalQuestions);
            });
        }

        [Test]
        public void NoSupplyLineIsFedByEverythingItHoldsAndDependsOnNoBase()
        {
            IInvasionSupplyLine supplyLine = InvasionSupplyLines.Find("None")!;

            Assert.Multiple(() =>
            {
                Assert.IsTrue(supplyLine.IsSupplyBase(Facts(isTown: true, isCoastalTown: true)));
                Assert.IsTrue(supplyLine.IsSupplyBase(Facts(isTown: false, isCoastalTown: false)));
                Assert.IsFalse(supplyLine.DependsOnASupplyBase);
                Assert.AreEqual(0, _coastalQuestions);
            });
        }

        [Test]
        public void AnUnnamedSupplyLineIsCoastalAndAnUnknownOneIsNoDoctrineAtAll()
        {
            Assert.Multiple(() =>
            {
                Assert.IsInstanceOf<CoastalSupplyLine>(InvasionSupplyLines.Find(null));
                Assert.IsInstanceOf<CoastalSupplyLine>(InvasionSupplyLines.Find(" "));
                Assert.IsNull(InvasionSupplyLines.Find("coastal"));
                Assert.IsNull(InvasionSupplyLines.Find("Inland"));
            });
        }

        private InvasionSettlementFacts Facts(bool isTown, bool isCoastalTown) =>
            new(isTown, () =>
            {
                _coastalQuestions++;
                return isCoastalTown;
            });
    }
}
