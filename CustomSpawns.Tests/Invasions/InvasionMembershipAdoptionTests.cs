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

        [SetUp]
        public void SetUp()
        {
            _state = new InvasionState();
            _state.RegisterMember("invasion", "clan", "invasion_party_1");
        }

        [Test]
        public void ARespawnedLordOfAnInvadingClanIsAdoptedUnderItsGroupTemplate()
        {
            Assert.AreEqual("invasion",
                InvasionMembership.FindAdoptingTemplate(LordParty("clan"), null, _state));
        }

        [Test]
        public void ALordOfAnUninvolvedClanIsNotAdopted()
        {
            Assert.IsNull(InvasionMembership.FindAdoptingTemplate(LordParty("other-clan"), null, _state));
        }

        [Test]
        public void APartyThisModAlreadyKnowsKeepsTheIdentityItWasRegisteredWith()
        {
            var registered = new CsPartyData("invasion", "invasion", true, null, null);

            Assert.IsNull(InvasionMembership.FindAdoptingTemplate(LordParty("clan"), registered, _state));
        }

        [Test]
        public void AGarrisonOrCaravanOfAnInvadingClanIsNotALordToAdopt()
        {
            MobileParty party = DummyParty("clan");

            Assert.IsNull(InvasionMembership.FindAdoptingTemplate(party, null, _state));
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
