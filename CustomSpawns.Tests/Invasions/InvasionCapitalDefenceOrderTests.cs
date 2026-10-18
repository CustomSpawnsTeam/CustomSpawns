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

        [TestCase(InvasionCapitalObjectivePhase.Gathering, true)]
        [TestCase(InvasionCapitalObjectivePhase.Advancing, false)]
        public void DefenceActivePhaseMapsToNativeDefence(
            InvasionCapitalObjectivePhase phase, bool willGatherArmy)
        {
            InvasionCapitalOrderIntent? intent = _mapping.Find(
                InvasionCapitalObjectiveKind.Defence, phase,
                InvasionCapitalObjectiveKind.Defence, AiBehavior.DefendSettlement);

            Assert.That(intent.HasValue, Is.True);
            Assert.That(intent!.Value.Behavior, Is.EqualTo(AiBehavior.DefendSettlement));
            Assert.That(intent.Value.WillGatherArmy, Is.EqualTo(willGatherArmy));
        }

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
