using CustomSpawns.Invasions.Adapters;
using CustomSpawns.Invasions.CapitalObjectives;
using NUnit.Framework;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace CustomSpawns.Tests.Invasions
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
        }

        [Test]
        public void WrongObjectiveKindDoesNotIssueOrder()
        {
            Assert.That(_mapping.Find(InvasionCapitalObjectiveKind.Defence,
                InvasionCapitalObjectivePhase.Gathering, InvasionCapitalObjectiveKind.Attack,
                AiBehavior.BesiegeSettlement), Is.Null);
        }

        [TestCase(InvasionCapitalObjectivePhase.Mobilizing)]
        [TestCase(InvasionCapitalObjectivePhase.Gathering)]
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
