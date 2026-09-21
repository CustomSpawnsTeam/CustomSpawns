using System.Runtime.Serialization;
using CustomSpawns.Invasions;
using CustomSpawns.Invasions.Adapters;
using CustomSpawns.Spawn;
using NUnit.Framework;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;

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
