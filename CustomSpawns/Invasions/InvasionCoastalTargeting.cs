using System.Collections.Generic;
using System.Linq;

namespace CustomSpawns.Invasions
{
    // Pure first-conquest rule shared by capture credit and siege targeting.
    public sealed class InvasionCoastalTargeting
    {
        private readonly IReadOnlyList<string> _coastalTownIds;

        public InvasionCoastalTargeting(bool hasFirstCoastalConquest, IReadOnlyList<string> coastalTownIds)
        {
            RequiresFirstCoastalConquest = !hasFirstCoastalConquest;
            _coastalTownIds = coastalTownIds ?? new string[0];
        }

        public bool RequiresFirstCoastalConquest { get; }

        public bool AllowsFirstConquestTarget(string settlementId, bool isTown, bool hasPort)
        {
            return !RequiresFirstCoastalConquest || IsCoastalTown(_coastalTownIds, settlementId, isTown, hasPort);
        }

        public static bool IsCoastalTown(IReadOnlyList<string> coastalTownIds, string settlementId,
            bool isTown, bool hasPort)
        {
            return isTown && (coastalTownIds.Count == 0 ? hasPort : coastalTownIds.Contains(settlementId));
        }
    }
}
