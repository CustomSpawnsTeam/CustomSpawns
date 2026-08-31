using System.Collections.Generic;
using System.Linq;
using CustomSpawns.Invasions;
using NUnit.Framework;

namespace CustomSpawns.Tests.Invasions
{
    public class OccupationGarrisonAllocationTests
    {
        [Test]
        public void AdequateNativeGarrisonDoesNotReceiveAnotherContribution()
        {
            OccupationGarrisonAllocationResult result = Allocate(Snapshot(currentCount: 5, currentStrength: 12f,
                militia: 10f, minimum: 5, maximum: 8, ratio: 1f));

            Assert.That(result.Entries, Is.Empty);
            Assert.That(result.IsInsufficient, Is.False);
            Assert.That(result.FinalCount, Is.EqualTo(5));
            Assert.That(result.FinalStrength, Is.EqualTo(12f));
        }

        [Test]
        public void StrengthObjectiveCanRequireTroopsBeyondMinimumButCapacityBoundsThePlan()
        {
            OccupationGarrisonDonorSnapshot donor = Donor("capturer", 1f,
                Candidate("heavy", false, 2, 2, 0, 0, 10f));
            OccupationGarrisonAllocationResult result = Allocate(Snapshot(new[] { donor }, currentCount: 1,
                capacity: 3, minimum: 2, maximum: 5, militia: 15f, ratio: 1f,
                destination: Destination("heavy", 0, 0, 0)));

            Assert.That(result.Entries.Count, Is.EqualTo(2));
            Assert.That(result.FinalCount, Is.EqualTo(3));
            Assert.That(result.FinalStrength, Is.EqualTo(20f));
            Assert.That(result.IsInsufficient, Is.False);
        }

        [Test]
        public void DonorFractionAndNativeCapacityProduceConservativePartialPlan()
        {
            OccupationGarrisonDonorSnapshot donor = Donor("capturer", 0.5f,
                Candidate("first", false, 2, 2, 0, 0, 4f),
                Candidate("second", false, 2, 2, 0, 0, 3f));
            OccupationGarrisonAllocationResult result = Allocate(Snapshot(new[] { donor }, currentCount: 0,
                capacity: 3, minimum: 5, maximum: 8, destination: new[]
                {
                    Destination("first", 0, 0, 0), Destination("second", 0, 0, 0)
                }));

            Assert.That(result.SourceLoss, Is.EqualTo(2));
            Assert.That(result.DestinationGain, Is.EqualTo(2));
            Assert.That(result.FinalCount, Is.EqualTo(2));
            Assert.That(result.IsInsufficient, Is.True);
        }

        [Test]
        public void RangedAndNonRangedCandidatesAlternateDeterministically()
        {
            OccupationGarrisonDonorSnapshot r1 = Donor("r1", 1f, Candidate("r1", true, 1, 1, 0, 0, 9f));
            OccupationGarrisonDonorSnapshot r2 = Donor("r2", 1f, Candidate("r2", true, 1, 1, 0, 0, 8f));
            OccupationGarrisonDonorSnapshot m1 = Donor("m1", 1f, Candidate("m1", false, 1, 1, 0, 0, 7f));
            OccupationGarrisonDonorSnapshot m2 = Donor("m2", 1f, Candidate("m2", false, 1, 1, 0, 0, 6f));

            OccupationGarrisonAllocationResult result = Allocate(Snapshot(new[] { r1, r2, m1, m2 }, minimum: 4,
                capacity: 4, maximum: 4, destination: new[]
                {
                    Destination("r1", 0, 0, 0), Destination("r2", 0, 0, 0),
                    Destination("m1", 0, 0, 0), Destination("m2", 0, 0, 0)
                }));

            Assert.That(result.Entries.Select(entry => entry.CharacterId), Is.EqualTo(new[] { "r1", "m1", "r2", "m2" }));
        }

        [Test]
        public void NonparticipantHeroAndWoundedOnlyStacksCannotContribute()
        {
            OccupationGarrisonDonorSnapshot participant = Donor("capturer", 1f,
                Candidate("hero", false, 2, 2, 0, 0, 10f, isHero: true),
                Candidate("wounded", false, 0, 2, 0, 0, 9f));
            OccupationGarrisonDonorSnapshot otherGroup = Donor("other", 1f,
                Candidate("foreign", false, 3, 3, 0, 0, 8f));

            OccupationGarrisonAllocationResult result = Allocate(Snapshot(new[] { participant, otherGroup }, minimum: 1,
                capacity: 3, maximum: 3, participants: new[] { "capturer" }, destination: new[]
                {
                    Destination("hero", 0, 0, 0), Destination("wounded", 0, 0, 0),
                    Destination("foreign", 0, 0, 0)
                }));

            Assert.That(result.Entries, Is.Empty);
            Assert.That(result.IsInsufficient, Is.True);
        }

