using System;
using CustomSpawns.AI.Adapters;
using CustomSpawns.AI.Model;
using CustomSpawns.Data.Dto;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace CustomSpawns.AI.Offensives
{
    public sealed class BesiegeClosestSettlementBehaviour
        : SettlementOffensivePartyBehaviour<BesiegeClosestSettlementData>
    {
        internal const string SaveKey = "_customSpawnsBesiegeClosestSettlementStates";
        private readonly ISiegeOperations _siege;

        internal BesiegeClosestSettlementBehaviour(ISiegeOperations siege,
            Func<float, float, float> roll)
            : this(siege, roll, new PartyStateStore<SettlementOffensiveBehaviourState>(SaveKey))
        {
        }

        internal BesiegeClosestSettlementBehaviour(ISiegeOperations siege,
            Func<float, float, float> roll,
            PartyStateStore<SettlementOffensiveBehaviourState> states) : base(roll, states)
        {
            _siege = siege ?? throw new ArgumentNullException(nameof(siege));
        }

        private protected override IPartyStrengthObserver PartyStrength => _siege;

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
         * The strength requirement that qualified the target when it was selected is re-checked every tick.
         * While a siege camp is active, the assessment instead combines the garrison with nearby non-fleeing
         * hostile parties so that reinforcements can make the besieger withdraw before an assault starts.
         */
        private protected override bool ShouldAbandonTarget(MobileParty party, Settlement target,
            float attackerStrength, BesiegeClosestSettlementData configuration)
        {
            if (IsActiveSiegeCamp(party, target))
            {
                return !OffensiveStrengthRules.MeetsStrengthRatio(attackerStrength,
                    _siege.GetActiveSiegeDefenderStrength(party, target),
                    configuration.MinimumAttackerToTargetRatio);
            }

            return !OffensiveStrengthRules.MeetsStrengthRatio(attackerStrength,
                GetTargetStrength(party, target), configuration.MinimumAttackerToTargetRatio);
        }

        private protected override AIBehaviorData? CreateTargetAbandonmentBehavior(
            MobileParty party, Settlement target, BesiegeClosestSettlementData configuration)
        {
            return IsActiveSiegeCamp(party, target)
                ? _siege.AbandonSiegeAndCreateRetreatBehavior(party, target)
                : null;
        }

        private bool IsActiveSiegeCamp(MobileParty party, Settlement target)
        {
            return _siege.IsMatchingSiegeActive(party, target) &&
                   !_siege.IsMatchingAssaultActive(party, target);
        }

        private protected override float GetReconVisibleHostileStrength(MobileParty party,
            Settlement target, float visibleHostileStrength)
        {
            return _siege.GetHostileLordStrengthNearTarget(party, target);
        }

        private protected override bool ReselectTargetWhenUnsafe => true;

        private protected override Settlement? FindAlternativeTarget(MobileParty party,
            Settlement target, float attackerStrength, BesiegeClosestSettlementData configuration)
        {
            return _siege.FindAlternativeSiegeTarget(party, target, attackerStrength,
                configuration.MinimumAttackerToTargetRatio,
                configuration.MinimumAttackerToRoamingHostilesRatio, configuration.TargetTypes);
        }

        private protected override Settlement? FindTarget(MobileParty party, float attackerStrength,
            BesiegeClosestSettlementData configuration)
        {
            return _siege.FindClosestSiegeTarget(party, attackerStrength,
                configuration.MinimumAttackerToTargetRatio,
                configuration.MinimumAttackerToRoamingHostilesRatio, configuration.TargetTypes);
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
