using CustomSpawns.Invasions.Adapters;
using CustomSpawns.Invasions.CapitalObjectives;
using NUnit.Framework;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace CustomSpawns.Tests.Invasions
{
    public class InvasionCapitalDefenceOrderTests
    {
        private readonly InvasionCapitalOrderMapping _mapping = new();

        [Test]
        public void DefenceAdvancingMapsToNativeDefence()
        {
            InvasionCapitalOrderIntent? intent = _mapping.Find(
                InvasionCapitalObjectiveKind.Defence, InvasionCapitalObjectivePhase.Advancing,
                InvasionCapitalObjectiveKind.Defence, AiBehavior.DefendSettlement);

            Assert.That(intent.HasValue, Is.True);
            Assert.That(intent!.Value.Behavior, Is.EqualTo(AiBehavior.DefendSettlement));
        }

        [TestCase(InvasionCapitalObjectivePhase.Mobilizing)]
        [TestCase(InvasionCapitalObjectivePhase.Gathering)]
        [TestCase(InvasionCapitalObjectivePhase.Besieging)]
        [TestCase(InvasionCapitalObjectivePhase.Dormant)]
        public void DefenceNonDominantPhaseLeavesVanillaInControl(
            InvasionCapitalObjectivePhase phase)
        {
            Assert.That(_mapping.Find(InvasionCapitalObjectiveKind.Defence, phase,
                InvasionCapitalObjectiveKind.Defence, AiBehavior.DefendSettlement), Is.Null);
        }
    }
}
