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

        [TestCase(InvasionCapitalObjectivePhase.Gathering, true)]
        [TestCase(InvasionCapitalObjectivePhase.Advancing, false)]
        public void AttackActivePhaseMapsToNativeSiege(
            InvasionCapitalObjectivePhase phase, bool willGatherArmy)
        {
            InvasionCapitalOrderIntent? intent = _mapping.Find(
                InvasionCapitalObjectiveKind.Attack, phase,
                InvasionCapitalObjectiveKind.Attack, AiBehavior.BesiegeSettlement);

            Assert.That(intent.HasValue, Is.True);
            Assert.That(intent!.Value.Behavior, Is.EqualTo(AiBehavior.BesiegeSettlement));
            Assert.That(intent.Value.WillGatherArmy, Is.EqualTo(willGatherArmy));
        }

        [Test]
        public void WrongObjectiveKindDoesNotIssueOrder()
        {
            Assert.That(_mapping.Find(InvasionCapitalObjectiveKind.Defence,
                InvasionCapitalObjectivePhase.Gathering, InvasionCapitalObjectiveKind.Attack,
                AiBehavior.BesiegeSettlement), Is.Null);
        }

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
