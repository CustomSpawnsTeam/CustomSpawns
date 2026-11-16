using CustomSpawns.Invasions.CapitalObjectives;
using CustomSpawns.Invasions.CapitalObjectives.Adapters;
using NUnit.Framework;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace CustomSpawns.Tests.Invasions.CapitalObjectives
{
    public class InvasionCapitalAttackOrderTests
    {
        private readonly InvasionCapitalOrderMapping _mapping = new();

        [Test]
        public void AttackAdvancingMapsToNativeSiege()
        {
            InvasionCapitalOrderIntent? intent = _mapping.Find(
                InvasionCapitalObjectiveKind.Attack, InvasionCapitalObjectivePhase.Advancing,
                InvasionCapitalObjectiveKind.Attack, AiBehavior.BesiegeSettlement);

            Assert.That(intent.HasValue, Is.True);
            Assert.That(intent!.Value.Behavior, Is.EqualTo(AiBehavior.BesiegeSettlement));

            var thinkParams = new PartyThinkParams(null!);
            intent.Value.PrepareMovement(thinkParams, () => Assert.Fail("Advancing must use native movement."));

            Assert.That(thinkParams.DoNotChangeBehavior, Is.False);
        }

        [Test]
        public void GatheringRecapturePreservesRallyMovementUntilDomainAllowsAdvance()
        {
            InvasionCapitalOrderIntent? intent = _mapping.Find(
                InvasionCapitalObjectiveKind.Attack, InvasionCapitalObjectivePhase.Gathering,
                InvasionCapitalObjectiveKind.Attack, AiBehavior.BesiegeSettlement);

            Assert.That(intent.HasValue, Is.True);
            Assert.That(intent!.Value.Behavior, Is.EqualTo(AiBehavior.DefendSettlement));

            var thinkParams = new PartyThinkParams(null!);
            int moves = 0;
            intent.Value.PrepareMovement(thinkParams, () => moves++);

            Assert.That(moves, Is.EqualTo(1));
            Assert.That(thinkParams.DoNotChangeBehavior, Is.True);

            thinkParams.Reset(null!);

            Assert.That(thinkParams.DoNotChangeBehavior, Is.False);
        }

        [Test]
        public void WrongObjectiveKindDoesNotIssueOrder()
        {
            Assert.That(_mapping.Find(InvasionCapitalObjectiveKind.Defence,
                InvasionCapitalObjectivePhase.Gathering, InvasionCapitalObjectiveKind.Attack,
                AiBehavior.BesiegeSettlement), Is.Null);
        }

        [TestCase(InvasionCapitalObjectivePhase.Mobilizing)]
        [TestCase(InvasionCapitalObjectivePhase.Besieging)]
        [TestCase(InvasionCapitalObjectivePhase.Dormant)]
        public void AttackNonDominantPhaseLeavesVanillaInControl(
            InvasionCapitalObjectivePhase phase)
        {
            Assert.That(_mapping.Find(InvasionCapitalObjectiveKind.Attack, phase,
                InvasionCapitalObjectiveKind.Attack, AiBehavior.BesiegeSettlement), Is.Null);
        }
    }
}
