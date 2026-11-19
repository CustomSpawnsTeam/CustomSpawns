using CustomSpawns.Invasions.CapitalObjectives;
using NUnit.Framework;

namespace CustomSpawns.Tests.Invasions.CapitalObjectives
{
    public class CapitalArmyStagingRulesTests
    {
        private readonly CapitalArmyStagingRules _rules = new();

        [Test]
        public void StagingMustBeSafeDistinctAndReachable()
        {
            Assert.That(_rules.IsEligible(Candidate()), Is.True);
            Assert.That(_rules.IsEligible(Candidate(isActive: false)), Is.False);
            Assert.That(_rules.IsEligible(Candidate(isFortification: false)), Is.False);
            Assert.That(_rules.IsEligible(Candidate(isBesieged: true)), Is.False);
            Assert.That(_rules.IsEligible(Candidate(hasMapEvent: true)), Is.False);
            Assert.That(_rules.IsEligible(Candidate(isTarget: true)), Is.False);
            Assert.That(_rules.IsEligible(Candidate(isReachableFromCommander: false)), Is.False);
            Assert.That(_rules.IsEligible(Candidate(isReachableFromTarget: false)), Is.False);
        }

        [Test]
        public void StagingAcceptsCastleVillageCloseToCapital()
        {
            CapitalArmyStagingCandidate village = Candidate(isFortification: false,
                isCastleVillage: true, distanceFromTarget: 1f);

            Assert.That(_rules.IsEligible(village), Is.True);
        }

        [Test]
        public void StagingPrefersNearestCastleVillage()
        {
            CapitalArmyStagingCandidate? selected = _rules.FindStaging(new[]
            {
                Candidate(settlementId: "town", distanceFromTarget: 5f),
                Candidate(settlementId: "far-village", isFortification: false,
                    isCastleVillage: true, distanceFromTarget: 20f),
                Candidate(settlementId: "near-village", isFortification: false,
                    isCastleVillage: true, distanceFromTarget: 10f)
            });

            Assert.That(selected?.SettlementId, Is.EqualTo("near-village"));
        }

        [Test]
        public void StagingFallsBackToNearestTownOrCastleWhenVillagesAreUnavailable()
        {
            CapitalArmyStagingCandidate? selected = _rules.FindStaging(new[]
            {
                Candidate(settlementId: "raided-village", isFortification: false,
                    isCastleVillage: true, isBesieged: true, distanceFromTarget: 1f),
                Candidate(settlementId: "unreachable-village", isFortification: false,
                    isCastleVillage: true, isReachableFromCommander: false, distanceFromTarget: 2f),
                Candidate(settlementId: "town", distanceFromTarget: 20f),
                Candidate(settlementId: "castle", distanceFromTarget: 10f)
            });

            Assert.That(selected?.SettlementId, Is.EqualTo("castle"));
        }

        [Test]
        public void NoStagingWhenEveryCandidateIsUnavailable()
        {
            Assert.That(_rules.FindStaging(new[]
            {
                Candidate(isTarget: true), Candidate(isReachableFromTarget: false)
            }), Is.Null);
            Assert.That(_rules.FindStaging(System.Array.Empty<CapitalArmyStagingCandidate>()), Is.Null);
        }

        private static CapitalArmyStagingCandidate Candidate(bool isActive = true,
            bool isFortification = true,
            bool isBesieged = false, bool hasMapEvent = false, bool isTarget = false,
            bool isReachableFromCommander = true, bool isReachableFromTarget = true,
            bool isCastleVillage = false,
            float distanceFromTarget = 10f, string settlementId = "staging") =>
            new(settlementId, isActive, isFortification, isBesieged,
                hasMapEvent, isTarget, isReachableFromCommander, isReachableFromTarget,
                isCastleVillage, distanceFromTarget);
    }
}
