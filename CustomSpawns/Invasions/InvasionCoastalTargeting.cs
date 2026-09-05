using System;

namespace CustomSpawns.Invasions
{
    // Pure first-conquest rule shared by capture credit and siege targeting.
    public sealed class InvasionCoastalTargeting
    {
        private readonly Func<string, bool>? _isCoastalTown;

        public InvasionCoastalTargeting(bool hasFirstCoastalConquest, Func<string, bool>? isCoastalTown = null)
        {
            RequiresFirstCoastalConquest = !hasFirstCoastalConquest;
            _isCoastalTown = isCoastalTown;
        }

        public bool RequiresFirstCoastalConquest { get; }

        public bool AllowsFirstConquestTarget(string settlementId, bool isTown, bool hasPort)
        {
            return !RequiresFirstCoastalConquest || (_isCoastalTown?.Invoke(settlementId) ??
                IsCoastalTown(isTown, hasPort));
        }

        public bool AllowsHeldTarget(string settlementId, bool isTown, bool hasPort,
            bool matchingNativeAssault, bool hasReachableLandGate)
        {
            return !RequiresFirstCoastalConquest || matchingNativeAssault ||
                   (AllowsFirstConquestTarget(settlementId, isTown, hasPort) && hasReachableLandGate);
        }

        public static bool IsCoastalTown(bool isTown, bool hasPort, bool hasNearbyMaritimeCoast = false)
        {
            return InvasionCoastalTownClassification.IsCoastalTown(isTown, hasPort, hasNearbyMaritimeCoast);
        }
    }
}
