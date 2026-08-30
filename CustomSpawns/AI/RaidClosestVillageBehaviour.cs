using System;
using CustomSpawns.AI.Model;
using CustomSpawns.Data.Dto;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace CustomSpawns.AI
{
    public sealed class RaidClosestVillageBehaviour
        : SettlementOffensivePartyBehaviour<RaidClosestVillageData>
    {
        internal const string SaveKey = "_customSpawnsRaidClosestVillageStates";
        private readonly IRaidOperations _raid;

        internal RaidClosestVillageBehaviour(IRaidOperations raid,
            Func<float, float, float> roll) : base(roll, SaveKey)
        {
            _raid = raid ?? throw new ArgumentNullException(nameof(raid));
        }

        private protected override IPartyStrengthObserver PartyStrength => _raid;

        private protected override IOffensiveOperationStatus OperationStatus => _raid;

        private protected override IPatrolOrders PatrolOrders => _raid;

        private protected override bool IsMatchingOperationActive(MobileParty party, Settlement target)
        {
            return _raid.IsMatchingRaidActive(party, target);
        }

        private protected override bool IsCurrentTargetValid(MobileParty party,
            SettlementOffensiveBehaviourState state, bool matchingOperationActive)
        {
            return _raid.IsCurrentTargetValid(party, state.Target!, matchingOperationActive);
        }

        private protected override bool IsTargetComplete(MobileParty party, Settlement target)
        {
            return _raid.IsTargetComplete(target);
        }

        private protected override Settlement? FindTarget(MobileParty party, float attackerStrength,
            RaidClosestVillageData configuration)
        {
            return _raid.FindClosestRaidTarget(party, attackerStrength,
                configuration.MinimumAttackerToTargetRatio);
        }

        private protected override float GetTargetStrength(MobileParty party, Settlement target)
        {
            return _raid.GetTargetStrength(target);
        }

        private protected override AIBehaviorData CreateOffensiveBehavior(MobileParty party,
            Settlement target)
        {
            return _raid.CreateRaidBehavior(party, target);
        }
    }
}
