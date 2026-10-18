using System;

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
        Dormant = 3
    }

    public enum InvasionCapitalObjectiveDormantReason
    {
        None = 0,
        NoViableTarget = 1,
        AttackTimedOut = 2
    }

    public sealed class InvasionCapitalObjective
    {
        private const double GatherDurationDays = 3d;
        private const double AttackDurationDays = 7d;

        public InvasionCapitalObjective(InvasionCapitalObjectiveKind kind, string targetSettlementId,
            double nowDays, string? defenceSiegeEpisodeId = null)
        {
            Kind = kind;
            Phase = InvasionCapitalObjectivePhase.Gathering;
            TargetSettlementId = RequiredId(targetSettlementId, nameof(targetSettlementId));
            GatherDeadlineDays = nowDays + GatherDurationDays;
            AttackDeadlineDays = kind == InvasionCapitalObjectiveKind.Attack
                ? nowDays + AttackDurationDays
                : null;
            DormantReason = InvasionCapitalObjectiveDormantReason.None;
            DefenceSiegeEpisodeId = EmptyAsNull(defenceSiegeEpisodeId);

            Validate();
        }

        // Save-boundary reconstruction. The constructor rejects every state the aggregate cannot create.
        public InvasionCapitalObjective(InvasionCapitalObjectiveKind kind,
            InvasionCapitalObjectivePhase phase, string? targetSettlementId, double? gatherDeadlineDays,
            double? attackDeadlineDays, InvasionCapitalObjectiveDormantReason dormantReason,
            string? commanderPartyId, string? defenceSiegeEpisodeId = null)
        {
            Kind = kind;
            Phase = phase;
            TargetSettlementId = EmptyAsNull(targetSettlementId);
            GatherDeadlineDays = gatherDeadlineDays;
            AttackDeadlineDays = attackDeadlineDays;
            DormantReason = dormantReason;
            CommanderPartyId = EmptyAsNull(commanderPartyId);
            DefenceSiegeEpisodeId = EmptyAsNull(defenceSiegeEpisodeId);

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

        public bool IsGatherDeadlineReached(double nowDays) =>
            Phase == InvasionCapitalObjectivePhase.Gathering && GatherDeadlineDays <= nowDays;

        public bool IsAttackDeadlineReached(double nowDays) =>
            Kind == InvasionCapitalObjectiveKind.Attack &&
            Phase == InvasionCapitalObjectivePhase.Advancing &&
            AttackDeadlineDays <= nowDays;

        public void AssignCommander(string partyId)
        {
            if (Phase != InvasionCapitalObjectivePhase.Gathering &&
                Phase != InvasionCapitalObjectivePhase.Advancing)
            {
                throw new InvalidOperationException("Only an active capital objective can have a commander.");
            }

            CommanderPartyId = RequiredId(partyId, nameof(partyId));
        }

        public void ClearCommander()
        {
            CommanderPartyId = null;
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

        public void MarkBesieging()
        {
            if (Kind != InvasionCapitalObjectiveKind.Attack)
            {
                throw new InvalidOperationException("Only an attack objective can begin a siege.");
            }

            RequirePhase(InvasionCapitalObjectivePhase.Advancing);

            Phase = InvasionCapitalObjectivePhase.Besieging;
            AttackDeadlineDays = null;
            CommanderPartyId = null;
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
            Validate();
        }

        public void TimeOutAttack()
        {
            if (Kind != InvasionCapitalObjectiveKind.Attack)
            {
                throw new InvalidOperationException("Only an attack objective can time out.");
            }

            RequirePhase(InvasionCapitalObjectivePhase.Advancing);

            Phase = InvasionCapitalObjectivePhase.Dormant;
            TargetSettlementId = null;
            GatherDeadlineDays = null;
            AttackDeadlineDays = null;
            DormantReason = InvasionCapitalObjectiveDormantReason.AttackTimedOut;
            CommanderPartyId = null;
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
                DormantReason == InvasionCapitalObjectiveDormantReason.None)
            {
                throw new ArgumentException("A dormant capital objective must be untargeted and have a reason.");
            }
        }

        private void ValidateActiveDeadlines()
        {
            if (Phase == InvasionCapitalObjectivePhase.Gathering)
            {
                bool hasExpectedAttackDeadline = Kind == InvasionCapitalObjectiveKind.Attack
                    ? AttackDeadlineDays != null
                    : AttackDeadlineDays == null;

                if (GatherDeadlineDays == null || !hasExpectedAttackDeadline)
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

                if (GatherDeadlineDays != null || !hasExpectedAttackDeadline)
                {
                    throw new ArgumentException("An advancing capital objective has invalid deadlines.");
                }

                return;
            }

            if (Phase != InvasionCapitalObjectivePhase.Besieging ||
                Kind != InvasionCapitalObjectiveKind.Attack || GatherDeadlineDays != null ||
                AttackDeadlineDays != null || CommanderPartyId != null)
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

        private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
