using System;
using System.Collections.Generic;
using CustomSpawns.Invasions;
using NUnit.Framework;

namespace CustomSpawns.Tests.Invasions
{
    public class InvasionLordAllocationTests
    {
        [Test]
        public void TheFirstClanWithRoomWinsWithoutQueryingLaterClans()
        {
            var invasion = new Invasion("invasion", "clan");
            invasion.AddClan("expanded-clan");
            var queried = new List<string>();

            string? selected = invasion.FindClanWithRoom(clanId =>
            {
                queried.Add(clanId);
                return (false, () => (1, 2));
            });

            Assert.AreEqual("clan", selected);
            Assert.That(queried, Is.EqualTo(new[] { "clan" }));
        }

        [Test]
        public void MissingEliminatedAndFullClansAreSkippedInInvasionOrder()
        {
            var invasion = new Invasion("invasion", "missing");
            invasion.AddClan("eliminated");
            invasion.AddClan("full");
            invasion.AddClan("over-limit");
            invasion.AddClan("available");
            var queried = new List<string>();

            string? selected = invasion.FindClanWithRoom(clanId =>
            {
                queried.Add(clanId);
                return clanId switch
                {
                    "missing" => null,
                    "eliminated" => (true, () => (0, 1)),
                    "full" => (false, () => (2, 2)),
                    "over-limit" => (false, () => (3, 2)),
                    _ => (false, () => (0, 1))
                };
            });

            Assert.AreEqual("available", selected);
            Assert.That(queried, Is.EqualTo(invasion.ClanIds));
        }

        [TestCase(false, 0, 0)]
        [TestCase(false, 1, 1)]
        [TestCase(false, 2, 1)]
        [TestCase(true, 0, 1)]
        public void NoEligibleClanRequestsAnotherClan(bool isEliminated, int warParties, int warPartyLimit)
        {
            var invasion = new Invasion("invasion", "clan");

            Assert.IsNull(invasion.FindClanWithRoom(_ => (isEliminated, () => (warParties, warPartyLimit))));
            Assert.IsNull(invasion.FindClanWithRoom(_ => null));
        }

        [Test]
        public void EliminatedClansNeverReadNativeCapacity()
        {
            var invasion = new Invasion("invasion", "clan");

            Assert.IsNull(invasion.FindClanWithRoom(_ =>
                (true, () => throw new AssertionException("Eliminated clans must not query native capacity."))));
        }

        [Test]
        public void RepeatedAllocationsReadCurrentPartyCountsAndNativeLimits()
        {
            var invasion = new Invasion("invasion", "clan");
            invasion.AddClan("expanded-clan");
            var clans = new Dictionary<string, (bool IsEliminated, Func<(int WarParties, int WarPartyLimit)> PartyCapacity)>
            {
                ["clan"] = (false, () => (1, 2)),
                ["expanded-clan"] = (false, () => (0, 1))
            };

            Assert.AreEqual("clan", invasion.FindClanWithRoom(clanId => clans[clanId]));

            clans["clan"] = (false, () => (2, 2));
            Assert.AreEqual("expanded-clan", invasion.FindClanWithRoom(clanId => clans[clanId]));

            clans["expanded-clan"] = (false, () => (1, 1));
            Assert.IsNull(invasion.FindClanWithRoom(clanId => clans[clanId]));

            clans["clan"] = (false, () => (2, 3));
            Assert.AreEqual("clan", invasion.FindClanWithRoom(clanId => clans[clanId]));
        }

        [Test]
        public void SuspensionAndAnEmptyPartyLedgerDoNotChangeClanCapacity()
        {
            var invasion = new Invasion("invasion", "clan") { IsSuspended = true };

            Assert.That(invasion.MemberPartyIds, Is.Empty);
            Assert.AreEqual("clan", invasion.FindClanWithRoom(_ => (false, () => (0, 1))));
        }

        [TestCase(true, false, true)]
        [TestCase(true, true, false)]
        [TestCase(false, false, false)]
        [TestCase(false, true, false)]
        public void OnlyAnActiveLeaderWithoutAPartyCanBeReused(bool isActive, bool hasParty, bool expected)
        {
            var invasion = new Invasion("invasion", "clan");

            Assert.AreEqual(expected, invasion.CanReuseLeader(isActive, hasParty));
        }
    }
}
