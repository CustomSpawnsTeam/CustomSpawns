using System;
using System.Collections.Generic;
using System.Linq;

namespace CustomSpawns.Invasions
{
    internal sealed class OccupationGarrisonAllocation
    {
        internal OccupationGarrisonAllocationResult Allocate(OccupationGarrisonAllocationSnapshot snapshot)
        {
            int countCeiling = Math.Min(snapshot.MaximumTroopCount, Math.Max(0, snapshot.ActualCapacity));
            int room = Math.Max(0, countCeiling - snapshot.CurrentGarrisonCount);
            float strengthObjective = snapshot.MilitiaStrength * snapshot.MilitiaStrengthRatio;
            if (MeetsObjectives(snapshot.CurrentGarrisonCount, snapshot.CurrentGarrisonStrength,
                snapshot.MinimumTroopCount, strengthObjective))
            {
                return new OccupationGarrisonAllocationResult(Array.Empty<OccupationGarrisonTransferEntry>(), false,
                    snapshot.CurrentGarrisonCount, snapshot.CurrentGarrisonStrength);
            }

            var destination = snapshot.DestinationStacks.ToDictionary(stack => stack.CharacterId,
                stack => new MutableDestinationStack(stack), StringComparer.Ordinal);
            var donors = snapshot.Donors.Where(donor => snapshot.ParticipantPartyIds.Contains(donor.DonorId))
                .Select(donor => new MutableDonor(donor)).ToList();
            var candidates = donors.SelectMany(donor => donor.Snapshot.Candidates
                    .Select(candidate => new MutableCandidate(donor, candidate)))
                .Where(candidate => candidate.IsEligible)
                .ToList();
            var ranged = candidates.Where(candidate => candidate.Candidate.IsRanged).OrderByDescending(candidate => candidate.Candidate.DefenderPower)
                .ThenBy(candidate => candidate.Candidate.CharacterId, StringComparer.Ordinal)
                .ThenBy(candidate => candidate.Donor.Snapshot.DonorId, StringComparer.Ordinal).ToList();
            var nonRanged = candidates.Where(candidate => !candidate.Candidate.IsRanged).OrderByDescending(candidate => candidate.Candidate.DefenderPower)
                .ThenBy(candidate => candidate.Candidate.CharacterId, StringComparer.Ordinal)
                .ThenBy(candidate => candidate.Donor.Snapshot.DonorId, StringComparer.Ordinal).ToList();
            var entries = new List<OccupationGarrisonTransferEntry>();
            float strength = snapshot.CurrentGarrisonStrength;
            int count = snapshot.CurrentGarrisonCount;
            bool preferRanged = true;

            while (room > 0 && !MeetsObjectives(count, strength, snapshot.MinimumTroopCount, strengthObjective))
            {
                MutableCandidate? selected = TryTake(preferRanged ? ranged : nonRanged, destination);
                if (selected == null)
                {
                    selected = TryTake(preferRanged ? nonRanged : ranged, destination);
                }
                if (selected == null) break;

                OccupationGarrisonTransferEntry entry = selected.LastTransfer!;
                entries.Add(entry);
                count++;
                room--;
                strength += selected.Candidate.DefenderPower;
                preferRanged = !preferRanged;
            }

            bool insufficient = !MeetsObjectives(count, strength, snapshot.MinimumTroopCount, strengthObjective);
            return new OccupationGarrisonAllocationResult(entries, insufficient, count, strength);
        }

        private static bool MeetsObjectives(int count, float strength, int minimumTroopCount, float strengthObjective)
        {
            return count >= minimumTroopCount && strength >= strengthObjective;
        }

        private static MutableCandidate? TryTake(IEnumerable<MutableCandidate> candidates,
            IReadOnlyDictionary<string, MutableDestinationStack> destination)
        {
            foreach (MutableCandidate candidate in candidates)
            {
                if (!candidate.TryTake(destination)) continue;
                return candidate;
            }
            return null;
        }

        private sealed class MutableCandidate
        {
            private int _healthy;
            private int _count;
            private int _xp;

