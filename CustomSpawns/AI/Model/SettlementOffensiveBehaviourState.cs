using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.SaveSystem;

namespace CustomSpawns.AI.Model
{
    internal sealed class SettlementOffensiveBehaviourState
    {
        [SaveableField(1)]
        private Settlement? _target;

        [SaveableField(2)]
        private SettlementOffensivePhase _phase;

        [SaveableField(3)]
        private float _requiredClearReconDays;

        [SaveableField(4)]
        private float _accumulatedClearReconDays;

        [SaveableField(5)]
        private float _operationWindowDays;

        [SaveableField(6)]
        private float _elapsedOperationDays;

        [SaveableField(7)]
        private float _recoveryGraceDays;

        [SaveableField(8)]
        private float _recoveryElapsedDays;

        [SaveableField(9)]
        private int _failedAttempts;

        [SaveableField(10)]
        private float _cooldownRemainingDays;

        [SaveableField(11)]
        private IFaction? _targetMapFaction;

        public Settlement? Target { get => _target; set => _target = value; }
        public SettlementOffensivePhase Phase { get => _phase; set => _phase = value; }
        public float RequiredClearReconDays { get => _requiredClearReconDays; set => _requiredClearReconDays = value; }
        public float AccumulatedClearReconDays { get => _accumulatedClearReconDays; set => _accumulatedClearReconDays = value; }
        public float OperationWindowDays { get => _operationWindowDays; set => _operationWindowDays = value; }
        public float ElapsedOperationDays { get => _elapsedOperationDays; set => _elapsedOperationDays = value; }
        public float RecoveryGraceDays { get => _recoveryGraceDays; set => _recoveryGraceDays = value; }
        public float RecoveryElapsedDays { get => _recoveryElapsedDays; set => _recoveryElapsedDays = value; }
        public int FailedAttempts { get => _failedAttempts; set => _failedAttempts = value; }
        public float CooldownRemainingDays { get => _cooldownRemainingDays; set => _cooldownRemainingDays = value; }
        public IFaction? TargetMapFaction { get => _targetMapFaction; set => _targetMapFaction = value; }
    }
}
