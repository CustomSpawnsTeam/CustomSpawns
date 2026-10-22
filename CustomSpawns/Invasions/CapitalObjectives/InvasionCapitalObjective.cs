using System;
using System.Collections.Generic;
using System.Linq;

namespace CustomSpawns.Invasions.CapitalObjectives
{
    public enum InvasionCapitalObjectiveKind
    {
        Defence = 0,
        Attack = 1
    }

    public enum InvasionCapitalObjectivePhase
    {
        Gathering = 0,
        Advancing = 1,
        Besieging = 2,
        Dormant = 3,
        Mobilizing = 4
    }

    public enum InvasionCapitalObjectiveDormantReason
    {
        None = 0,
        NoViableTarget = 1,
        AttackTimedOut = 2,
        SiegeAborted = 3
    }

    public sealed class InvasionCapitalObjective
    {
        private const double GatherDurationDays = 3d;
        private const double AttackDurationDays = 7d;

        public InvasionCapitalObjective(InvasionCapitalObjectiveKind kind, string targetSettlementId,
            double nowDays, string? defenceSiegeEpisodeId = null)
        {
            Kind = kind;
            Phase = InvasionCapitalObjectivePhase.Mobilizing;
            TargetSettlementId = RequiredId(targetSettlementId, nameof(targetSettlementId));
            GatherDeadlineDays = null;
            AttackDeadlineDays = kind == InvasionCapitalObjectiveKind.Attack
                ? nowDays + AttackDurationDays
                : null;
            DormantReason = InvasionCapitalObjectiveDormantReason.None;
            DefenceSiegeEpisodeId = EmptyAsNull(defenceSiegeEpisodeId);
            MobilizedPartyIds = Array.Empty<string>();

            Validate();
        }

        // Save-boundary reconstruction. The constructor rejects every state the aggregate cannot create.
        public InvasionCapitalObjective(InvasionCapitalObjectiveKind kind,
            InvasionCapitalObjectivePhase phase, string? targetSettlementId, double? gatherDeadlineDays,
            double? attackDeadlineDays, InvasionCapitalObjectiveDormantReason dormantReason,
            string? commanderPartyId, string? defenceSiegeEpisodeId = null,
            string? stagingSettlementId = null, IReadOnlyList<string>? mobilizedPartyIds = null)
        {
            Kind = kind;
            Phase = phase;
            TargetSettlementId = EmptyAsNull(targetSettlementId);
            GatherDeadlineDays = gatherDeadlineDays;
            AttackDeadlineDays = attackDeadlineDays;
            DormantReason = dormantReason;
            CommanderPartyId = EmptyAsNull(commanderPartyId);
            DefenceSiegeEpisodeId = EmptyAsNull(defenceSiegeEpisodeId);
            StagingSettlementId = EmptyAsNull(stagingSettlementId);
            MobilizedPartyIds = NormalizePartyIds(mobilizedPartyIds);

            NormalizeLegacyActiveState();

            Validate();
        }

        public InvasionCapitalObjectiveKind Kind { get; private set; }
        public InvasionCapitalObjectivePhase Phase { get; private set; }
        public string? TargetSettlementId { get; private set; }
        public double? GatherDeadlineDays { get; private set; }
        public double? AttackDeadlineDays { get; private set; }
        public InvasionCapitalObjectiveDormantReason DormantReason { get; private set; }
        public string? CommanderPartyId { get; private set; }
        public string? DefenceSiegeEpisodeId { get; private set; }
        public string? StagingSettlementId { get; private set; }
        public IReadOnlyList<string> MobilizedPartyIds { get; private set; }

        public bool IsGatherDeadlineReached(double nowDays) =>
            Phase == InvasionCapitalObjectivePhase.Gathering && GatherDeadlineDays <= nowDays;

