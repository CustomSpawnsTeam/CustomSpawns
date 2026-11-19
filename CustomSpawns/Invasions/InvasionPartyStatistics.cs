using System.Collections.Generic;
using System.Linq;

namespace CustomSpawns.Invasions
{
    public readonly struct InvasionPartyStatistics
    {
        public InvasionPartyStatistics(IEnumerable<(int Men, int Healthy, int Limit, float Strength)> parties)
        {
            var snapshots = parties.ToList();
            Count = snapshots.Count;
            Men = snapshots.Sum(party => party.Men);
            Healthy = snapshots.Sum(party => party.Healthy);
            TotalLimit = snapshots.Sum(party => party.Limit);
            Strength = snapshots.Sum(party => (double)party.Strength);
            MinimumSize = Count == 0 ? 0 : snapshots.Min(party => party.Men);
            MaximumSize = Count == 0 ? 0 : snapshots.Max(party => party.Men);
        }

        public int Count { get; }
        public int Men { get; }
        public int Healthy { get; }
        public int TotalLimit { get; }
        public double Strength { get; }
        public int MinimumSize { get; }
        public int MaximumSize { get; }
        public double AverageSize => Count == 0 ? 0 : (double)Men / Count;
        public double AverageLimit => Count == 0 ? 0 : (double)TotalLimit / Count;
        public double FillPercent => TotalLimit == 0 ? 0 : 100d * Men / TotalLimit;
    }
}
