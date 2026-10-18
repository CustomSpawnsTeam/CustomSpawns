using System;
using System.Collections.Generic;
using System.Linq;
using CustomSpawns.AI.Offensives;

namespace CustomSpawns.Invasions.CapitalObjectives
{
    public enum InvasionSupplyLineKind
    {
        Coastal = 0,
        Any = 1,
        None = 2
    }

    public sealed class InvasionAssaultRequirements
    {
        public InvasionAssaultRequirements(IReadOnlyList<SiegeTargetKind> targetKinds,
            float minimumTargetRatio, float minimumRoamingRatio, bool requiresOriginalCoastalTarget,
            InvasionSupplyLineKind supplyLine)
        {
            if (targetKinds == null || targetKinds.Count == 0)
            {
                throw new ArgumentException("At least one siege target kind is required.", nameof(targetKinds));
            }

            if (minimumTargetRatio < 0f || float.IsNaN(minimumTargetRatio) ||
                float.IsInfinity(minimumTargetRatio))
            {
                throw new ArgumentOutOfRangeException(nameof(minimumTargetRatio));
            }

            if (minimumRoamingRatio < 0f || float.IsNaN(minimumRoamingRatio) ||
                float.IsInfinity(minimumRoamingRatio))
            {
                throw new ArgumentOutOfRangeException(nameof(minimumRoamingRatio));
            }

            if (targetKinds.Any(kind => !Enum.IsDefined(typeof(SiegeTargetKind), kind)) ||
                !Enum.IsDefined(typeof(InvasionSupplyLineKind), supplyLine))
            {
                throw new ArgumentException("Invasion assault requirements contain an unknown enum value.");
            }

            TargetKinds = targetKinds.Distinct().ToArray();
            MinimumTargetRatio = minimumTargetRatio;
            MinimumRoamingRatio = minimumRoamingRatio;
            RequiresOriginalCoastalTarget = requiresOriginalCoastalTarget;
            SupplyLine = supplyLine;
        }

        public IReadOnlyList<SiegeTargetKind> TargetKinds { get; }
        public float MinimumTargetRatio { get; }
        public float MinimumRoamingRatio { get; }
        public bool RequiresOriginalCoastalTarget { get; }
        public InvasionSupplyLineKind SupplyLine { get; }

        public bool MatchesStrategicRequirement(bool isTown, bool isCoastalTown)
        {
            if (!RequiresOriginalCoastalTarget || SupplyLine == InvasionSupplyLineKind.None)
            {
                return true;
            }

            return SupplyLine == InvasionSupplyLineKind.Coastal
                ? isCoastalTown
                : isTown;
        }
    }
}