        public bool IsAttackDeadlineReached(double nowDays) =>
            Kind == InvasionCapitalObjectiveKind.Attack &&
            (Phase == InvasionCapitalObjectivePhase.Mobilizing ||
             Phase == InvasionCapitalObjectivePhase.Gathering ||
             Phase == InvasionCapitalObjectivePhase.Advancing) &&
            AttackDeadlineDays <= nowDays;

        public void MarkMobilized(CapitalArmyMobilizationResult result, double nowDays)
        {
            RequirePhase(InvasionCapitalObjectivePhase.Mobilizing);

            if (!result.Succeeded)
            {
                throw new ArgumentException("A failed mobilization cannot begin gathering.",
                    nameof(result));
            }

            CommanderPartyId = result.CommanderPartyId;
            StagingSettlementId = result.StagingSettlementId;
            MobilizedPartyIds = result.PartyIds.ToArray();
            Phase = InvasionCapitalObjectivePhase.Gathering;
            GatherDeadlineDays = nowDays + GatherDurationDays;
            Validate();
        }

        public void RequireRemobilization()
        {
            if (Phase != InvasionCapitalObjectivePhase.Gathering &&
                Phase != InvasionCapitalObjectivePhase.Advancing)
            {
                throw new InvalidOperationException(
                    "Only a gathering or advancing objective can remobilize.");
            }

            Phase = InvasionCapitalObjectivePhase.Mobilizing;
            GatherDeadlineDays = null;
            CommanderPartyId = null;
            StagingSettlementId = null;
            MobilizedPartyIds = Array.Empty<string>();
            Validate();
        }

        public void AssignCommander(string partyId)
        {
            if (Phase != InvasionCapitalObjectivePhase.Gathering &&
                Phase != InvasionCapitalObjectivePhase.Advancing)
            {
                throw new InvalidOperationException("Only an active capital objective can have a commander.");
            }

            string requiredPartyId = RequiredId(partyId, nameof(partyId));
            if (!MobilizedPartyIds.Contains(requiredPartyId, StringComparer.Ordinal))
            {
                throw new ArgumentException("The commander must belong to the mobilized army.",
                    nameof(partyId));
            }

            CommanderPartyId = requiredPartyId;
            Validate();
        }

        public void ClearCommander()
        {
            RequireRemobilization();
        }

        public void MarkReady()
        {
            RequirePhase(InvasionCapitalObjectivePhase.Gathering);

            Phase = InvasionCapitalObjectivePhase.Advancing;
            GatherDeadlineDays = null;
            Validate();
        }

        public void RetargetForAttack(string settlementId, double nowDays)
        {
            Kind = InvasionCapitalObjectiveKind.Attack;
            Phase = InvasionCapitalObjectivePhase.Advancing;
            TargetSettlementId = RequiredId(settlementId, nameof(settlementId));
            GatherDeadlineDays = null;
            AttackDeadlineDays = nowDays + AttackDurationDays;
            DormantReason = InvasionCapitalObjectiveDormantReason.None;
            Validate();
        }

        public void RestartAttackForMobilization(string settlementId, double nowDays)
        {
            RequirePhase(InvasionCapitalObjectivePhase.Dormant);

            Kind = InvasionCapitalObjectiveKind.Attack;
            Phase = InvasionCapitalObjectivePhase.Mobilizing;
            TargetSettlementId = RequiredId(settlementId, nameof(settlementId));
            GatherDeadlineDays = null;
            AttackDeadlineDays = nowDays + AttackDurationDays;
            DormantReason = InvasionCapitalObjectiveDormantReason.None;
            CommanderPartyId = null;
            DefenceSiegeEpisodeId = null;
            StagingSettlementId = null;
            MobilizedPartyIds = Array.Empty<string>();
            Validate();
        }

