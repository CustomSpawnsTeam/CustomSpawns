using System.Linq;
using CustomSpawns.Invasions;
using NUnit.Framework;

namespace CustomSpawns.Tests.Invasions
{
    public class InvasionRebellionsTests
    {
        private InvasionConquests _invasionConquests = new();
        private InvasionRebellions _invasionRebellions = new(new InvasionConquests());

        [SetUp]
        public void SetUp()
        {
            _invasionConquests = new InvasionConquests();
            _invasionRebellions = new InvasionRebellions(_invasionConquests);
            InvasionConquest leader = _invasionConquests.Begin("humar", "vlandia", 10)!;
            _invasionConquests.BeginFollowing("humar_2", leader, "aserai", 11);
            _invasionConquests.BeginFollowing("humar_3", leader, "sturgia", 12);
            _invasionConquests.Begin("makko", "battania", 13);
        }

        [Test]
        public void ATownRisingAgainstARealmOfALineSetsTheWholeLineAgainstItsRebels()
        {
            Assert.That(_invasionRebellions.FindRealmsAtWarWithRebels("humar_2", _ => false)
                .Select(realm => realm.InvasionClanId), Is.EquivalentTo(new[] { "humar", "humar_3" }));
        }

        [Test]
        public void ATownRisingAgainstARealmOfALineLeavesOtherLinesOut()
        {
            Assert.That(_invasionRebellions.FindRealmsAtWarWithRebels("humar_2", _ => true)
                .Select(realm => realm.InvasionClanId), Has.No.Member("makko"));
        }

        [Test]
        public void ARealmAtWarWithTheKingdomATownRoseAgainstGoesToWarWithItsRebels()
        {
            Assert.That(_invasionRebellions.FindRealmsAtWarWithRebels(null,
                    realm => realm.InvasionClanId == "humar_3" || realm.InvasionClanId == "makko")
                .Select(realm => realm.InvasionClanId), Is.EquivalentTo(new[] { "humar_3", "makko" }));
        }

        [Test]
        public void RealmsAtPeaceWithTheKingdomATownRoseAgainstLeaveItsRebelsBe()
        {
            Assert.That(_invasionRebellions.FindRealmsAtWarWithRebels(null, _ => false), Is.Empty);
        }
    }
}
