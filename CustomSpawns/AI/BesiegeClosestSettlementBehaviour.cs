using System;
using CustomSpawns.AI.Model;
using CustomSpawns.Data.Dto;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace CustomSpawns.AI
{
    public sealed class BesiegeClosestSettlementBehaviour
        : SettlementOffensivePartyBehaviour<BesiegeClosestSettlementData>
    {
        internal const string SaveKey = "_customSpawnsBesiegeClosestSettlementStates";
        private readonly ISiegeOperations _siege;

        internal BesiegeClosestSettlementBehaviour(ISiegeOperations siege,
            Func<float, float, float> roll) : base(roll, SaveKey)
        {
            _siege = siege ?? throw new ArgumentNullException(nameof(siege));
        }

        private protected override IPartyStrengthObserver PartyStrength => _siege;

        private protected override IOffensiveOperationStatus OperationStatus => _siege;

        private protected override IPatrolOrders PatrolOrders => _siege;

        private protected override void TrackTarget(SettlementOffensiveBehaviourState state)
        {
            state.TargetMapFaction ??= _siege.GetTargetMapFaction(state.Target!);
        }

        private protected override bool IsMatchingOperationActive(MobileParty party, Settlement target)
        {
            return _siege.IsMatchingSiegeActive(party, target) ||
                   _siege.IsMatchingAssaultActive(party, target);
        }

        private protected override bool IsCurrentTargetValid(MobileParty party,
            SettlementOffensiveBehaviourState state, bool matchingOperationActive)
        {
            return _siege.IsCurrentTargetValid(party, state.Target!, matchingOperationActive,
                state.TargetMapFaction);
        }

        private protected override bool IsTargetComplete(MobileParty party, Settlement target)
        {
            return _siege.IsTargetComplete(party, target);
        }

        /**
         * The strength requirement that qualified the target when it was selected is re-checked every tick,
         * so a force weakened by losses or facing reinforced defenders abandons a target it can no longer take.
         * Re-selection filters on the same ratio, hence the dropped target cannot be picked again while too strong.
         */
        private protected override bool ShouldAbandonTarget(MobileParty party, Settlement target,
            float attackerStrength, BesiegeClosestSettlementData configuration)
        {
            return !SettlementOffensiveBehaviour.MeetsStrengthRatio(attackerStrength,
                GetTargetStrength(party, target), configuration.MinimumAttackerToTargetRatio);
        }

        private protected override Settlement? FindTarget(MobileParty party, float attackerStrength,
            BesiegeClosestSettlementData configuration)
        {
            return _siege.FindClosestSiegeTarget(party, attackerStrength,
                configuration.MinimumAttackerToTargetRatio, configuration.TargetTypes);
        }

        private protected override float GetTargetStrength(MobileParty party, Settlement target)
        {
            return _siege.GetTargetStrength(party, target);
        }

        private protected override AIBehaviorData CreateOffensiveBehavior(MobileParty party,
            Settlement target)
        {
            return _siege.CreateSiegeBehavior(party, target);
        }
    }
}
