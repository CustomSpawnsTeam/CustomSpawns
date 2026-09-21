using System.Runtime.Serialization;
using CustomSpawns.Invasions;
using CustomSpawns.Invasions.Adapters;
using CustomSpawns.Spawn;
using NUnit.Framework;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Roster;

namespace CustomSpawns.Tests.Invasions
{
    public class InvasionMembershipAdoptionTests
    {
        private InvasionState _state = null!;
        private InvasionGroupState _group = null!;

        [SetUp]
        public void SetUp()
        {
            _state = new InvasionState();
            _group = _state.RegisterMember("invasion", "clan", "invasion_party_1");
        }

        [Test]
        public void ARespawnedLordOfAnInvadingClanIsAdoptedUnderItsGroupTemplate()
        {
            Assert.AreEqual("invasion", AdoptingTemplate(LordParty("clan")));
        }

        [Test]
        public void ALordOfAnUninvolvedClanIsNotAdopted()
        {
            Assert.IsNull(AdoptingTemplate(LordParty("other-clan")));
        }

        [Test]
        public void APartyThisModAlreadyKnowsKeepsTheIdentityItWasRegisteredWith()
        {
            var registered = new CsPartyData("invasion", "invasion", true, null, null);

            Assert.IsNull(AdoptingTemplate(LordParty("clan"), registered));
        }

        [Test]
        public void AGarrisonOrCaravanOfAnInvadingClanIsNotALordToAdopt()
        {
            MobileParty party = DummyParty("clan");

            Assert.IsNull(AdoptingTemplate(party));
        }

        [Test]
        public void AnInvasionFieldingItsBudgetedPartiesAdoptsOnlyWhatItLoses()
        {
            _state.RegisterMember("invasion", "clan", "invasion_party_2");

            Assert.IsNull(AdoptingTemplate(LordParty("clan"), budget: 2));

            _state.RemoveMember("invasion_party_2");
            Assert.AreEqual("invasion", AdoptingTemplate(LordParty("clan"), budget: 2));
        }

        [Test]
        public void AnAdoptedLordDoesNotSpendTheInvasionsLandingBudget()
        {
            Assert.False(InvasionReinforcementArrivals.CountsAgainstActivePartyCap(PartyOfTheGroup("Adopted"), _group));
            Assert.True(InvasionReinforcementArrivals.CountsAgainstActivePartyCap(PartyOfTheGroup("Reinforcement"),
                _group));
        }

        [Test]
        public void AnAdoptedLordComesBackFromASaveAsAnAdoptedLord()
        {
            Assert.AreEqual("Adopted", InvasionMembership.RoleFor(PartyOfTheGroup("Adopted")));
            Assert.AreEqual("Reinforcement", InvasionMembership.RoleFor(PartyOfTheGroup("Reinforcement")));
        }

        [Test]
        public void AnAdoptedLordIsNotAdoptedASecondTime()
        {
            var adopted = new CsPartyData("invasion", "invasion", false, null, null, "Adopted");

            Assert.True(adopted.IsAdopted);
            Assert.IsNull(AdoptingTemplate(LordParty("clan"), adopted));
        }

        [Test]
        public void AnAdoptedLordTradesTheClanTroopsItCameBackWithForTheInvasionsOwn()
        {
            CharacterObject lord = Lord();
            CharacterObject clanTroop = Troop();
            CharacterObject invasionTroop = Troop();
            TroopRoster roster = RosterOf(Member(lord, 1), Member(clanTroop, 20));

            InvasionMembership.ReplaceRegulars(roster, RosterOf(Member(invasionTroop, 200)));

            Assert.AreEqual(1, roster.GetTroopCount(lord));
            Assert.AreEqual(0, roster.GetTroopCount(clanTroop));
            Assert.AreEqual(200, roster.GetTroopCount(invasionTroop));
        }

        [Test]
        public void AnAdoptedLordIsRefilledWithTroopsItLeadsAndNotWithPrisoners()
        {
            CharacterObject invasionTroop = Troop();
            MobileParty party = PartyWithRosters();

            InvasionMembership.ReplaceRegulars(party, RosterOf(Member(invasionTroop, 200)));

            Assert.AreEqual(200, party.MemberRoster.GetTroopCount(invasionTroop));
            Assert.AreEqual(0, party.PrisonRoster.GetTroopCount(invasionTroop));
        }

        private static MobileParty PartyWithRosters()
        {
            MobileParty party = DummyParty("clan");
            var partyBase = (PartyBase)FormatterServices.GetUninitializedObject(typeof(PartyBase));

            Field(typeof(MobileParty), "<Party>k__BackingField").SetValue(party, partyBase);
            Field(typeof(PartyBase), "<MemberRoster>k__BackingField")
                .SetValue(partyBase, TroopRoster.CreateDummyTroopRoster());
            Field(typeof(PartyBase), "<PrisonRoster>k__BackingField")
                .SetValue(partyBase, TroopRoster.CreateDummyTroopRoster());

            return party;
        }

        private static TroopRoster RosterOf(params TroopRosterElement[] members)
        {
            TroopRoster roster = TroopRoster.CreateDummyTroopRoster();
            Field(typeof(TroopRoster), "data").SetValue(roster, members);
            Field(typeof(TroopRoster), "_count").SetValue(roster, members.Length);
            roster.UpdateVersion();
            return roster;
        }

        private static TroopRosterElement Member(CharacterObject character, int number)
        {
            var member = new TroopRosterElement(character);
            member.Number = number;
            return member;
        }

        private static CharacterObject Lord()
        {
            CharacterObject lord = Troop();
            Field(typeof(CharacterObject), "_heroObject")
                .SetValue(lord, FormatterServices.GetUninitializedObject(typeof(Hero)));
            return lord;
        }

        private static CharacterObject Troop() =>
            (CharacterObject)FormatterServices.GetUninitializedObject(typeof(CharacterObject));

        private static System.Reflection.FieldInfo Field(System.Type owner, string name) =>
            owner.GetField(name, System.Reflection.BindingFlags.Instance |
                                 System.Reflection.BindingFlags.NonPublic)!;

        private string? AdoptingTemplate(MobileParty party, CsPartyData? registered = null, int budget = int.MaxValue) =>
            InvasionMembership.FindAdoptingTemplate(party, registered, _state,
                group => group.MemberPartyIds.Count, _ => budget);

        private CsPartyData PartyOfTheGroup(string partyRole)
        {
            var data = new CsPartyData("invasion", "invasion", false, null, null, partyRole);
            data.SetInvasionIdentity(_group.Id, null);
            return data;
        }

        private static MobileParty LordParty(string clanId)
        {
            MobileParty party = DummyParty(clanId);
            typeof(MobileParty).GetField("_partyComponent",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .SetValue(party, FormatterServices.GetUninitializedObject(typeof(LordPartyComponent)));
            return party;
        }

        private static MobileParty DummyParty(string clanId)
        {
            var party = (MobileParty)FormatterServices.GetUninitializedObject(typeof(MobileParty));
            var clan = (Clan)FormatterServices.GetUninitializedObject(typeof(Clan));
            clan.StringId = clanId;
            party.ActualClan = clan;
            return party;
        }
    }
}
