using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using CustomSpawns.AI.Model;
using CustomSpawns.AI.Offensives;
using CustomSpawns.Data.Adapter;
using CustomSpawns.Invasions.Reinforcements.Adapters;
using NUnit.Framework;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Library;

namespace CustomSpawns.Tests.Invasions.Reinforcements
{
    public class InvasionCoastalSiegeTargetPolicyTests
    {
        [Test]
        public void AttackingForce_CountsALandingPartyOnTheSameObjectiveHoweverFarItStillIs()
        {
            MobileParty party = PartyAt(0f, 0f);
            MobileParty comrade = PartyAt(0f, 400f);
            Settlement objective = UnbesiegedTown();

            var policy = new InvasionCoastalSiegeTargetPolicy(
                Landing(_ => _ => true, party, comrade), MapPaths(),
                Objectives((party, objective), (comrade, objective)));

            Assert.That(policy.GetAttackingForce(party), Is.EqualTo(new[] { party, comrade }));
        }

        [Test]
        public void AttackingForce_ExcludesALandingPartyOnAnotherObjective()
        {
            MobileParty party = PartyAt(0f, 0f);
            MobileParty elsewhere = PartyAt(0f, 5f);

            var policy = new InvasionCoastalSiegeTargetPolicy(
                Landing(_ => _ => true, party, elsewhere), MapPaths(),
                Objectives((party, UnbesiegedTown()), (elsewhere, UnbesiegedTown())));

            Assert.That(policy.GetAttackingForce(party), Is.EqualTo(new[] { party }));
        }

        [Test]
        public void AttackingForce_ExcludesALandingPartyWithNoObjectiveOfItsOwnYet()
        {
            MobileParty party = PartyAt(0f, 0f);
            MobileParty undecided = PartyAt(0f, 5f);

            var policy = new InvasionCoastalSiegeTargetPolicy(
                Landing(_ => _ => true, party, undecided), MapPaths(),
                Objectives((party, UnbesiegedTown())));

            Assert.That(policy.GetAttackingForce(party), Is.EqualTo(new[] { party }));
        }

        [Test]
        public void AttackingForce_PoolsNoObjectiveForAPartyThatServesNoLiveInvasion()
        {
            MobileParty party = PartyAt(0f, 0f);
            MobileParty comrade = PartyAt(0f, 400f);
            Settlement objective = UnbesiegedTown();

            var policy = new InvasionCoastalSiegeTargetPolicy(
                NoLiveInvasion(party, comrade), MapPaths(),
                Objectives((party, objective), (comrade, objective)));

            Assert.That(policy.GetAttackingForce(party), Is.EqualTo(new[] { party }));
        }

