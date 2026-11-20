using System;
using System.Collections.Generic;
using CustomSpawns.Invasions;
using NUnit.Framework;

namespace CustomSpawns.Tests.Invasions
{
    public class InvasionPopulationTests
    {
        [TestCase(0)]
        [TestCase(7)]
        public void TheFirstKingdomInNativeOrderWinsEvenWhenItHasNoTowns(int kingdomTowns)
        {
            var invasion = new Invasion("invasion", "clan");
            invasion.AddClan("expanded-clan");
            var clans = new (string ClanId, Func<int?> KingdomTowns, Func<int> ClanTowns)[]
            {
                ("foreign", () => throw new InvalidOperationException("Foreign kingdom read"),
                    () => throw new InvalidOperationException("Foreign towns read")),
                ("expanded-clan", () => kingdomTowns,
                    () => throw new InvalidOperationException("Clan fallback read")),
                ("clan", () => throw new InvalidOperationException("Later kingdom read"),
                    () => throw new InvalidOperationException("Clan fallback read"))
            };

            Assert.AreEqual(kingdomTowns, invasion.CountTownsHeld(clans));
        }

        [Test]
        public void AnIndependentClanDoesNotHideALaterKingdomOrTriggerClanFallback()
        {
            var invasion = new Invasion("invasion", "clan");
            invasion.AddClan("expanded-clan");
            var clans = new (string ClanId, Func<int?> KingdomTowns, Func<int> ClanTowns)[]
            {
                ("clan", () => null, () => throw new InvalidOperationException("Early clan fallback read")),
                ("expanded-clan", () => 4, () => throw new InvalidOperationException("Clan fallback read"))
            };

            Assert.AreEqual(4, invasion.CountTownsHeld(clans));
        }

        [Test]
        public void WithoutAKingdomOnlyTheInvasionClansTownsAreSummed()
        {
            var invasion = new Invasion("invasion", "clan") { IsSuspended = true };
            invasion.AddClan("expanded-clan");
            var clans = new (string ClanId, Func<int?> KingdomTowns, Func<int> ClanTowns)[]
            {
                ("clan", () => null, () => 2),
                ("foreign", () => throw new InvalidOperationException("Foreign kingdom read"),
                    () => throw new InvalidOperationException("Foreign towns read")),
                ("expanded-clan", () => null, () => 3)
            };

            Assert.AreEqual(5, invasion.CountTownsHeld(clans));
        }

        [Test]
        public void LivingAdultLordsOfFoundingAndExpandedClansCountEvenWhileSuspended()
        {
            var invasion = new Invasion("invasion", "clan") { IsSuspended = true };
            invasion.AddClan("expanded-clan");
            var clans = new (string ClanId, IEnumerable<(bool IsAlive, bool IsChild, bool IsLord)> Heroes)[]
            {
                ("clan", new[] { (true, false, true), (false, false, true), (true, true, true), (true, false, false) }),
                ("expanded-clan", new[] { (true, false, true), (true, false, true) }),
                ("foreign", new[] { (true, false, true) })
            };

            Assert.AreEqual(3, invasion.CountLivingLords(clans));
        }

        [Test]
        public void NoInvasionClansMeansNoTownsOrLivingLords()
        {
            var invasion = new Invasion("invasion", "clan");

            Assert.AreEqual(0, invasion.CountTownsHeld(
                Array.Empty<(string ClanId, Func<int?> KingdomTowns, Func<int> ClanTowns)>()));
            Assert.AreEqual(0, invasion.CountLivingLords(
                Array.Empty<(string ClanId, IEnumerable<(bool IsAlive, bool IsChild, bool IsLord)> Heroes)>()));
        }
    }
}
