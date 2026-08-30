using System;
using CustomSpawns.AI.Model;
using CustomSpawns.Data.Dto;
using CustomSpawns.Spawn;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace CustomSpawns.AI
{
    public sealed class BesiegeClosestSettlementBehaviour
        : SettlementOffensivePartyBehaviour<BesiegeClosestSettlementData>
    {
        internal const string SaveKey = "_customSpawnsBesiegeClosestSettlementStates";
        private readonly IBesiegeClosestSettlementContext _context;

        public BesiegeClosestSettlementBehaviour(DynamicSpawnData dynamicSpawnData) : this(
            new BannerlordBesiegeClosestSettlementContext(dynamicSpawnData), MBRandom.RandomFloatRanged)
        {
        }

        internal BesiegeClosestSettlementBehaviour(IBesiegeClosestSettlementContext context,
            Func<float, float, float> roll) : base(roll, SaveKey)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        private protected override ISettlementOffensiveContext Context => _context;

        private protected override void TrackTarget(SettlementOffensiveBehaviourState state)
        {
            state.TargetMapFaction ??= _context.GetTargetMapFaction(state.Target!);
        }

        private protected override bool IsMatchingOperationActive(MobileParty party, Settlement target)
        {
            return _context.IsMatchingSiegeActive(party, target) ||
                   _context.IsMatchingAssaultActive(party, target);
        }

        private protected override bool IsCurrentTargetValid(MobileParty party,
            SettlementOffensiveBehaviourState state, bool matchingOperationActive)
        {
            return _context.IsCurrentTargetValid(party, state.Target!, matchingOperationActive,
                state.TargetMapFaction);
        }

        private protected override bool IsTargetComplete(MobileParty party, Settlement target)
        {
            return _context.IsTargetComplete(party, target);
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
            return _context.FindClosestSiegeTarget(party, attackerStrength,
                configuration.MinimumAttackerToTargetRatio, configuration.TargetTypes);
        }

        private protected override float GetTargetStrength(MobileParty party, Settlement target)
        {
            return _context.GetTargetStrength(party, target);
        }

        private protected override AIBehaviorData CreateOffensiveBehavior(MobileParty party,
            Settlement target)
        {
            return _context.CreateSiegeBehavior(party, target);
        }
    }
}