        [Test]
        public void AttackingForce_StillPoolsCampMatesForAPartyThatServesNoLiveInvasion()
        {
            BesiegerCamp camp = EmptyCamp();
            MobileParty party = PartyInCamp(camp, 0f, 0f);
            MobileParty comrade = PartyInCamp(camp, 0f, 100f);

            var policy = new InvasionCoastalSiegeTargetPolicy(
                NoLiveInvasion(party, comrade), MapPaths());

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
        public void AttackingForce_CountsALandingPartyInTheSameBesiegerCampHoweverFarAway()
        {
            BesiegerCamp camp = EmptyCamp();
            MobileParty party = PartyInCamp(camp, 0f, 0f);
            MobileParty comrade = PartyInCamp(camp, 0f, 100f);

            var policy = new InvasionCoastalSiegeTargetPolicy(
                Landing(_ => _ => true, party, comrade), MapPaths());

            Assert.That(policy.GetAttackingForce(party), Is.EqualTo(new[] { party, comrade }));
        }

        [Test]
        public void AttackingForce_CountsALandingPartyBesiegingTheTownWhileTheDecidingPartyMarchesToIt()
        {
            BesiegerCamp camp = EmptyCamp();
            MobileParty besieger = PartyInCamp(camp, 0f, 100f);
            MobileParty party = PartyMarchingOn(BesiegedTown(camp), 0f, 0f);

            var policy = new InvasionCoastalSiegeTargetPolicy(
                Landing(_ => _ => true, party, besieger), MapPaths());

            Assert.That(policy.GetAttackingForce(party), Is.EqualTo(new[] { party, besieger }));
        }

        [Test]
        public void AttackingForce_ExcludesALandingPartyBesiegingATownTheDecidingPartyIsNotMarchingOn()
        {
            MobileParty besieger = PartyInCamp(EmptyCamp(), 0f, 100f);
            BesiegedTown(besieger.BesiegerCamp);
            MobileParty party = PartyMarchingOn(BesiegedTown(EmptyCamp()), 0f, 0f);

            var policy = new InvasionCoastalSiegeTargetPolicy(
                Landing(_ => _ => true, party, besieger), MapPaths());

            Assert.That(policy.GetAttackingForce(party), Is.EqualTo(new[] { party }));
        }

        [Test]
        public void AttackingForce_ExcludesADistantBesiegerWhileTheDecidingPartyIsStillLookingForATarget()
        {
            MobileParty party = PartyAt(0f, 0f);
            MobileParty besieger = PartyInCamp(EmptyCamp(), 0f, 100f);
            BesiegedTown(besieger.BesiegerCamp);

            var policy = new InvasionCoastalSiegeTargetPolicy(
                Landing(_ => _ => true, party, besieger), MapPaths());

            Assert.That(policy.GetAttackingForce(party), Is.EqualTo(new[] { party }));
        }

        [Test]
        public void AttackingForce_ExcludesALandingPartyInAnotherCampThanTheDecidingParty()
        {
            MobileParty party = PartyInCamp(EmptyCamp(), 0f, 0f);
            MobileParty elsewhere = PartyInCamp(EmptyCamp(), 0f, 100f);

            var policy = new InvasionCoastalSiegeTargetPolicy(
                Landing(_ => _ => true, party, elsewhere), MapPaths());

            Assert.That(policy.GetAttackingForce(party), Is.EqualTo(new[] { party }));
        }

        [Test]
        public void TargetDistance_PrefersASiblingObjectiveOverACandidateThatIsNotClearlyCloser()
        {
            MobileParty party = PartyAt(0f, 0f);
            MobileParty sibling = PartyAt(0f, 10f);
            Settlement siblingObjective = UnbesiegedTown();

            var policy = new InvasionCoastalSiegeTargetPolicy(
                Landing(_ => _ => true, party, sibling), MapPaths(),
                Objectives((sibling, siblingObjective)));

            Func<SiegeTargetCandidate, float> distance = policy.GetTargetDistance(party);

            Assert.That(distance(Candidate(siblingObjective, 200f)),
                Is.LessThan(distance(Candidate(UnbesiegedTown(), 150f))));
        }

        [Test]
        public void TargetDistance_KeepsItsOwnCandidateWhenThatOneIsClearlyCloser()
        {
            MobileParty party = PartyAt(0f, 0f);
            MobileParty sibling = PartyAt(0f, 10f);
            Settlement siblingObjective = UnbesiegedTown();

            var policy = new InvasionCoastalSiegeTargetPolicy(
                Landing(_ => _ => true, party, sibling), MapPaths(),
                Objectives((sibling, siblingObjective)));

            Func<SiegeTargetCandidate, float> distance = policy.GetTargetDistance(party);

            Assert.That(distance(Candidate(siblingObjective, 240f)),
                Is.GreaterThan(distance(Candidate(UnbesiegedTown(), 150f))));
        }

        [Test]
        public void TargetDistance_IgnoresASiblingObjectiveBeyondTheGatheringReach()
        {
            MobileParty party = PartyAt(0f, 0f);
            MobileParty sibling = PartyAt(0f, 10f);
            Settlement siblingObjective = UnbesiegedTown();

            var policy = new InvasionCoastalSiegeTargetPolicy(
                Landing(_ => _ => true, party, sibling), MapPaths(),
                Objectives((sibling, siblingObjective)));

            Assert.That(policy.GetTargetDistance(party)(Candidate(siblingObjective, 300f)),
                Is.EqualTo(300f));
        }

        [Test]
        public void TargetDistance_DiscountsNothingForAPartyThatServesNoLiveInvasion()
        {
            MobileParty party = PartyAt(0f, 0f);
            MobileParty sibling = PartyAt(0f, 10f);
            Settlement siblingObjective = UnbesiegedTown();

            var policy = new InvasionCoastalSiegeTargetPolicy(
                NoLiveInvasion(party, sibling), MapPaths(), Objectives((sibling, siblingObjective)));

            Assert.That(policy.GetTargetDistance(party)(Candidate(siblingObjective, 200f)),
                Is.EqualTo(200f));
        }

        [Test]
        public void GathersBeforeAssault_OnlyWhileThePartyServesALiveInvasion()
        {
            MobileParty party = PartyAt(0f, 0f);

            Assert.Multiple(() =>
            {
                Assert.That(new InvasionCoastalSiegeTargetPolicy(
                    Landing(_ => _ => true, party), MapPaths()).GathersBeforeAssault(party), Is.True);
                Assert.That(new InvasionCoastalSiegeTargetPolicy(
                    NoLiveInvasion(party), MapPaths()).GathersBeforeAssault(party), Is.False);
            });
        }

        [Test]
        public void JoinableSiege_WhenALandingPartyIsInTheBesiegerCamp()
        {
            MobileParty party = PartyAt(0f, 0f);
            BesiegerCamp camp = EmptyCamp();
            MobileParty comrade = PartyInCamp(camp, 0f, 100f);

            var policy = new InvasionCoastalSiegeTargetPolicy(
                Landing(_ => _ => true, party, comrade), MapPaths());

            Assert.That(policy.IsJoinableSiege(party, BesiegedTown(camp)), Is.True);
        }

        [Test]
        public void JoinableSiege_IsRefusedWhenTheBesiegerCampHoldsNoLandingParty()
        {
            MobileParty party = PartyAt(0f, 0f);
            MobileParty comrade = PartyAt(0f, 1f);

            var policy = new InvasionCoastalSiegeTargetPolicy(
                Landing(_ => _ => true, party, comrade), MapPaths());

            Assert.That(policy.IsJoinableSiege(party, BesiegedTown(CampOf(StrangerParty()))), Is.False);
        }

        [Test]
        public void JoinableSiege_IsRefusedWhenTheCampIsBesiegingAnotherTown()
        {
            MobileParty party = PartyAt(0f, 0f);
            BesiegerCamp camp = EmptyCamp();
            MobileParty comrade = PartyInCamp(camp, 0f, 1f);
            Settlement besiegedTown = BesiegedTown(camp);

            Settlement otherTown = UnbesiegedTown();
            otherTown.SiegeEvent = besiegedTown.SiegeEvent;

            var policy = new InvasionCoastalSiegeTargetPolicy(
                Landing(_ => _ => true, party, comrade), MapPaths());

            Assert.That(policy.IsJoinableSiege(party, otherTown), Is.False);
        }

        [Test]
        public void JoinableSiege_IsRefusedWhenNobodyIsBesiegingTheTown()
        {
            MobileParty party = PartyAt(0f, 0f);
            BesiegerCamp camp = EmptyCamp();
            MobileParty comrade = PartyInCamp(camp, 0f, 1f);

            var policy = new InvasionCoastalSiegeTargetPolicy(
                Landing(_ => _ => true, party, comrade), MapPaths());

            Assert.That(policy.IsJoinableSiege(party, UnbesiegedTown()), Is.False);
        }

        [Test]
        public void JoinableSiege_IsAlwaysRefusedWithoutASiegeTargetPolicy()
        {
            BesiegerCamp camp = EmptyCamp();
            MobileParty party = PartyInCamp(camp, 0f, 0f);

            Assert.That(new NoRestrictionSiegeTargetPolicy().IsJoinableSiege(party, BesiegedTown(camp)),
                Is.False);
        }

        private static IInvasionLandingQuery Landing(Func<MobileParty, Func<Settlement, bool>?> filter,
            params MobileParty[] landingParties)
        {
            return new LandingStub(filter, landingParties, true);
        }

        private static IInvasionLandingQuery NoLiveInvasion(params MobileParty[] landingParties)
        {
            return new LandingStub(_ => _ => true, landingParties, false);
        }

        private sealed class LandingStub : IInvasionLandingQuery
        {
            private readonly Func<MobileParty, Func<Settlement, bool>?> _filter;
            private readonly IReadOnlyList<MobileParty> _landingParties;
            private readonly bool _servesLiveInvasion;

            public LandingStub(Func<MobileParty, Func<Settlement, bool>?> filter,
                IReadOnlyList<MobileParty> landingParties, bool servesLiveInvasion)
            {
                _filter = filter;
                _landingParties = landingParties;
                _servesLiveInvasion = servesLiveInvasion;
            }

            public Func<Settlement, bool>? GetInitialCoastalTargetFilter(MobileParty party) => _filter(party);

            public IReadOnlyList<MobileParty> GetLandingParties(MobileParty party) => _landingParties;

            public bool ServesLiveInvasion(MobileParty party) => _servesLiveInvasion;
        }

        // The objectives the siege behaviour has recorded, which is what the policy reads to tell who
        // belongs to one force and which objectives a party's siblings are already on.
        private static Func<MobileParty, Settlement?> Objectives(
            params (MobileParty Party, Settlement Objective)[] objectives)
        {
            var recorded = new Dictionary<MobileParty, Settlement>();

            foreach ((MobileParty party, Settlement objective) in objectives)
            {
                recorded[party] = objective;
            }

            return party => recorded.TryGetValue(party, out Settlement? objective) ? objective : null;
        }

        private static SiegeTargetCandidate Candidate(Settlement target, float distance)
        {
            return new SiegeTargetCandidate(target, true, true, SpawnSettlementType.Town, false, distance, 0f);
        }

        private static IMapPathQuery MapPaths()
        {
            return new MapPathStub();
        }

        private sealed class MapPathStub : IMapPathQuery
        {
            public bool HasReachableLandGate(MobileParty party, Settlement target) => true;
        }

        private static MobileParty PartyAt(float x, float y)
        {
            var party = (MobileParty)FormatterServices.GetUninitializedObject(typeof(MobileParty));

            typeof(MobileParty).GetField("_position", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(party, new CampaignVec2(new Vec2(x, y), true));
            typeof(MobileParty).GetField("<Party>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(party, FormatterServices.GetUninitializedObject(typeof(PartyBase)));

            return party;
        }

        private static MobileParty PartyInCamp(BesiegerCamp camp, float x, float y)
        {
            MobileParty party = PartyAt(x, y);

            typeof(MobileParty).GetField("_besiegerCamp", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(party, camp);

            return party;
        }

        // A party under way to besiege a town, as SetMoveBesiegeSettlement leaves it: target and behaviour
        // together. Both setters run campaign code an uninitialised party cannot, so the fields are set.
        private static MobileParty PartyMarchingOn(Settlement target, float x, float y)
        {
            MobileParty party = PartyAt(x, y);

            typeof(MobileParty).GetField("_targetSettlement", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(party, target);
            typeof(MobileParty).GetField("_defaultBehavior", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(party, AiBehavior.BesiegeSettlement);

            return party;
        }

        // A party of somebody else's camp, wired up for the real BesiegerCamp lookups that walk it.
        private static MobileParty StrangerParty()
        {
            MobileParty party = PartyAt(0f, 0f);

            typeof(MobileParty).GetField("_attachedParties", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(party, new MBList<MobileParty>());
            typeof(PartyBase).GetField("<MobileParty>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(party.Party, party);

            return party;
        }

        private static BesiegerCamp EmptyCamp()
        {
            var camp = (BesiegerCamp)FormatterServices.GetUninitializedObject(typeof(BesiegerCamp));

            typeof(BesiegerCamp).GetField("_besiegerParties", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(camp, new MBList<MobileParty>());

            return camp;
        }

        private static BesiegerCamp CampOf(MobileParty besieger)
        {
            BesiegerCamp camp = EmptyCamp();

            ((MBList<MobileParty>)typeof(BesiegerCamp)
                    .GetField("_besiegerParties", BindingFlags.NonPublic | BindingFlags.Instance)
                    .GetValue(camp))
                .Add(besieger);

            return camp;
        }

        private static Settlement BesiegedTown(BesiegerCamp camp)
        {
            var siegeEvent = (SiegeEvent)FormatterServices.GetUninitializedObject(typeof(SiegeEvent));
            typeof(SiegeEvent).GetField("BesiegerCamp").SetValue(siegeEvent, camp);
            typeof(BesiegerCamp).GetField("<SiegeEvent>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(camp, siegeEvent);

            Settlement town = UnbesiegedTown();
            typeof(SiegeEvent).GetField("BesiegedSettlement").SetValue(siegeEvent, town);
            town.SiegeEvent = siegeEvent;

            return town;
        }

        private static Settlement UnbesiegedTown()
        {
            return (Settlement)FormatterServices.GetUninitializedObject(typeof(Settlement));
        }
    }
}
