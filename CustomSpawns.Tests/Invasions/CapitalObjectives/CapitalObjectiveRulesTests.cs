using CustomSpawns.AI.Offensives;
using CustomSpawns.Invasions.CapitalObjectives;
using NUnit.Framework;

namespace CustomSpawns.Tests.Invasions.CapitalObjectives
{
    public class CapitalObjectiveRulesTests
    {
        private CapitalObjectiveStrengthRules _strengthRules = null!;

        [SetUp]
        public void SetUp()
        {
            _strengthRules = new CapitalObjectiveStrengthRules(new ForceRatioRules());
        }

        [Test]
        public void DefenceCombinesAssembledGarrisonAndOccupantsAtTheExactRatio()
        {
            var justShort = new CapitalDefenceForces(39.99f, 20f, 50f);
            var exact = new CapitalDefenceForces(40f, 20f, 50f);

            Assert.That(_strengthRules.CanDefend(justShort, 1.2f), Is.False);
            Assert.That(_strengthRules.CanDefend(exact, 1.2f), Is.True);
        }

        [Test]
        public void DefenceWithNoEnemyIsReady()
        {
            Assert.That(_strengthRules.CanDefend(new CapitalDefenceForces(0f, 0f, 0f),
                float.MaxValue), Is.True);
        }

        [Test]
        public void AttackMustPassTargetAndRoamingRatios()
        {
            Assert.That(_strengthRules.CanAttack(new CapitalAttackForces(150f, 100f, 120f),
                1.5f, 1.2f), Is.True);
            Assert.That(_strengthRules.CanAttack(new CapitalAttackForces(149.99f, 100f, 100f),
                1.5f, 1.2f), Is.False);
            Assert.That(_strengthRules.CanAttack(new CapitalAttackForces(150f, 90f, 126f),
                1.5f, 1.2f), Is.False);
        }

        [TestCase(InvasionSupplyLineKind.Coastal, true, true, true)]
        [TestCase(InvasionSupplyLineKind.Coastal, true, false, false)]
        [TestCase(InvasionSupplyLineKind.Coastal, false, false, false)]
        [TestCase(InvasionSupplyLineKind.Any, true, false, true)]
        [TestCase(InvasionSupplyLineKind.Any, false, false, false)]
        [TestCase(InvasionSupplyLineKind.None, true, false, true)]
        [TestCase(InvasionSupplyLineKind.None, false, false, true)]
        public void OriginalCoastalDoctrineUsesConfiguredSupplyBaseSemantics(
            InvasionSupplyLineKind supplyLine, bool isTown, bool isCoastalTown, bool expected)
        {
            var requirements = new InvasionAssaultRequirements(
                new[] { SiegeTargetKind.Town, SiegeTargetKind.Castle }, 1f, 1f, true,
                supplyLine);

            Assert.That(requirements.MatchesStrategicRequirement(isTown, isCoastalTown),
                Is.EqualTo(expected));
        }

        [Test]
        public void NonCoastalDoctrineDoesNotRestrictFallbackTargetKind()
        {
            var requirements = new InvasionAssaultRequirements(
                new[] { SiegeTargetKind.Town, SiegeTargetKind.Castle }, 1f, 1f, false,
                InvasionSupplyLineKind.Coastal);

            Assert.That(requirements.MatchesStrategicRequirement(false, false), Is.True);
        }
    }
}
