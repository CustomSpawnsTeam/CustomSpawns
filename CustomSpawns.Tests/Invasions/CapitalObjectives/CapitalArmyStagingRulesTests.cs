using CustomSpawns.Invasions.CapitalObjectives;
using NUnit.Framework;

namespace CustomSpawns.Tests.Invasions.CapitalObjectives
{
    public class CapitalArmyStagingRulesTests
    {
        private readonly CapitalArmyStagingRules _rules = new();

        [Test]
        public void StagingMustBeFriendlySafeDistinctAndReachable()
        {
            Assert.That(_rules.IsEligible(Candidate()), Is.True);
            Assert.That(_rules.IsEligible(Candidate(isActive: false)), Is.False);
            Assert.That(_rules.IsEligible(Candidate(isFortification: false)), Is.False);
            Assert.That(_rules.IsEligible(Candidate(isOwnedByKingdom: false)), Is.False);
            Assert.That(_rules.IsEligible(Candidate(isBesieged: true)), Is.False);
            Assert.That(_rules.IsEligible(Candidate(hasMapEvent: true)), Is.False);
            Assert.That(_rules.IsEligible(Candidate(isTarget: true)), Is.False);
            Assert.That(_rules.IsEligible(Candidate(isReachableFromCommander: false)), Is.False);
            Assert.That(_rules.IsEligible(Candidate(isReachableFromTarget: false)), Is.False);
            Assert.That(_rules.IsEligible(Candidate(isFarEnoughFromTarget: false)), Is.False);
        }

        [Test]
        public void DefenceAcceptsUnownedCastleVillageCloseToCapital()
        {
            CapitalArmyStagingCandidate village = Candidate(isFortification: false,
                isOwnedByKingdom: false, isFarEnoughFromTarget: false, isCastleVillage: true);

            Assert.That(_rules.IsEligibleForDefence(village), Is.True);
            Assert.That(_rules.IsEligible(village), Is.False);
        }

        [Test]
        public void DefenceStillRequiresSafeDistinctReachableStaging()
        {
            Assert.That(_rules.IsEligibleForDefence(Candidate(isActive: false)), Is.False);
            Assert.That(_rules.IsEligibleForDefence(Candidate(isFortification: false)), Is.False);
            Assert.That(_rules.IsEligibleForDefence(Candidate(isBesieged: true)), Is.False);
            Assert.That(_rules.IsEligibleForDefence(Candidate(hasMapEvent: true)), Is.False);
            Assert.That(_rules.IsEligibleForDefence(Candidate(isTarget: true)), Is.False);
            Assert.That(_rules.IsEligibleForDefence(Candidate(isReachableFromCommander: false)), Is.False);
            Assert.That(_rules.IsEligibleForDefence(Candidate(isReachableFromTarget: false)), Is.False);
        }

        [Test]
        public void DefencePrefersNearestCastleVillageRegardlessOfOwnership()
        {
            CapitalArmyStagingCandidate? selected = _rules.FindDefenceStaging(new[]
            {
                Candidate(settlementId: "town", distanceFromTarget: 5f),
                Candidate(settlementId: "owned-village", isFortification: false,
                    isCastleVillage: true, distanceFromTarget: 20f),
                Candidate(settlementId: "enemy-village", isFortification: false,
                    isOwnedByKingdom: false, isCastleVillage: true, distanceFromTarget: 10f)
            });

            Assert.That(selected?.SettlementId, Is.EqualTo("enemy-village"));
        }

        [Test]
        public void DefenceFallsBackToNearestTownOrCastleWhenVillagesAreUnavailable()
        {
            CapitalArmyStagingCandidate? selected = _rules.FindDefenceStaging(new[]
            {
                Candidate(settlementId: "raided-village", isFortification: false,
                    isCastleVillage: true, isBesieged: true, distanceFromTarget: 1f),
                Candidate(settlementId: "unreachable-village", isFortification: false,
                    isCastleVillage: true, isReachableFromCommander: false, distanceFromTarget: 2f),
                Candidate(settlementId: "owned-town", distanceFromTarget: 20f),
                Candidate(settlementId: "enemy-castle", isOwnedByKingdom: false, distanceFromTarget: 10f)
            });

            Assert.That(selected?.SettlementId, Is.EqualTo("enemy-castle"));
        }

        [Test]
        public void DefenceHasNoStagingWhenEveryCandidateIsUnavailable()
        {
            Assert.That(_rules.FindDefenceStaging(new[]
            {
                Candidate(isTarget: true), Candidate(isReachableFromTarget: false)
            }), Is.Null);
            Assert.That(_rules.FindDefenceStaging(System.Array.Empty<CapitalArmyStagingCandidate>()), Is.Null);
        }

        private static CapitalArmyStagingCandidate Candidate(bool isActive = true,
            bool isFortification = true, bool isOwnedByKingdom = true,
            bool isBesieged = false, bool hasMapEvent = false, bool isTarget = false,
            bool isReachableFromCommander = true, bool isReachableFromTarget = true,
            bool isFarEnoughFromTarget = true, bool isCastleVillage = false,
            float distanceFromTarget = 10f, string settlementId = "staging") =>
            new(settlementId, isActive, isFortification, isOwnedByKingdom, isBesieged,
                hasMapEvent, isTarget, isReachableFromCommander, isReachableFromTarget,
                isFarEnoughFromTarget, isCastleVillage, distanceFromTarget);
    }
}
