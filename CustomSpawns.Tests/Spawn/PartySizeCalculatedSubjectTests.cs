using CustomSpawns.Spawn.PartySize;
using Moq;
using NUnit.Framework;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Party;

namespace CustomSpawns.Tests.Spawn
{
    public class PartySizeCalculatedSubjectTests
    {
        [Test]
        public void NotifyObservers_IsScopedToItsOwnCampaignSubject()
        {
            var firstSubject = new PartySizeCalculatedSubject();
            var secondSubject = new PartySizeCalculatedSubject();
            var observer = new CountingObserver();
            firstSubject.Register(observer);

            firstSubject.NotifyObservers(null, new ExplainedNumber(20));
            secondSubject.NotifyObservers(null, new ExplainedNumber(20));

            Assert.AreEqual(1, observer.NumberOfNotifications);
        }

        [Test]
        public void Decorator_DelegatesUnchangedModelMembers()
        {
            var decorated = new Mock<PartySizeLimitModel>();
            decorated.SetupGet(model => model.MinimumNumberOfVillagersAtVillagerParty).Returns(12);
            decorated.Setup(model => model.GetIdealVillagerPartySize(null)).Returns(34);
            var model = new CustomSpawnsPartySizeLimitModel(decorated.Object, new PartySizeCalculatedSubject());

            Assert.AreEqual(12, model.MinimumNumberOfVillagersAtVillagerParty);
            Assert.AreEqual(34, model.GetIdealVillagerPartySize(null));
        }

        [Test]
        public void Decorator_NonMobilePartyBypassesObservers()
        {
            var party = new Mock<PartyBase>();
            var decorated = new Mock<PartySizeLimitModel>();
            var expected = new ExplainedNumber(45);
            decorated.Setup(model => model.GetPartyMemberSizeLimit(party.Object, true)).Returns(expected);
            var observer = new CountingObserver();
            var subject = new PartySizeCalculatedSubject();
            subject.Register(observer);
            var model = new CustomSpawnsPartySizeLimitModel(decorated.Object, subject);

            ExplainedNumber actual = model.GetPartyMemberSizeLimit(party.Object);

            Assert.AreEqual(expected.ResultNumber, actual.ResultNumber);
            Assert.AreEqual(0, observer.NumberOfNotifications);
        }

        private class CountingObserver : IPartySizeLimitObserver
        {
            public int NumberOfNotifications { get; private set; }

            public ExplainedNumber OnPartySizeLimitCalculated(MobileParty mobileParty, ExplainedNumber partySizeLimit)
            {
                NumberOfNotifications++;
                return partySizeLimit;
            }
        }
    }
}
