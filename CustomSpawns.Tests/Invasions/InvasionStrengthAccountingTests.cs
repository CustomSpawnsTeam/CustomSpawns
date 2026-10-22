using System;
using System.Collections.Generic;
using System.Linq;
using CustomSpawns.Invasions;
using NUnit.Framework;

namespace CustomSpawns.Tests.Invasions
{
    public class InvasionStrengthAccountingTests
    {
        private Invasion _invasion = null!;

        [SetUp]
        public void SetUp()
        {
            _invasion = new Invasion("template", "founder");
            _invasion.AddClan("expanded-clan");
        }

        [TestCase(null, 200)]
        [TestCase(true, 200)]
        [TestCase(false, 0)]
        public void OnlyRegisteredNonAdoptedWarPartiesAreExcluded(bool? adopted, double expected)
        {
            Assert.AreEqual(expected, _invasion.MeasureReinforcementStrength(new[]
            {
                Forces("founder", Array.Empty<float>(), (adopted, 200f))
            }));
        }

        [TestCase("founder", 600)]
        [TestCase("expanded-clan", 600)]
        [TestCase("foreign-clan", 0)]
        public void WarPartiesAndEveryGarrisonCountOnlyForInvasionClans(string clanId, double expected)
        {
            Assert.AreEqual(expected, _invasion.MeasureReinforcementStrength(new[]
            {
                Forces(clanId, new[] { 100f, 0f, 200f }, (null, 300f))
            }));
        }

        [Test]
        public void FoundingAndExpandedClansShareOneTotal()
        {
            Assert.AreEqual(1000, _invasion.MeasureReinforcementStrength(new[]
            {
                Forces("founder", new[] { 100f }, (null, 200f)),
                Forces("expanded-clan", new[] { 300f }, (true, 400f)),
                Forces("foreign-clan", new[] { 500f }, (null, 600f))
            }));
        }

        [Test]
        public void ExcludedWarPartyStrengthIsNeverRead()
        {
            IEnumerable<(bool? IsAdopted, Func<float> Strength)> parties = new[]
            {
                ((bool?)false, (Func<float>)(() => throw new InvalidOperationException("Excluded party")))
            };

            Assert.AreEqual(50, _invasion.MeasureReinforcementStrength(new[]
            {
                ("founder", parties, (IEnumerable<float>)new[] { 50f })
            }));
        }

        [Test]
        public void ForeignClanForcesAreNeverEnumerated()
        {
            Assert.AreEqual(0, _invasion.MeasureReinforcementStrength(new[]
            {
                ("foreign-clan", UnexpectedForces<(bool?, Func<float>)>(), UnexpectedForces<float>())
            }));
        }

        [Test]
        public void FloatSumsStaySeparateByClanAndByForceKind()
        {
            Assert.AreEqual(33554434d, _invasion.MeasureReinforcementStrength(new[]
            {
                Forces("founder", new[] { 16777216f, 1f }, (null, 16777216f), (true, 1f)),
                Forces("expanded-clan", new[] { 1f }, (true, 1f))
            }));
        }

        [Test]
        public void EmptyForcesHaveNoStrength()
        {
            Assert.AreEqual(0, _invasion.MeasureReinforcementStrength(new[]
            {
                Forces("founder", Array.Empty<float>())
            }));
        }

        private (string ClanId, IEnumerable<(bool? IsAdopted, Func<float> Strength)> WarParties,
            IEnumerable<float> Garrisons) Forces(string clanId, float[] garrisons,
            params (bool? IsAdopted, float Strength)[] parties) =>
            (clanId, parties.Select(party => (party.IsAdopted, (Func<float>)(() => party.Strength))), garrisons);

        private IEnumerable<T> UnexpectedForces<T>() =>
            Enumerable.Range(0, 1).Select<int, T>(_ => throw new InvalidOperationException("Foreign clan"));
    }
}
