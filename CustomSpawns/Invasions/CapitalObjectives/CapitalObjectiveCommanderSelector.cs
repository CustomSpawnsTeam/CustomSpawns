using System;
using System.Collections.Generic;
using System.Linq;

namespace CustomSpawns.Invasions.CapitalObjectives
{
    public readonly struct CapitalObjectiveCommanderCandidate
    {
        public CapitalObjectiveCommanderCandidate(string partyId, float strength, bool isEligible,
            bool leadsArmy, bool canLeadArmy)
        {
            if (string.IsNullOrWhiteSpace(partyId))
            {
                throw new ArgumentException("A party id is required.", nameof(partyId));
            }

            PartyId = partyId;
            Strength = strength;
            IsEligible = isEligible;
            LeadsArmy = leadsArmy;
            CanLeadArmy = canLeadArmy;
        }

        public string PartyId { get; }
        public float Strength { get; }
        public bool IsEligible { get; }
        public bool LeadsArmy { get; }
        public bool CanLeadArmy { get; }
    }

    public sealed class CapitalObjectiveCommanderSelector
    {
        public CapitalObjectiveCommanderCandidate? Find(
            IReadOnlyList<CapitalObjectiveCommanderCandidate> candidates)
        {
            CapitalObjectiveCommanderCandidate? armyLeader = Strongest(candidates,
                candidate => candidate.IsEligible && candidate.LeadsArmy);

            return armyLeader ?? Strongest(candidates,
                candidate => candidate.IsEligible && candidate.CanLeadArmy);
        }

        private static CapitalObjectiveCommanderCandidate? Strongest(
            IReadOnlyList<CapitalObjectiveCommanderCandidate> candidates,
            Func<CapitalObjectiveCommanderCandidate, bool> predicate)
        {
            return candidates.Where(predicate)
                .OrderByDescending(candidate => candidate.Strength)
                .ThenBy(candidate => candidate.PartyId, StringComparer.Ordinal)
                .Cast<CapitalObjectiveCommanderCandidate?>()
                .FirstOrDefault();
        }
    }
}
