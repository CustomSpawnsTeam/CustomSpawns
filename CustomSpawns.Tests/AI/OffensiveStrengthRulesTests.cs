using CustomSpawns.AI.Model;
using NUnit.Framework;

namespace CustomSpawns.Tests.AI
{
    public class OffensiveStrengthRulesTests
    {
        [Test]
        public void VisibleHostiles_UseObserverSeeingRangeBoundaryAndExcludeInvalidParties()
        {
            var parties = new[]
            {
                Strength(distance: 5f, partyStrength: 10f),
                Strength(distance: 5.001f, partyStrength: 100f),
                Strength(distance: 1f, partyStrength: 100f, isSelf: true),
                Strength(distance: 1f, partyStrength: 100f, isActive: false),
                Strength(distance: 1f, partyStrength: 100f, isHostile: false)
            };

            Assert.That(OffensiveStrengthRules.SumVisibleHostileStrength(parties, 5f),
                Is.EqualTo(10f));
            Assert.That(OffensiveStrengthRules.IsWithinSeeingRange(5f, 5f), Is.True);
            Assert.That(OffensiveStrengthRules.IsWithinSeeingRange(5.001f, 5f), Is.False);
        }

        [Test]
        public void VisibleHostiles_CountOnlyVisibleArmyMembersOnce()
        {
            object army = new();
            var parties = new[]
            {
                Strength(1f, 20f, army),
                Strength(1f, 30f, army),
                Strength(5.001f, 100f, army)
            };

            Assert.That(OffensiveStrengthRules.SumVisibleHostileStrength(parties, 5f),
                Is.EqualTo(50f));
        }

        [Test]
        public void VisibleHostiles_CountVisibleAttachmentWhenLeaderIsOutsideRange()
        {
            object army = new();
            var parties = new[]
            {
                Strength(5.001f, 20f, army),
                Strength(5f, 30f, army)
            };

            Assert.That(OffensiveStrengthRules.SumVisibleHostileStrength(parties, 5f),
                Is.EqualTo(30f));
        }

        [Test]
        public void VisibleHostiles_DoNotCountInvisibleAttachmentsThroughVisibleLeader()
        {
            object army = new();
            var parties = new[]
            {
                Strength(5f, 20f, army),
                Strength(5.001f, 30f, army)
            };

            Assert.That(OffensiveStrengthRules.SumVisibleHostileStrength(parties, 5f),
                Is.EqualTo(20f));
        }

        [Test]
        public void VisibleHostileLordThreat_ExcludesCivilianAndOtherNonLordParties()
        {
            var parties = new[]
            {
                Strength(5f, 20f, isLordParty: true),
                Strength(5f, 100f),
                Strength(5.001f, 100f, isLordParty: true),
                Strength(5f, 100f, isLordParty: true, isActive: false),
                Strength(5f, 100f, isLordParty: true, isHostile: false)
            };

            Assert.That(OffensiveStrengthRules.SumVisibleHostileLordStrength(parties, 5f),
                Is.EqualTo(20f));
        }

        [Test]
        public void ClosestQualifiedTarget_HandlesZeroDefendersAndRetargetsByDistance()
        {
            object weak = new();
            object zero = new();
            object closestQualified = new();
            var candidates = new[]
            {
                new RaidTargetCandidate<object>(weak, true, 1f, 100f),
                new RaidTargetCandidate<object>(zero, true, 3f, 0f),
                new RaidTargetCandidate<object>(closestQualified, true, 2f, 10f)
            };

            object? selected = OffensiveStrengthRules.ChooseClosestTarget(candidates, 20f, 1.5f);

            Assert.That(selected, Is.SameAs(closestQualified));
            Assert.That(OffensiveStrengthRules.MeetsStrengthRatio(0f, 0f, 100f), Is.True);
        }

        private static RoamingPartyStrength Strength(float distance, float partyStrength,
            object? armyIdentity = null, bool isSelf = false, bool isActive = true,
            bool isHostile = true, bool isLordParty = false)
        {
            return new RoamingPartyStrength(isSelf, isActive, isHostile, armyIdentity,
                distance, partyStrength, isLordParty);
        }
    }
}
