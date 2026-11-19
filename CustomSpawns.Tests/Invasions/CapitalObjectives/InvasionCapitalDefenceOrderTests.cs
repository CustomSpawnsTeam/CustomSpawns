using CustomSpawns.Invasions.CapitalObjectives;
using CustomSpawns.Invasions.CapitalObjectives.Adapters;
using NUnit.Framework;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace CustomSpawns.Tests.Invasions.CapitalObjectives
{
    public class InvasionCapitalDefenceOrderTests
    {
        private readonly InvasionCapitalOrderMapping _mapping = new();

        [Test]
        public void GatheringDefencePreservesRallyMovementUntilDomainAllowsAdvance()
        {
            InvasionCapitalOrderIntent? intent = _mapping.Find(
                InvasionCapitalObjectiveKind.Defence, InvasionCapitalObjectivePhase.Gathering,
                InvasionCapitalObjectiveKind.Defence, AiBehavior.DefendSettlement);

            Assert.That(intent.HasValue, Is.True);
            Assert.That(intent!.Value.Behavior, Is.EqualTo(AiBehavior.DefendSettlement));

            var thinkParams = new PartyThinkParams(null!);
            int moves = 0;
            intent.Value.PrepareMovement(thinkParams, () => moves++);

            Assert.That(moves, Is.EqualTo(1));
            Assert.That(thinkParams.DoNotChangeBehavior, Is.True);
        }

        [Test]
        public void DefenceAdvancingMapsToNativeDefence()
        {
            InvasionCapitalOrderIntent? intent = _mapping.Find(
                InvasionCapitalObjectiveKind.Defence, InvasionCapitalObjectivePhase.Advancing,
                InvasionCapitalObjectiveKind.Defence, AiBehavior.DefendSettlement);

            Assert.That(intent.HasValue, Is.True);
            Assert.That(intent!.Value.Behavior, Is.EqualTo(AiBehavior.DefendSettlement));

            var thinkParams = new PartyThinkParams(null!);
            intent.Value.PrepareMovement(thinkParams, () => Assert.Fail("Advancing must use native movement."));

            Assert.That(thinkParams.DoNotChangeBehavior, Is.False);
        }

        [Test]
        public void GatheringMovementProtectionEndsOnNextThinkAfterObjectiveEnds()
        {
            InvasionCapitalOrderIntent intent = _mapping.Find(InvasionCapitalObjectiveKind.Defence,
                InvasionCapitalObjectivePhase.Gathering, InvasionCapitalObjectiveKind.Defence,
                AiBehavior.DefendSettlement)!.Value;
            var thinkParams = new PartyThinkParams(null!);
            intent.PrepareMovement(thinkParams, () => { });

            thinkParams.Reset(null!);

            Assert.That(thinkParams.DoNotChangeBehavior, Is.False);
        }

        [Test]
        public void GatheringMovementProtectionIsReappliedAfterThinkCacheIsReset()
        {
            var thinkParams = new PartyThinkParams(null!);
            for (int hour = 0; hour < 72; hour++)
            {
                thinkParams.Reset(null!);
                InvasionCapitalOrderIntent intent = _mapping.Find(InvasionCapitalObjectiveKind.Defence,
                    InvasionCapitalObjectivePhase.Gathering, InvasionCapitalObjectiveKind.Defence,
                    AiBehavior.DefendSettlement)!.Value;
                intent.PrepareMovement(thinkParams, () => { });

                Assert.That(thinkParams.DoNotChangeBehavior, Is.True);
            }
        }

        [TestCase(InvasionCapitalObjectivePhase.Mobilizing)]
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
