using System.Collections.Generic;
using CustomSpawns.AI.Offensives;
using NUnit.Framework;

namespace CustomSpawns.Tests.AI
{
    public class SiegeTargetSelectorTests
    {
        private readonly SiegeTargetSelector _selector =
            new SiegeTargetSelector(new ForceRatioRules());

        [Test]
        public void GatherBeforeAssault_ChoosesClosestEligibleTargetWithoutStrengthGates()
        {
            SiegeTargetSelection? selected = _selector.FindClosest(new[]
            {
                Candidate("strong-near", 1f, defenders: 1000f, nearbyHostiles: 1000f),
                Candidate("weak-far", 2f, defenders: 1f, nearbyHostiles: 1f)
            }, Criteria(SiegeTargetSelectionMode.GatherBeforeAssault, attackerStrength: 10f));

            Assert.That(selected?.SettlementId, Is.EqualTo("strong-near"));
        }

        [Test]
        public void RequireViableNow_RequiresBothConfiguredStrengthRatios()
        {
            SiegeTargetSelection? selected = _selector.FindClosest(new[]
            {
                Candidate("target-ratio-fails", 1f, defenders: 100f, nearbyHostiles: 10f),
                Candidate("roaming-ratio-fails", 2f, defenders: 10f, nearbyHostiles: 100f),
                Candidate("viable", 3f, defenders: 40f, nearbyHostiles: 50f)
            }, Criteria(SiegeTargetSelectionMode.RequireViableNow, attackerStrength: 100f,
                minimumTargetRatio: 1.5f, minimumRoamingRatio: 1.2f));

            Assert.That(selected?.SettlementId, Is.EqualTo("viable"));
        }

        [Test]
        public void RequireViableNow_ZeroEnemyStrengthPassesAtZeroAttackerStrength()
        {
            SiegeTargetSelection? selected = _selector.FindClosest(new[]
            {
                Candidate("empty", 1f, defenders: 0f, nearbyHostiles: 0f)
            }, Criteria(SiegeTargetSelectionMode.RequireViableNow, attackerStrength: 0f,
                minimumTargetRatio: float.MaxValue, minimumRoamingRatio: float.MaxValue));

            Assert.That(selected?.SettlementId, Is.EqualTo("empty"));
        }

        [Test]
        public void RequireViableNow_AcceptsBothInclusiveRatioBoundariesAndChoosesClosest()
        {
            SiegeTargetSelection? selected = _selector.FindClosest(new[]
            {
                Candidate("exact", 1f, defenders: 80f, nearbyHostiles: 100f),
                Candidate("farther", 2f, defenders: 20f, nearbyHostiles: 20f)
            }, Criteria(SiegeTargetSelectionMode.RequireViableNow, attackerStrength: 120f,
                minimumTargetRatio: 1.5f, minimumRoamingRatio: 1.2f));

            Assert.That(selected?.SettlementId, Is.EqualTo("exact"));
        }

        [Test]
        public void FindClosest_RejectsEveryStructuralIneligibility()
        {
            SiegeTargetSelection? selected = _selector.FindClosest(new[]
            {
                Candidate("inactive", 1f, isActive: false),
                Candidate("friendly", 2f, isHostile: false),
                Candidate("wrong-kind", 3f, kind: SiegeTargetKind.Castle),
                Candidate("excluded", 4f),
                Candidate("incompatible-siege", 5f, compatibleSiege: false),
                Candidate("wrong-doctrine", 6f, strategic: false),
                Candidate("unreachable", 7f, reachable: false),
                Candidate("eligible", 8f)
            }, Criteria(SiegeTargetSelectionMode.GatherBeforeAssault, excludedSettlementId: "excluded"));

            Assert.That(selected?.SettlementId, Is.EqualTo("eligible"));
        }

        [Test]
        public void FindClosest_PreservesInputOrderForEqualDistances()
        {
            SiegeTargetSelection? selected = _selector.FindClosest(new[]
            {
                Candidate("first", 1f),
                Candidate("second", 1f)
            }, Criteria(SiegeTargetSelectionMode.GatherBeforeAssault));

            Assert.That(selected?.SettlementId, Is.EqualTo("first"));
        }

        private static SiegeTargetCriteria Criteria(SiegeTargetSelectionMode mode,
            float attackerStrength = 100f, float minimumTargetRatio = 1.5f,
            float minimumRoamingRatio = 1.2f, string? excludedSettlementId = null)
        {
            return new SiegeTargetCriteria(new[] { SiegeTargetKind.Town }, excludedSettlementId,
                attackerStrength, minimumTargetRatio, minimumRoamingRatio, mode);
        }

        [Test]
        public void APreferredTargetIsChosenOverACloserOne()
        {
            SiegeTargetSelection? selected = _selector.FindClosest(new[]
            {
                Candidate("near", 1f),
                Candidate("preferred-far", 5f, preferred: true),
                Candidate("preferred-farther", 9f, preferred: true)
            }, Criteria(SiegeTargetSelectionMode.RequireViableNow, attackerStrength: 100f));

            Assert.That(selected?.SettlementId, Is.EqualTo("preferred-far"));
        }

        [Test]
        public void TheClosestTargetIsChosenWhenNoPreferredOneIsViable()
        {
            SiegeTargetSelection? selected = _selector.FindClosest(new[]
            {
                Candidate("near", 1f),
                Candidate("preferred-too-strong", 5f, defenders: 1000f, preferred: true)
            }, Criteria(SiegeTargetSelectionMode.RequireViableNow, attackerStrength: 100f));

            Assert.That(selected?.SettlementId, Is.EqualTo("near"));
        }

        private static SiegeTargetFacts Candidate(string settlementId, float distance,
            SiegeTargetKind kind = SiegeTargetKind.Town, bool isActive = true,
            bool isHostile = true, bool compatibleSiege = true, bool strategic = true,
            bool reachable = true, float defenders = 10f, float nearbyHostiles = 10f, bool preferred = false)
        {
            return new SiegeTargetFacts(settlementId, kind, isActive, isHostile,
                compatibleSiege, strategic, reachable, distance, defenders, nearbyHostiles, preferred);
        }
    }
}
