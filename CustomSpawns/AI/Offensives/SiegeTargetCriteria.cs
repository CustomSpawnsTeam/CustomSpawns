using System;
using System.Collections.Generic;

namespace CustomSpawns.AI.Offensives
{
    public enum SiegeTargetSelectionMode
    {
        GatherBeforeAssault = 0,
        RequireViableNow = 1
    }

    public readonly struct SiegeTargetCriteria
    {
        public SiegeTargetCriteria(IReadOnlyList<SiegeTargetKind> allowedKinds,
            string? excludedSettlementId, float attackerStrength, float minimumTargetRatio,
            float minimumRoamingRatio, SiegeTargetSelectionMode mode)
        {
            AllowedKinds = allowedKinds ?? throw new ArgumentNullException(nameof(allowedKinds));
            ExcludedSettlementId = excludedSettlementId;
            AttackerStrength = attackerStrength;
            MinimumTargetRatio = minimumTargetRatio;
            MinimumRoamingRatio = minimumRoamingRatio;
            Mode = mode;
        }

        public IReadOnlyList<SiegeTargetKind> AllowedKinds { get; }
        public string? ExcludedSettlementId { get; }
        public float AttackerStrength { get; }
        public float MinimumTargetRatio { get; }
        public float MinimumRoamingRatio { get; }
        public SiegeTargetSelectionMode Mode { get; }
    }
}
