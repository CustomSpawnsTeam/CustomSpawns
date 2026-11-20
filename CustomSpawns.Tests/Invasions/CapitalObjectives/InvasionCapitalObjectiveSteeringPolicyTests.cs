using CustomSpawns.Invasions.CapitalObjectives;
using NUnit.Framework;

namespace CustomSpawns.Tests.Invasions.CapitalObjectives
{
    public class InvasionCapitalObjectiveSteeringPolicyTests
    {
        private readonly InvasionCapitalObjectiveSteeringPolicy _policy = new();

        [Test]
        public void MobilizingPartyKeepsConfiguredSteering()
        {
            var objective = new InvasionCapitalObjective(InvasionCapitalObjectiveKind.Defence,
                "capital", 0d);

            Assert.That(_policy.IsReleasedToNative(objective, "member", "commander"), Is.False);
        }

        [Test]
        public void GatheringRosterMemberInUnifiedArmyIsReleasedToNative()
        {
            InvasionCapitalObjective objective = GatheringObjective(
                InvasionCapitalObjectiveKind.Defence);

            Assert.That(_policy.IsReleasedToNative(objective, "member", "commander"), Is.True);
        }

        [TestCase(null)]
        [TestCase("other-commander")]
        public void GatheringRosterMemberWithoutUnifiedArmyLinkKeepsConfiguredSteering(
            string? nativeCommanderPartyId)
        {
            InvasionCapitalObjective objective = GatheringObjective(
                InvasionCapitalObjectiveKind.Defence);

            Assert.That(_policy.IsReleasedToNative(objective, "member", nativeCommanderPartyId),
                Is.False);
        }

        [Test]
        public void AdvancingDetachedInvitationKeepsConfiguredSteering()
        {
            InvasionCapitalObjective objective = GatheringObjective(
                InvasionCapitalObjectiveKind.Attack);
            objective.MarkReady();

            Assert.That(_policy.IsReleasedToNative(objective, "member", null), Is.False);
        }

        [Test]
        public void AdvancingCommanderAndAttachedMemberRemainUnderNativeArmyControl()
        {
            InvasionCapitalObjective objective = GatheringObjective(
                InvasionCapitalObjectiveKind.Attack);
            objective.MarkReady();

            Assert.That(_policy.IsReleasedToNative(objective, "commander", "commander"), Is.True);
            Assert.That(_policy.IsReleasedToNative(objective, "member", "commander"), Is.True);
        }

        [Test]
        public void BesiegingMemberRemainsUnderNativeArmyControl()
        {
            InvasionCapitalObjective objective = GatheringObjective(
                InvasionCapitalObjectiveKind.Attack);
            objective.MarkReady();
            objective.MarkBesieging();

            Assert.That(_policy.IsReleasedToNative(objective, "member", "commander"), Is.True);
        }

        [Test]
        public void PartyOutsidePersistedRosterKeepsConfiguredSteering()
        {
            InvasionCapitalObjective objective = GatheringObjective(
                InvasionCapitalObjectiveKind.Defence);

            Assert.That(_policy.IsReleasedToNative(objective, "outsider", "commander"), Is.False);
        }

        [Test]
        public void AbortedObjectiveDoesNotSuppressFormerArmyMember()
        {
            InvasionCapitalObjective objective = GatheringObjective(
                InvasionCapitalObjectiveKind.Attack);
            objective.MarkReady();
            objective.MarkBesieging();
            objective.AbortSiege();

            Assert.That(_policy.IsReleasedToNative(objective, "member", "commander"), Is.False);
        }

        [Test]
        public void CompletedObjectiveDoesNotSuppressFormerArmyMember()
        {
            Assert.That(_policy.IsReleasedToNative(null, "member", "commander"), Is.False);
        }

        private static InvasionCapitalObjective GatheringObjective(
            InvasionCapitalObjectiveKind kind)
        {
            var objective = new InvasionCapitalObjective(kind, "capital", 0d);
            objective.MarkMobilized(new CapitalArmyMobilizationResult(true, "commander",
                new[] { "commander", "member" }, "staging"), 0d);
            return objective;
        }
    }
}