            public MutableCandidate(MutableDonor donor, OccupationGarrisonCandidateSnapshot candidate)
            {
                Donor = donor;
                Candidate = candidate;
                _healthy = candidate.HealthyCount;
                _count = candidate.StackCount;
                _xp = candidate.StackXp;
            }

            public MutableDonor Donor { get; }
            public OccupationGarrisonCandidateSnapshot Candidate { get; }
            public OccupationGarrisonTransferEntry? LastTransfer { get; private set; }

            public bool IsEligible => !Candidate.IsHero && Candidate.HealthyCount > 0 && Candidate.StackCount >= Candidate.HealthyCount &&
                                      Candidate.StackXp >= 0 && Candidate.SourceUpgradeXpCap >= 0 &&
                                      IsFinite(Candidate.DefenderPower) && Donor.RemainingBudget > 0;

            public bool TryTake(IReadOnlyDictionary<string, MutableDestinationStack> destination)
            {
                if (!IsEligible || _healthy == 0 || _count == 0 || Donor.RemainingBudget == 0 ||
                    !destination.TryGetValue(Candidate.CharacterId, out MutableDestinationStack? target)) return false;
                if (_xp > (long)_count * Candidate.SourceUpgradeXpCap || target.Xp > (long)target.Count * target.UpgradeXpCap) return false;

                long lower = Math.Max(0L, (long)_xp - (long)(_count - 1) * Candidate.SourceUpgradeXpCap);
                long upper = Math.Min(_xp, (long)(target.Count + 1) * target.UpgradeXpCap - target.Xp);
                if (lower > upper || upper > int.MaxValue) return false;
                long proportional = _xp / _count;
                int xpToMove = (int)Math.Max(lower, Math.Min(proportional, upper));

                _healthy--;
                _count--;
                _xp -= xpToMove;
                Donor.RemainingBudget--;
                target.Count++;
                target.Xp += xpToMove;
                LastTransfer = new OccupationGarrisonTransferEntry(Donor.Snapshot.DonorId, Candidate.CharacterId, 1, xpToMove,
                    _count, _xp, target.Count, target.Xp);
                return true;
            }

            private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private sealed class MutableDonor
        {
            public MutableDonor(OccupationGarrisonDonorSnapshot snapshot)
            {
                Snapshot = snapshot;
                RemainingBudget = snapshot.MaximumLoss;
            }

            public OccupationGarrisonDonorSnapshot Snapshot { get; }
            public int RemainingBudget { get; set; }
        }

        private sealed class MutableDestinationStack
        {
            public MutableDestinationStack(OccupationGarrisonDestinationStackSnapshot snapshot)
            {
                Count = snapshot.Count;
                Xp = snapshot.Xp;
                UpgradeXpCap = snapshot.UpgradeXpCap;
            }

            public int Count { get; set; }
            public int Xp { get; set; }
            public int UpgradeXpCap { get; }
        }
    }

    internal sealed class OccupationGarrisonAllocationSnapshot
    {
        public OccupationGarrisonAllocationSnapshot(IReadOnlyCollection<string> participantPartyIds,
            IReadOnlyList<OccupationGarrisonDonorSnapshot> donors,
            IReadOnlyList<OccupationGarrisonDestinationStackSnapshot> destinationStacks,
            int currentGarrisonCount, int actualCapacity, float currentGarrisonStrength, float militiaStrength,
            int minimumTroopCount, int maximumTroopCount, float militiaStrengthRatio)
        {
            ParticipantPartyIds = new HashSet<string>(participantPartyIds, StringComparer.Ordinal);
            Donors = donors;
            DestinationStacks = destinationStacks;
            CurrentGarrisonCount = currentGarrisonCount;
            ActualCapacity = actualCapacity;
            CurrentGarrisonStrength = currentGarrisonStrength;
            MilitiaStrength = militiaStrength;
            MinimumTroopCount = minimumTroopCount;
            MaximumTroopCount = maximumTroopCount;
            MilitiaStrengthRatio = militiaStrengthRatio;
        }

