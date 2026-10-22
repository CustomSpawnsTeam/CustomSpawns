using System;
using System.Linq;

namespace CustomSpawns.Invasions.CapitalObjectives
{
    /**
     * Decides whether native army AI owns a party while a capital objective is active. The persisted
     * roster identifies the army the objective created; the live army leader link proves the party is
     * still in that army, so a released invitation or a broken army never remains without steering.
     */
    public sealed class InvasionCapitalObjectiveSteeringPolicy
    {
        public bool IsReleasedToNative(InvasionCapitalObjective? objective, string partyId,
            string? nativeArmyCommanderPartyId)
        {
            if (objective == null || !IsNativeArmyPhase(objective.Phase))
            {
                return false;
            }

            bool belongsToObjectiveRoster = objective.MobilizedPartyIds.Contains(partyId,
                StringComparer.Ordinal);
            bool belongsToObjectiveArmy = string.Equals(nativeArmyCommanderPartyId,
                objective.CommanderPartyId, StringComparison.Ordinal);
            return belongsToObjectiveRoster && belongsToObjectiveArmy;
        }

        private static bool IsNativeArmyPhase(InvasionCapitalObjectivePhase phase)
        {
            return phase == InvasionCapitalObjectivePhase.Gathering ||
                   phase == InvasionCapitalObjectivePhase.Advancing ||
                   phase == InvasionCapitalObjectivePhase.Besieging;
        }
    }
}