        public void MarkBesieging()
        {
            if (Kind != InvasionCapitalObjectiveKind.Attack)
            {
                throw new InvalidOperationException("Only an attack objective can begin a siege.");
            }

            if (Phase != InvasionCapitalObjectivePhase.Mobilizing &&
                Phase != InvasionCapitalObjectivePhase.Gathering &&
                Phase != InvasionCapitalObjectivePhase.Advancing)
            {
                throw new InvalidOperationException("Only a pre-siege attack can begin a siege.");
            }

            Phase = InvasionCapitalObjectivePhase.Besieging;
            GatherDeadlineDays = null;
            Validate();
        }

        public void AbortSiege()
        {
            RequirePhase(InvasionCapitalObjectivePhase.Besieging);

            Phase = InvasionCapitalObjectivePhase.Dormant;
            TargetSettlementId = null;
            GatherDeadlineDays = null;
            AttackDeadlineDays = null;
            DormantReason = InvasionCapitalObjectiveDormantReason.SiegeAborted;
            CommanderPartyId = null;
            StagingSettlementId = null;
            MobilizedPartyIds = Array.Empty<string>();
            Validate();
        }

        public void WaitForViableTarget()
        {
            Phase = InvasionCapitalObjectivePhase.Dormant;
            TargetSettlementId = null;
            GatherDeadlineDays = null;
            AttackDeadlineDays = null;
            DormantReason = InvasionCapitalObjectiveDormantReason.NoViableTarget;
            CommanderPartyId = null;
            StagingSettlementId = null;
            MobilizedPartyIds = Array.Empty<string>();
            Validate();
        }

        public void TimeOutAttack()
        {
            if (Kind != InvasionCapitalObjectiveKind.Attack)
            {
                throw new InvalidOperationException("Only an attack objective can time out.");
            }

            if (Phase != InvasionCapitalObjectivePhase.Mobilizing &&
                Phase != InvasionCapitalObjectivePhase.Gathering &&
                Phase != InvasionCapitalObjectivePhase.Advancing)
            {
                throw new InvalidOperationException("Only a pre-siege attack can time out.");
            }

            Phase = InvasionCapitalObjectivePhase.Dormant;
            TargetSettlementId = null;
            GatherDeadlineDays = null;
            AttackDeadlineDays = null;
            DormantReason = InvasionCapitalObjectiveDormantReason.AttackTimedOut;
            CommanderPartyId = null;
            StagingSettlementId = null;
            MobilizedPartyIds = Array.Empty<string>();
            Validate();
        }

        private void Validate()
        {
            if (!Enum.IsDefined(typeof(InvasionCapitalObjectiveKind), Kind) ||
                !Enum.IsDefined(typeof(InvasionCapitalObjectivePhase), Phase) ||
                !Enum.IsDefined(typeof(InvasionCapitalObjectiveDormantReason), DormantReason))
            {
                throw new ArgumentException("Capital objective contains an unknown enum value.");
            }

            if (GatherDeadlineDays.HasValue && !IsFinite(GatherDeadlineDays.Value) ||
                AttackDeadlineDays.HasValue && !IsFinite(AttackDeadlineDays.Value))
            {
                throw new ArgumentException("Capital objective deadlines must be finite.");
            }

            if (Phase == InvasionCapitalObjectivePhase.Dormant)
            {
                ValidateDormant();
                return;
            }

            if (string.IsNullOrWhiteSpace(TargetSettlementId) ||
                DormantReason != InvasionCapitalObjectiveDormantReason.None)
            {
                throw new ArgumentException("An active capital objective requires a target and no dormant reason.");
            }

            ValidateActiveDeadlines();
        }

        private void ValidateDormant()
        {
            bool validDefenceRelease = Kind == InvasionCapitalObjectiveKind.Defence &&
                                       DormantReason == InvasionCapitalObjectiveDormantReason.NoViableTarget;
            bool validAttackRelease = Kind == InvasionCapitalObjectiveKind.Attack;

            if ((!validDefenceRelease && !validAttackRelease) || TargetSettlementId != null ||
                GatherDeadlineDays != null || AttackDeadlineDays != null || CommanderPartyId != null ||
                StagingSettlementId != null || MobilizedPartyIds.Count != 0 ||
                DormantReason == InvasionCapitalObjectiveDormantReason.None)
            {
                throw new ArgumentException("A dormant capital objective must be untargeted and have a reason.");
            }
        }

