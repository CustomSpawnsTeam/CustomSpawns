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
