using System;
using System.Collections.Generic;
using System.Linq;

namespace CustomSpawns.Invasions
{
    // The candidate nearest a place, the same one every time whatever order the campaign lists them in.
    public sealed class InvasionNearestChoice
    {
        public string? FindNearestId(IEnumerable<InvasionCandidateDistance> candidates) => candidates
            .OrderBy(candidate => candidate.Distance)
            .ThenBy(candidate => candidate.Id, StringComparer.Ordinal)
            .Select(candidate => candidate.Id)
            .FirstOrDefault();
    }

    public readonly struct InvasionCandidateDistance
    {
        public InvasionCandidateDistance(string id, float distance)
        {
            Id = id;
            Distance = distance;
        }

        public string Id { get; }
        public float Distance { get; }
    }
}