        private void ValidateActiveDeadlines()
        {
            if (Phase == InvasionCapitalObjectivePhase.Mobilizing)
            {
                bool hasExpectedAttackDeadline = Kind == InvasionCapitalObjectiveKind.Attack
                    ? AttackDeadlineDays != null
                    : AttackDeadlineDays == null;

                if (GatherDeadlineDays != null || !hasExpectedAttackDeadline ||
                    CommanderPartyId != null || StagingSettlementId != null ||
                    MobilizedPartyIds.Count != 0)
                {
                    throw new ArgumentException("A mobilizing capital objective has invalid state.");
                }

                return;
            }

            if (Phase == InvasionCapitalObjectivePhase.Gathering)
            {
                bool hasExpectedAttackDeadline = Kind == InvasionCapitalObjectiveKind.Attack
                    ? AttackDeadlineDays != null
                    : AttackDeadlineDays == null;

                if (GatherDeadlineDays == null || !hasExpectedAttackDeadline ||
                    !HasValidMobilizedArmy())
                {
                    throw new ArgumentException("A gathering capital objective has invalid deadlines.");
                }

                return;
            }

            if (Phase == InvasionCapitalObjectivePhase.Advancing)
            {
                bool hasExpectedAttackDeadline = Kind == InvasionCapitalObjectiveKind.Attack
                    ? AttackDeadlineDays != null
                    : AttackDeadlineDays == null;

                if (GatherDeadlineDays != null || !hasExpectedAttackDeadline ||
                    !HasValidMobilizedArmy())
                {
                    throw new ArgumentException("An advancing capital objective has invalid deadlines.");
                }

                return;
            }

            bool hasNoMobilizedArmy = CommanderPartyId == null && StagingSettlementId == null &&
                                      MobilizedPartyIds.Count == 0;
            bool validCurrentSiege = AttackDeadlineDays != null &&
                                     (HasValidMobilizedArmy() || hasNoMobilizedArmy);
            bool validLegacySiege = AttackDeadlineDays == null && CommanderPartyId == null &&
                                    StagingSettlementId == null && MobilizedPartyIds.Count == 0;

            if (Phase != InvasionCapitalObjectivePhase.Besieging ||
                Kind != InvasionCapitalObjectiveKind.Attack || GatherDeadlineDays != null ||
                !validCurrentSiege && !validLegacySiege)
            {
                throw new ArgumentException("A besieging capital objective has invalid state.");
            }
        }

        private void RequirePhase(InvasionCapitalObjectivePhase expected)
        {
            if (Phase != expected)
            {
                throw new InvalidOperationException("Capital objective must be " + expected + ".");
            }
        }

        private static string RequiredId(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("An id is required.", parameterName);
            }

            return value;
        }

        private static string? EmptyAsNull(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value;

        private bool HasValidMobilizedArmy() => CommanderPartyId != null &&
            StagingSettlementId != null && MobilizedPartyIds.Count != 0 &&
            MobilizedPartyIds.Contains(CommanderPartyId, StringComparer.Ordinal);

        private void NormalizeLegacyActiveState()
        {
            if ((Phase != InvasionCapitalObjectivePhase.Gathering &&
                 Phase != InvasionCapitalObjectivePhase.Advancing) || HasValidMobilizedArmy())
            {
                return;
            }

            Phase = InvasionCapitalObjectivePhase.Mobilizing;
            GatherDeadlineDays = null;
            CommanderPartyId = null;
            StagingSettlementId = null;
            MobilizedPartyIds = Array.Empty<string>();
        }

        private static IReadOnlyList<string> NormalizePartyIds(
            IReadOnlyList<string>? partyIds) => partyIds == null
            ? Array.Empty<string>()
            : partyIds.Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .ToArray();

        private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
