using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using CustomSpawns.AI.Offensives;
using CustomSpawns.Invasions.Adapters;
using NUnit.Framework;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace CustomSpawns.Tests.Invasions
{
    public class InvasionCoastalSiegeTargetPolicyTests
    {
        [Test]
        public void AttackingForce_ExcludesALandingPartyStrandedAwayFromTheDecidingParty()
        {
            MobileParty party = PartyAt(0f, 0f);
            MobileParty stranded = PartyAt(0f, 40f);

            var policy = new InvasionCoastalSiegeTargetPolicy(
                Landing(_ => _ => true, party, stranded), MapPaths());

            Assert.That(policy.GetAttackingForce(party), Is.EqualTo(new[] { party }));
        }

        [Test]
        public void AttackingForce_IncludesALandingPartyGatheredWithTheDecidingParty()
        {
            MobileParty party = PartyAt(0f, 0f);
            MobileParty comrade = PartyAt(0f, 20f);

            var policy = new InvasionCoastalSiegeTargetPolicy(
                Landing(_ => _ => true, party, comrade), MapPaths());

            Assert.That(policy.GetAttackingForce(party), Is.EqualTo(new[] { party, comrade }));
        }

        [Test]
        public void AttackingForce_AlwaysHoldsTheDecidingPartyItself()
        {
            MobileParty party = PartyAt(0f, 0f);
            MobileParty stranded = PartyAt(0f, 40f);

            var withoutTheDecidingParty = new InvasionCoastalSiegeTargetPolicy(
                Landing(_ => _ => true, stranded), MapPaths());

            Assert.That(withoutTheDecidingParty.GetAttackingForce(party), Is.EqualTo(new[] { party }));
        }

        [Test]
        public void AttackingForce_LeavesTheDecidingPartyAloneOnceItsInvasionHoldsAFoothold()
        {
            MobileParty party = PartyAt(0f, 0f);
            MobileParty comrade = PartyAt(0f, 1f);

            var settled = new InvasionCoastalSiegeTargetPolicy(
                Landing(_ => null, party, comrade), MapPaths());

            Assert.That(settled.GetAttackingForce(party), Is.EqualTo(new[] { party }));
        }

        [TestCase(30f, true)]
        [TestCase(30.5f, false)]
        public void AttackingForce_CountsALandingPartyUpToTheGatheringRadius(float distance, bool isInTheForce)
        {
            MobileParty party = PartyAt(0f, 0f);
            MobileParty comrade = PartyAt(0f, distance);

            var policy = new InvasionCoastalSiegeTargetPolicy(
                Landing(_ => _ => true, party, comrade), MapPaths());

            Assert.That(policy.GetAttackingForce(party), Is.EqualTo(isInTheForce
                ? new[] { party, comrade }
                : new[] { party }));
        }

        private static IInvasionLandingQuery Landing(Func<MobileParty, Func<Settlement, bool>?> filter,
            params MobileParty[] landingParties)
        {
            return new LandingStub(filter, landingParties);
        }

        private sealed class LandingStub : IInvasionLandingQuery
        {
            private readonly Func<MobileParty, Func<Settlement, bool>?> _filter;
            private readonly IReadOnlyList<MobileParty> _landingParties;

            internal LandingStub(Func<MobileParty, Func<Settlement, bool>?> filter,
                IReadOnlyList<MobileParty> landingParties)
            {
                _filter = filter;
                _landingParties = landingParties;
            }

            public Func<Settlement, bool>? GetInitialCoastalTargetFilter(MobileParty party) => _filter(party);

            public IReadOnlyList<MobileParty> GetLandingParties(MobileParty party) => _landingParties;
        }

        private static IMapPathQuery MapPaths()
        {
            return new MapPathStub();
        }

        private sealed class MapPathStub : IMapPathQuery
        {
            public bool HasReachableLandGate(MobileParty party, Settlement target) => true;

            public float GetDistanceToGate(MobileParty party, Settlement settlement) => 0f;
        }

        private static MobileParty PartyAt(float x, float y)
        {
            var party = (MobileParty)FormatterServices.GetUninitializedObject(typeof(MobileParty));

            typeof(MobileParty).GetField("_position", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(party, new CampaignVec2(new Vec2(x, y), true));

            return party;
        }
    }
}