        public IReadOnlyCollection<string> ParticipantPartyIds { get; }
        public IReadOnlyList<OccupationGarrisonDonorSnapshot> Donors { get; }
        public IReadOnlyList<OccupationGarrisonDestinationStackSnapshot> DestinationStacks { get; }
        public int CurrentGarrisonCount { get; }
        public int ActualCapacity { get; }
        public float CurrentGarrisonStrength { get; }
        public float MilitiaStrength { get; }
        public int MinimumTroopCount { get; }
        public int MaximumTroopCount { get; }
        public float MilitiaStrengthRatio { get; }
    }

    internal sealed class OccupationGarrisonDonorSnapshot
    {
        public OccupationGarrisonDonorSnapshot(string donorId, int healthyTotal, float maximumDonorLossFraction,
            IReadOnlyList<OccupationGarrisonCandidateSnapshot> candidates)
        {
            DonorId = donorId;
            HealthyTotal = healthyTotal;
            MaximumLoss = Math.Max(0, (int)Math.Floor(healthyTotal * (double)maximumDonorLossFraction));
            Candidates = candidates;
        }

        public string DonorId { get; }
        public int HealthyTotal { get; }
        public int MaximumLoss { get; }
        public IReadOnlyList<OccupationGarrisonCandidateSnapshot> Candidates { get; }
    }

    internal sealed class OccupationGarrisonCandidateSnapshot
    {
        public OccupationGarrisonCandidateSnapshot(string characterId, bool isHero, bool isRanged, int healthyCount,
            int stackCount, int stackXp, float defenderPower, int sourceUpgradeXpCap)
        {
            CharacterId = characterId;
            IsHero = isHero;
            IsRanged = isRanged;
            HealthyCount = healthyCount;
            StackCount = stackCount;
            StackXp = stackXp;
            DefenderPower = defenderPower;
            SourceUpgradeXpCap = sourceUpgradeXpCap;
        }

        public string CharacterId { get; }
        public bool IsHero { get; }
        public bool IsRanged { get; }
        public int HealthyCount { get; }
        public int StackCount { get; }
        public int StackXp { get; }
        public float DefenderPower { get; }
        public int SourceUpgradeXpCap { get; }
    }

    internal sealed class OccupationGarrisonDestinationStackSnapshot
    {
        public OccupationGarrisonDestinationStackSnapshot(string characterId, int count, int xp, int upgradeXpCap)
        {
            CharacterId = characterId;
            Count = count;
            Xp = xp;
            UpgradeXpCap = upgradeXpCap;
        }

        public string CharacterId { get; }
        public int Count { get; }
        public int Xp { get; }
        public int UpgradeXpCap { get; }
    }

    internal sealed class OccupationGarrisonTransferEntry
    {
        public OccupationGarrisonTransferEntry(string donorId, string characterId, int count, int xpToMove,
            int sourceCountAfter, int sourceXpAfter, int destinationCountAfter, int destinationXpAfter)
        {
            DonorId = donorId;
            CharacterId = characterId;
            Count = count;
            XpToMove = xpToMove;
            SourceCountAfter = sourceCountAfter;
            SourceXpAfter = sourceXpAfter;
            DestinationCountAfter = destinationCountAfter;
            DestinationXpAfter = destinationXpAfter;
        }

        public string DonorId { get; }
        public string CharacterId { get; }
        public int Count { get; }
        public int XpToMove { get; }
        public int SourceCountAfter { get; }
        public int SourceXpAfter { get; }
        public int DestinationCountAfter { get; }
        public int DestinationXpAfter { get; }
    }

    internal sealed class OccupationGarrisonAllocationResult
    {
        public OccupationGarrisonAllocationResult(IReadOnlyList<OccupationGarrisonTransferEntry> entries,
            bool isInsufficient, int finalCount, float finalStrength)
        {
            Entries = entries;
            IsInsufficient = isInsufficient;
            FinalCount = finalCount;
            FinalStrength = finalStrength;
        }

        public IReadOnlyList<OccupationGarrisonTransferEntry> Entries { get; }
        public bool IsInsufficient { get; }
        public int FinalCount { get; }
        public float FinalStrength { get; }
        public int SourceLoss => Entries.Sum(entry => entry.Count);
        public int DestinationGain => Entries.Sum(entry => entry.Count);
        public int XpMoved => Entries.Sum(entry => entry.XpToMove);

    }
}
