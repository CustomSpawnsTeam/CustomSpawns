using System.Collections.Generic;
using System.Linq;
using CustomSpawns.AI.Offensives;

namespace CustomSpawns.Invasions.CapitalObjectives
{
    /**
     * Chooses the fief that becomes an invasion's capital when it loses its own, for an invasion configured to
     * move its capital rather than retake it. Only a fief that could have been the invasion's first conquest
     * qualifies: one of its siege target kinds, and a coastal town for an invasion that must land on the coast.
     * The one nearest the lost capital is chosen.
     */
    public sealed class InvasionCapitalSuccession
    {
        public string? FindSuccessor(InvasionAssaultRequirements requirements,
            IEnumerable<CapitalSuccessorFacts> heldFiefs)
        {
            if (!requirements.RelocatesCapitalOnLoss)
            {
                return null;
            }

            return heldFiefs
                .Where(fief => requirements.TargetKinds.Contains(fief.Kind) &&
                               requirements.MatchesStrategicRequirement(fief.Kind == SiegeTargetKind.Town,
                                   fief.IsCoastalTown))
                .OrderBy(fief => fief.DistanceToLostCapital)
                .Select(fief => fief.SettlementId)
                .FirstOrDefault();
        }
    }
}
