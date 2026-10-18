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
            Assert.That(repository.FindAll()[0].InvaderKingdomId, Is.EqualTo("invader"));
        }

        [Test]
        public void ObjectiveCaptureMayExplicitlyRelocateCapital()
        {
            var repository = new InvasionCapitalRepository();
            repository.Register("invader", "lost-capital");

            repository.RelocateAfterObjectiveCapture("invader", "replacement-capital");

            Assert.That(repository.FindAll()[0].CapitalSettlementId,
                Is.EqualTo("replacement-capital"));
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

    }
}
