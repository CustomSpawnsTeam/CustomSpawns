using System;
using System.Collections.Generic;
using System.Linq;

namespace CustomSpawns.Invasions.CapitalObjectives
{
    public readonly struct CapitalArmyPartyCandidate
    {
        public CapitalArmyPartyCandidate(string partyId, float strength, bool isMobilizable,
            bool leadsArmy, bool canLeadArmy, string? deferredReason = null)
        {
            if (string.IsNullOrWhiteSpace(partyId))
            {
                throw new ArgumentException("A party id is required.", nameof(partyId));
            }

            if (float.IsNaN(strength) || float.IsInfinity(strength) || strength < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(strength));
            }

            if (isMobilizable && !string.IsNullOrWhiteSpace(deferredReason) ||
                !isMobilizable && string.IsNullOrWhiteSpace(deferredReason))
            {
                throw new ArgumentException(
                    "A deferred reason is required only for a party that cannot mobilize.",
                    nameof(deferredReason));
            }

            PartyId = partyId;
            Strength = strength;
            IsMobilizable = isMobilizable;
            LeadsArmy = leadsArmy;
            CanLeadArmy = canLeadArmy;
            DeferredReason = deferredReason;
        }

        public string PartyId { get; }
        public float Strength { get; }
        public bool IsMobilizable { get; }
        public bool LeadsArmy { get; }
        public bool CanLeadArmy { get; }
        public string? DeferredReason { get; }
    }

    public readonly struct CapitalArmyPartyDeferral
    {
        public CapitalArmyPartyDeferral(string partyId, string reason)
        {
            if (string.IsNullOrWhiteSpace(partyId))
            {
                throw new ArgumentException("A party id is required.", nameof(partyId));
            }

            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new ArgumentException("A deferral reason is required.", nameof(reason));
            }

            PartyId = partyId;
            Reason = reason;
        }

        public string PartyId { get; }
        public string Reason { get; }
    }

    public sealed class CapitalArmyMobilizationPlan
    {
        public CapitalArmyMobilizationPlan(string invasionId, string commanderPartyId,
            IReadOnlyList<string> partyIds, InvasionCapitalObjectiveKind objectiveKind,
            string targetSettlementId, IReadOnlyList<CapitalArmyPartyDeferral> deferredParties)
        {
            InvasionId = RequiredId(invasionId, nameof(invasionId));
            CommanderPartyId = RequiredId(commanderPartyId, nameof(commanderPartyId));
            TargetSettlementId = RequiredId(targetSettlementId, nameof(targetSettlementId));
            PartyIds = RequiredPartyIds(partyIds, CommanderPartyId);

            if (!Enum.IsDefined(typeof(InvasionCapitalObjectiveKind), objectiveKind))
            {
                throw new ArgumentException("An objective kind is required.",
                    nameof(objectiveKind));
            }

            ObjectiveKind = objectiveKind;
            DeferredParties = deferredParties?.ToArray() ??
                              throw new ArgumentNullException(nameof(deferredParties));
        }

        public string InvasionId { get; }
        public string CommanderPartyId { get; }
        public IReadOnlyList<string> PartyIds { get; }
        public InvasionCapitalObjectiveKind ObjectiveKind { get; }
        public string TargetSettlementId { get; }
        public IReadOnlyList<CapitalArmyPartyDeferral> DeferredParties { get; }

        private static IReadOnlyList<string> RequiredPartyIds(IReadOnlyList<string> partyIds,
            string commanderPartyId)
        {
            if (partyIds == null)
            {
                throw new ArgumentNullException(nameof(partyIds));
            }

            string[] copy = partyIds.ToArray();
            if (copy.Any(string.IsNullOrWhiteSpace) ||
                copy.Distinct(StringComparer.Ordinal).Count() != copy.Length ||
                !copy.Contains(commanderPartyId, StringComparer.Ordinal))
            {
                throw new ArgumentException(
                    "Mobilized party ids must be unique, non-empty, and include the commander.",
                    nameof(partyIds));
            }

            return copy;
        }

        private static string RequiredId(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("An id is required.", parameterName);
            }

            return value;
        }
    }

    public sealed class CapitalArmyMobilizationResult
    {
        public CapitalArmyMobilizationResult(bool succeeded, string? commanderPartyId,
            IReadOnlyList<string>? partyIds, string? stagingSettlementId,
            IReadOnlyList<CapitalArmyPartyDeferral>? deferredParties = null,
            string? failureReason = null)
        {
            Succeeded = succeeded;
            CommanderPartyId = EmptyAsNull(commanderPartyId);
            PartyIds = partyIds?.ToArray() ?? Array.Empty<string>();
            StagingSettlementId = EmptyAsNull(stagingSettlementId);
            DeferredParties = deferredParties?.ToArray() ?? Array.Empty<CapitalArmyPartyDeferral>();
            FailureReason = EmptyAsNull(failureReason);

            Validate();
        }

        public bool Succeeded { get; }
        public string? CommanderPartyId { get; }
        public IReadOnlyList<string> PartyIds { get; }
        public string? StagingSettlementId { get; }
        public IReadOnlyList<CapitalArmyPartyDeferral> DeferredParties { get; }
        public string? FailureReason { get; }

        private void Validate()
        {
            bool validRoster = PartyIds.All(id => !string.IsNullOrWhiteSpace(id)) &&
                               PartyIds.Distinct(StringComparer.Ordinal).Count() == PartyIds.Count;
            bool validSuccess = CommanderPartyId != null && StagingSettlementId != null &&
                                validRoster && PartyIds.Contains(CommanderPartyId,
                                    StringComparer.Ordinal) && FailureReason == null;
            bool validFailure = CommanderPartyId == null && StagingSettlementId == null &&
                                PartyIds.Count == 0 && FailureReason != null;

            if (Succeeded ? !validSuccess : !validFailure)
            {
                throw new ArgumentException("The mobilization result is inconsistent.");
            }
        }

        private static string? EmptyAsNull(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value;
    }

    public readonly struct CapitalArmyStagingCandidate
    {
        public CapitalArmyStagingCandidate(string settlementId, bool isActive,
            bool isFortification, bool isOwnedByKingdom, bool isBesieged,
            bool hasMapEvent, bool isTarget, bool isReachableFromCommander,
            bool isReachableFromTarget, bool isFarEnoughFromTarget)
        {
            SettlementId = string.IsNullOrWhiteSpace(settlementId)
                ? throw new ArgumentException("A settlement id is required.", nameof(settlementId))
                : settlementId;
            IsActive = isActive;
            IsFortification = isFortification;
            IsOwnedByKingdom = isOwnedByKingdom;
            IsBesieged = isBesieged;
            HasMapEvent = hasMapEvent;
            IsTarget = isTarget;
            IsReachableFromCommander = isReachableFromCommander;
            IsReachableFromTarget = isReachableFromTarget;
            IsFarEnoughFromTarget = isFarEnoughFromTarget;
        }

        public string SettlementId { get; }
        public bool IsActive { get; }
        public bool IsFortification { get; }
        public bool IsOwnedByKingdom { get; }
        public bool IsBesieged { get; }
        public bool HasMapEvent { get; }
        public bool IsTarget { get; }
        public bool IsReachableFromCommander { get; }
        public bool IsReachableFromTarget { get; }
        public bool IsFarEnoughFromTarget { get; }
    }

    public sealed class CapitalArmyStagingRules
    {
        public bool IsEligible(CapitalArmyStagingCandidate candidate) =>
            candidate.IsActive && candidate.IsFortification && candidate.IsOwnedByKingdom &&
            !candidate.IsBesieged && !candidate.HasMapEvent && !candidate.IsTarget &&
            candidate.IsReachableFromCommander && candidate.IsReachableFromTarget &&
            candidate.IsFarEnoughFromTarget;
    }

    public sealed class CapitalArmyMobilizationPlanner
    {
        public CapitalArmyMobilizationPlan? Create(string invasionId,
            InvasionCapitalObjectiveKind objectiveKind, string targetSettlementId,
            IReadOnlyList<CapitalArmyPartyCandidate> candidates)
        {
            if (candidates == null)
            {
                throw new ArgumentNullException(nameof(candidates));
            }

            CapitalArmyPartyCandidate[] unique = candidates
                .GroupBy(candidate => candidate.PartyId, StringComparer.Ordinal)
                .Select(PreferMobilizableRecord)
                .ToArray();
            CapitalArmyPartyCandidate? commander = FindCommander(unique);

            if (commander == null)
            {
                return null;
            }

            string[] roster = unique.Where(candidate => candidate.IsMobilizable)
                .Select(candidate => candidate.PartyId)
                .OrderBy(partyId => partyId, StringComparer.Ordinal)
                .ToArray();
            roster = new[] { commander.Value.PartyId }
                .Concat(roster.Where(partyId => partyId != commander.Value.PartyId))
                .ToArray();
            CapitalArmyPartyDeferral[] deferred = unique
                .Where(candidate => !candidate.IsMobilizable)
                .OrderBy(candidate => candidate.PartyId, StringComparer.Ordinal)
                .Select(candidate => new CapitalArmyPartyDeferral(candidate.PartyId,
                    candidate.DeferredReason!))
                .ToArray();

            return new CapitalArmyMobilizationPlan(invasionId, commander.Value.PartyId, roster,
                objectiveKind, targetSettlementId, deferred);
        }

        private static CapitalArmyPartyCandidate PreferMobilizableRecord(
            IGrouping<string, CapitalArmyPartyCandidate> records) => records
            .OrderByDescending(candidate => candidate.IsMobilizable)
            .ThenByDescending(candidate => candidate.LeadsArmy)
            .ThenByDescending(candidate => candidate.CanLeadArmy)
            .ThenByDescending(candidate => candidate.Strength)
            .First();

        private static CapitalArmyPartyCandidate? FindCommander(
            IReadOnlyList<CapitalArmyPartyCandidate> candidates)
        {
            CapitalArmyPartyCandidate? armyLeader = Strongest(candidates,
                candidate => candidate.IsMobilizable && candidate.LeadsArmy &&
                             candidate.CanLeadArmy);

            return armyLeader ?? Strongest(candidates,
                candidate => candidate.IsMobilizable && candidate.CanLeadArmy);
        }

        private static CapitalArmyPartyCandidate? Strongest(
            IReadOnlyList<CapitalArmyPartyCandidate> candidates,
            Func<CapitalArmyPartyCandidate, bool> predicate) => candidates
            .Where(predicate)
            .OrderByDescending(candidate => candidate.Strength)
            .ThenBy(candidate => candidate.PartyId, StringComparer.Ordinal)
            .Cast<CapitalArmyPartyCandidate?>()
            .FirstOrDefault();
    }
}
