using System.Linq;
using CustomSpawns.Invasions.Conquest;
using CustomSpawns.Invasions.Lines;
using CustomSpawns.Invasions.PlayerSparing;
using Moq;
using NUnit.Framework;

namespace CustomSpawns.Tests.Invasions.PlayerSparing
{
    public class InvasionPlayerSparingTests
    {
        private const string Line = "cs_makko_invasion_force";
        private const string OtherLine = "cs_humar_invasion_force";
        private const string PlayerClan = "player_faction";
        private const string PlayerKingdom = "vlandia";
        private const string Town = "town_V1";

        [Test]
        public void MayBesiege_RulesOutASettlementOfThePlayersOwnClan()
        {
            InvasionPlayerSparing sparing = Sparing(Realm(PlayerClan, null, PlayerClan, null));

            Assert.That(sparing.MayBesiege(Line, false, Town), Is.False);
        }

        [Test]
        public void MayBesiege_RulesOutASettlementOfTheKingdomThePlayerServes()
        {
            InvasionPlayerSparing sparing = Sparing(Realm(PlayerClan, PlayerKingdom, "clan_vassal", PlayerKingdom));

            Assert.That(sparing.MayBesiege(Line, false, Town), Is.False);
        }

        [Test]
        public void MayBesiege_KeepsASettlementThePlayerDoesNotHold()
        {
            InvasionPlayerSparing sparing = Sparing(Realm(PlayerClan, PlayerKingdom, "clan_empire", "empire"));

            Assert.That(sparing.MayBesiege(Line, false, Town), Is.True);
        }

        [Test]
        public void MayBesiege_KeepsAnOtherClansSettlementWhileThePlayerServesNoKingdom()
        {
            InvasionPlayerSparing sparing = Sparing(Realm(PlayerClan, null, "clan_empire", null));

            Assert.That(sparing.MayBesiege(Line, false, Town), Is.True);
        }

        [Test]
        public void MayBesiege_SparesThePlayerNoLongerOnceTheCapitalIsFounded()
        {
            InvasionPlayerSparing sparing = Sparing(Realm(PlayerClan, PlayerKingdom, PlayerClan, PlayerKingdom));

            Assert.That(sparing.MayBesiege(Line, true, Town), Is.True);
        }

        [Test]
        public void MayBesiege_SparesThePlayerNoLongerOnceTheLineHasBeatenAKingdom()
        {
            var beatenKingdoms = new InvasionBeatenKingdoms();
            beatenKingdoms.ObserveFall(Line, "empire");
            InvasionPlayerSparing sparing =
                Sparing(Realm(PlayerClan, PlayerKingdom, PlayerClan, PlayerKingdom), beatenKingdoms);

            Assert.That(sparing.MayBesiege(Line, false, Town), Is.True);
        }

        [Test]
        public void MayBesiege_AnotherLinesBeatenKingdomStillSparesThePlayer()
        {
            var beatenKingdoms = new InvasionBeatenKingdoms();
            beatenKingdoms.ObserveFall(OtherLine, "empire");
            InvasionPlayerSparing sparing =
                Sparing(Realm(PlayerClan, PlayerKingdom, PlayerClan, PlayerKingdom), beatenKingdoms);

            Assert.That(sparing.MayBesiege(Line, false, Town), Is.False);
        }

        [Test]
        public void FindFirstTarget_IsNeverThePlayersKingdom()
        {
            InvasionPlayerSparing sparing = Sparing(Realm(PlayerClan, PlayerKingdom, null, null));

            Assert.That(sparing.FindFirstTarget(Line, PlayerKingdom), Is.Null);
        }

        [Test]
        public void FindFirstTarget_IsAnyOtherKingdom()
        {
            InvasionPlayerSparing sparing = Sparing(Realm(PlayerClan, PlayerKingdom, null, null));

            Assert.That(sparing.FindFirstTarget(Line, "empire"), Is.EqualTo("empire"));
        }

        [Test]
        public void FindFirstTarget_IsAnyKingdomWhileThePlayerServesNone()
        {
            InvasionPlayerSparing sparing = Sparing(Realm(PlayerClan, null, null, null));

            Assert.That(sparing.FindFirstTarget(Line, PlayerKingdom), Is.EqualTo(PlayerKingdom));
        }

        [Test]
        public void FindTargetCandidates_LeavesOutThePlayersKingdomUntilTheLineHasBeatenOne()
        {
            var beatenKingdoms = new InvasionBeatenKingdoms();
            InvasionPlayerSparing sparing = Sparing(Realm(PlayerClan, PlayerKingdom, null, null), beatenKingdoms);
            var neighbours = new[] { new InvasionNeighbour(PlayerKingdom, 1f), new InvasionNeighbour("empire", 1f) };

            string[] spared = sparing.FindTargetCandidates(Line, neighbours).Select(n => n.KingdomId).ToArray();
            beatenKingdoms.ObserveFall(Line, "sturgia");
            string[] afterFall = sparing.FindTargetCandidates(Line, neighbours).Select(n => n.KingdomId).ToArray();

            Assert.That(spared, Is.EqualTo(new[] { "empire" }));
            Assert.That(afterFall, Is.EqualTo(new[] { PlayerKingdom, "empire" }));
        }

        private static InvasionPlayerSparing Sparing(IInvasionPlayerRealm realm,
            InvasionBeatenKingdoms? beatenKingdoms = null) =>
            new(realm, beatenKingdoms ?? new InvasionBeatenKingdoms());

        private static IInvasionPlayerRealm Realm(string? playerClanId, string? playerKingdomId,
            string? ownerClanId, string? ownerKingdomId)
        {
            var realm = new Mock<IInvasionPlayerRealm>();
            realm.Setup(r => r.FindPlayerClanId()).Returns(playerClanId);
            realm.Setup(r => r.FindPlayerKingdomId()).Returns(playerKingdomId);
            realm.Setup(r => r.FindOwnerClanId(Town)).Returns(ownerClanId);
            realm.Setup(r => r.FindOwnerKingdomId(Town)).Returns(ownerKingdomId);
            return realm.Object;
        }
    }
}
