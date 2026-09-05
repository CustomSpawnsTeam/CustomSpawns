using System.Runtime.Serialization;
using CustomSpawns.AI.Behaviours;
using CustomSpawns.Spawn;
using NUnit.Framework;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace CustomSpawns.Tests.AI
{
    public class FollowSupportedPartyBehaviourTests
    {
        [Test]
        public void Behaviour_EscortsTheMainPartyItSpawnedWith()
        {
            MobileParty party = DummyParty();
            MobileParty supportedParty = DummyParty();

            PartyBehaviourResult result = Behaviour().GetPriorityBehaviour(party, PartyData(supportedParty));

            Assert.That(result.IsApplicable, Is.True);
            Assert.That(result.AIBehavior.HasValue, Is.True);
            Assert.That(result.AIBehavior!.Value.AiBehavior, Is.EqualTo(AiBehavior.EscortParty));
            Assert.That(result.AIBehavior.Value.Party, Is.SameAs(supportedParty));
            Assert.That(result.AIBehavior.Value.NavigationType, Is.EqualTo(MobileParty.NavigationType.Default));
        }

        [Test]
        public void Behaviour_IsNotApplicableWithoutAMainParty()
        {
            PartyBehaviourResult result = Behaviour().GetPriorityBehaviour(DummyParty(), PartyData(null));

            Assert.That(result.IsApplicable, Is.False);
        }

        [Test]
        public void Behaviour_IsNotApplicableWhenTheMainPartyIsNoLongerActive()
        {
            MobileParty supportedParty = DummyParty();

            PartyBehaviourResult result = Behaviour(isActive: false)
                .GetPriorityBehaviour(DummyParty(), PartyData(supportedParty));

            Assert.That(result.IsApplicable, Is.False);
        }

        [Test]
        public void Behaviour_IsNotApplicableWhenAPartySupportsItself()
        {
            MobileParty party = DummyParty();

            PartyBehaviourResult result = Behaviour().GetPriorityBehaviour(party, PartyData(party));

            Assert.That(result.IsApplicable, Is.False);
        }

        private static FollowSupportedPartyBehaviour Behaviour(bool isActive = true)
        {
            return new FollowSupportedPartyBehaviour(_ => isActive, _ => MobileParty.NavigationType.Default);
        }

        private static CsPartyData PartyData(MobileParty? supportedParty)
        {
            return new CsPartyData("party", "spawn", false, null, supportedParty);
        }

        private static MobileParty DummyParty()
        {
            return (MobileParty)FormatterServices.GetUninitializedObject(typeof(MobileParty));
        }
    }
}
