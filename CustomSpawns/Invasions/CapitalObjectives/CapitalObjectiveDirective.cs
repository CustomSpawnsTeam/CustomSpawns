using System;
using System.Collections.Generic;

namespace CustomSpawns.Invasions.CapitalObjectives
{
    public readonly struct InvasionCapitalObjectiveDirective
    {
        public InvasionCapitalObjectiveDirective(string kingdomId, string commanderPartyId,
            string targetSettlementId, InvasionCapitalObjectiveKind kind,
            InvasionCapitalObjectivePhase phase)
            : this(kingdomId, commanderPartyId, targetSettlementId, kind, phase,
                Array.Empty<string>())
        {
        }

        public InvasionCapitalObjectiveDirective(string kingdomId, string commanderPartyId,
            string targetSettlementId, InvasionCapitalObjectiveKind kind,
            InvasionCapitalObjectivePhase phase, IReadOnlyList<string> partyIds)
        {
            KingdomId = kingdomId;
            CommanderPartyId = commanderPartyId;
            TargetSettlementId = targetSettlementId;
            Kind = kind;
            Phase = phase;
            PartyIds = partyIds ?? throw new ArgumentNullException(nameof(partyIds));
        }

        public string KingdomId { get; }
        public string CommanderPartyId { get; }
        public string TargetSettlementId { get; }
        public InvasionCapitalObjectiveKind Kind { get; }
        public InvasionCapitalObjectivePhase Phase { get; }
        public IReadOnlyList<string> PartyIds { get; }
    }

    public interface IInvasionCapitalObjectiveQuery
    {
        InvasionCapitalObjectiveDirective? FindForKingdom(string kingdomId);
    }
}
