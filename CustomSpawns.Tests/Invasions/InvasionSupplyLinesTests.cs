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
                Assert.IsTrue(supplyLine.IsSupplyBase(Facts(isTown: true, isCastle: false, isCoastalTown: true)));
                Assert.IsFalse(supplyLine.IsSupplyBase(Facts(isTown: true, isCastle: false, isCoastalTown: false)));
                Assert.IsFalse(supplyLine.IsSupplyBase(Facts(isTown: false, isCastle: true, isCoastalTown: false)));
            });
        }

        [Test]
        public void FreeSupplyFeedsFromAnyFortificationWithoutAskingAboutTheSea()
        {
            IInvasionSupplyLine supplyLine = InvasionSupplyLines.Find("Free")!;

            Assert.Multiple(() =>
            {
                Assert.IsTrue(supplyLine.IsSupplyBase(Facts(isTown: true, isCastle: false, isCoastalTown: false)));
                Assert.IsTrue(supplyLine.IsSupplyBase(Facts(isTown: false, isCastle: true, isCoastalTown: false)));
                Assert.IsFalse(supplyLine.IsSupplyBase(Facts(isTown: false, isCastle: false, isCoastalTown: false)));
                Assert.AreEqual(0, _coastalQuestions);
            });
        }

        [Test]
        public void NoSupplyLineFeedsFromNothingAtAll()
        {
            IInvasionSupplyLine supplyLine = InvasionSupplyLines.Find("None")!;

            Assert.Multiple(() =>
            {
                Assert.IsFalse(supplyLine.IsSupplyBase(Facts(isTown: true, isCastle: false, isCoastalTown: true)));
                Assert.IsFalse(supplyLine.IsSupplyBase(Facts(isTown: false, isCastle: true, isCoastalTown: false)));
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

        private InvasionSettlementFacts Facts(bool isTown, bool isCastle, bool isCoastalTown) =>
            new(isTown, isCastle, () =>
            {
                _coastalQuestions++;
                return isCoastalTown;
            });
    }
}