        [Test]
        public void XpPreflightPreservesSourceAndDestinationCapsAndTotalXp()
        {
            OccupationGarrisonDonorSnapshot donor = Donor("capturer", 1f,
                Candidate("veteran", false, 2, 2, 20, 10, 2f));
            OccupationGarrisonAllocationResult result = Allocate(Snapshot(new[] { donor }, minimum: 1, capacity: 1,
                maximum: 1, destination: Destination("veteran", 0, 0, 10)));
            OccupationGarrisonTransferEntry entry = result.Entries.Single();

            Assert.That(entry.XpToMove, Is.EqualTo(10));
            Assert.That(entry.SourceCountAfter, Is.EqualTo(1));
            Assert.That(entry.SourceXpAfter, Is.EqualTo(10));
            Assert.That(entry.DestinationCountAfter, Is.EqualTo(1));
            Assert.That(entry.DestinationXpAfter, Is.EqualTo(10));
            Assert.That(entry.SourceXpAfter + entry.DestinationXpAfter, Is.EqualTo(20));
        }

        [Test]
        public void DestinationXpCapCanReduceShareWithoutLosingXp()
        {
            OccupationGarrisonDonorSnapshot donor = Donor("capturer", 1f,
                Candidate("novice", false, 2, 2, 2, 10, 2f));
            OccupationGarrisonAllocationResult result = Allocate(Snapshot(new[] { donor }, minimum: 1, capacity: 1,
                maximum: 1, destination: Destination("novice", 0, 0, 0)));
            OccupationGarrisonTransferEntry entry = result.Entries.Single();

            Assert.That(entry.XpToMove, Is.Zero);
            Assert.That(entry.SourceXpAfter + entry.DestinationXpAfter, Is.EqualTo(2));
            Assert.That(entry.DestinationXpAfter, Is.EqualTo(0));
        }

        [Test]
        public void EmptyXpIntervalExcludesCandidateBeforeAnyTransfer()
        {
            OccupationGarrisonDonorSnapshot donor = Donor("capturer", 1f,
                Candidate("blocked", false, 2, 2, 20, 10, 2f));
            OccupationGarrisonAllocationResult result = Allocate(Snapshot(new[] { donor }, minimum: 1, capacity: 1,
                maximum: 1, destination: Destination("blocked", 0, 0, 2)));

            Assert.That(result.Entries, Is.Empty);
            Assert.That(result.IsInsufficient, Is.True);
        }

        [Test]
        public void SequentialDonorsOfSameCharacterUseVirtualDestinationXpAndCount()
        {
            OccupationGarrisonDonorSnapshot first = Donor("first", 1f,
                Candidate("shared", false, 1, 1, 10, 10, 2f));
            OccupationGarrisonDonorSnapshot second = Donor("second", 1f,
                Candidate("shared", false, 1, 1, 10, 10, 2f));
            OccupationGarrisonAllocationResult result = Allocate(Snapshot(new[] { first, second }, minimum: 2,
                capacity: 2, maximum: 2, destination: Destination("shared", 0, 0, 10)));

            Assert.That(result.Entries.Count, Is.EqualTo(2));
            Assert.That(result.Entries[0].DestinationCountAfter, Is.EqualTo(1));
            Assert.That(result.Entries[0].DestinationXpAfter, Is.EqualTo(10));
            Assert.That(result.Entries[1].DestinationCountAfter, Is.EqualTo(2));
            Assert.That(result.Entries[1].DestinationXpAfter, Is.EqualTo(20));
            Assert.That(result.Entries.Sum(entry => entry.XpToMove), Is.EqualTo(20));
        }

        private static OccupationGarrisonAllocationResult Allocate(OccupationGarrisonAllocationSnapshot snapshot) =>
            new OccupationGarrisonAllocation().Allocate(snapshot);

        private static OccupationGarrisonAllocationSnapshot Snapshot(
            IReadOnlyList<OccupationGarrisonDonorSnapshot>? donors = null, int currentCount = 0, int capacity = 10,
            float currentStrength = 0f, float militia = 0f, int minimum = 1, int maximum = 10, float ratio = 0f,
            IReadOnlyCollection<string>? participants = null,
            params OccupationGarrisonDestinationStackSnapshot[] destination)
        {
            donors ??= new List<OccupationGarrisonDonorSnapshot>();
            participants ??= donors.Select(donor => donor.DonorId).ToList();
            return new OccupationGarrisonAllocationSnapshot(participants, donors, destination, currentCount, capacity,
                currentStrength, militia, minimum, maximum, ratio);
        }

        private static OccupationGarrisonDonorSnapshot Donor(string id, float maximumFraction,
            params OccupationGarrisonCandidateSnapshot[] candidates) =>
            new(id, candidates.Where(candidate => !candidate.IsHero).Sum(candidate => candidate.HealthyCount),
                maximumFraction, candidates);

        private static OccupationGarrisonCandidateSnapshot Candidate(string id, bool ranged, int healthy, int count,
            int xp, int sourceCap, float power, bool isHero = false) =>
            new(id, isHero, ranged, healthy, count, xp, power, sourceCap);

        private static OccupationGarrisonDestinationStackSnapshot Destination(string id, int count, int xp, int cap) =>
            new(id, count, xp, cap);
    }
}
