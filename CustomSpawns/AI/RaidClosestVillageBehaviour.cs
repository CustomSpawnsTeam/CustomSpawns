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
    public sealed class RaidClosestVillageBehaviour
        : SettlementOffensivePartyBehaviour<RaidClosestVillageData>
    {
        internal const string SaveKey = "_customSpawnsRaidClosestVillageStates";
        private readonly IRaidClosestVillageContext _context;

        public RaidClosestVillageBehaviour(DynamicSpawnData dynamicSpawnData) : this(
            new BannerlordRaidClosestVillageContext(dynamicSpawnData), MBRandom.RandomFloatRanged)
        {
        }

        internal RaidClosestVillageBehaviour(IRaidClosestVillageContext context,
            Func<float, float, float> roll) : base(roll, SaveKey)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        private protected override ISettlementOffensiveContext Context => _context;

        private protected override bool IsMatchingOperationActive(MobileParty party, Settlement target)
        {
            return _context.IsMatchingRaidActive(party, target);
        }

        private protected override bool IsCurrentTargetValid(MobileParty party,
            SettlementOffensiveBehaviourState state, bool matchingOperationActive)
        {
            return _context.IsCurrentTargetValid(party, state.Target!, matchingOperationActive);
        }

        private protected override bool IsTargetComplete(MobileParty party, Settlement target)
        {
            return _context.IsTargetComplete(target);
        }

        private protected override Settlement? FindTarget(MobileParty party, float attackerStrength,
            RaidClosestVillageData configuration)
        {
            return _context.FindClosestRaidTarget(party, attackerStrength,
                configuration.MinimumAttackerToTargetRatio);
        }

        private protected override float GetTargetStrength(MobileParty party, Settlement target)
        {
            return _context.GetTargetStrength(target);
        }

        private protected override AIBehaviorData CreateOffensiveBehavior(MobileParty party,
            Settlement target)
        {
            return _context.CreateRaidBehavior(party, target);
        }
    }
}
