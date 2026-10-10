using System.Collections.Generic;
using CustomSpawns.Data.Dao;
using CustomSpawns.Invasions;
using CustomSpawns.Invasions.Adapters;
using CustomSpawns.UtilityBehaviours;
using Moq;
using NUnit.Framework;

namespace CustomSpawns.Tests.Invasions
{
    public class InvasionRepositoriesTests
    {
        [Test]
        public void CapitalMayStartNullAndFirstRecordedCapitalNeverMoves()
        {
            var repository = new InvasionCapitalRepository();

            repository.Register("invader", null);
            Assert.IsNull(repository.FindAll()[0].CapitalSettlementId);

            repository.Register("invader", "first-town");
            repository.Register("invader", "second-town");

            Assert.That(repository.FindAll()[0].CapitalSettlementId, Is.EqualTo("first-town"));
            Assert.That(repository.FindByCapital("first-town")?.InvaderKingdomId, Is.EqualTo("invader"));
            Assert.IsNull(repository.FindByCapital("second-town"));
        }

        [Test]
        public void ClanKeepsTheFirstInvasionItWasRegisteredFor()
        {
            var spawns = new Mock<ISpawnDao>();
            spawns.Setup(repository => repository.FindAll()).Returns(new List<CustomSpawns.Data.Dto.SpawnDto>());
            var repository = new InvasionClanRepository(spawns.Object, new SaveInitialiser());

            repository.Register("invasion", "generated-clan");
            repository.Register("other-invasion", "generated-clan");

            Assert.That(repository.FindSpawnTemplate("generated-clan"), Is.EqualTo("invasion"));
            Assert.That(repository.FindClanIds("invasion"), Is.EqualTo(new[] { "generated-clan" }));
            Assert.That(repository.FindClanIds("other-invasion"), Is.Empty);
        }

        [Test]
        public void OwnershipBehaviourEmitsOnlyWhenCapitalHeldStatusChanges()
        {
            var repository = new InvasionCapitalRepository();
            repository.Register("invader", "capital");
            var behaviour = new InvasionCapitalOwnershipBehaviour(repository);
            var changes = new List<InvasionCapitalStatusChanged>();
            behaviour.CapitalStatusChanged += changes.Add;

            behaviour.ObserveOwnerChange("other-town", "invader", "enemy");
            behaviour.ObserveOwnerChange("capital", "enemy", "another-enemy");
            behaviour.ObserveOwnerChange("capital", "invader", "enemy");
            behaviour.ObserveOwnerChange("capital", "enemy", "invader");

            Assert.That(changes, Has.Count.EqualTo(2));
            Assert.False(changes[0].IsHeldByInvader);
            Assert.True(changes[1].IsHeldByInvader);
        }

        [Test]
        public void RetakeUseCaseActsOnLossButNotRecovery()
        {
            var orders = new Mock<IInvasionCapitalRetakeOrders>();
            var useCase = new RetakeInvasionCapital(orders.Object);
            var capital = new InvasionCapital("invader", "capital");

            useCase.Handle(new InvasionCapitalStatusChanged(capital, false));
            useCase.Handle(new InvasionCapitalStatusChanged(capital, true));

            orders.Verify(adapter => adapter.Retake(capital), Times.Once);
        }
    }
}
