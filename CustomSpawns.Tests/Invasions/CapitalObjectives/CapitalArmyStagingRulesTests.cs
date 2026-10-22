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

        private static CapitalArmyStagingCandidate Candidate(bool isActive = true,
            bool isFortification = true, bool isOwnedByKingdom = true,
            bool isBesieged = false, bool hasMapEvent = false, bool isTarget = false,
            bool isReachableFromCommander = true, bool isReachableFromTarget = true,
            bool isFarEnoughFromTarget = true) => new("staging", isActive, isFortification,
            isOwnedByKingdom, isBesieged, hasMapEvent, isTarget,
            isReachableFromCommander, isReachableFromTarget, isFarEnoughFromTarget);
    }
}
