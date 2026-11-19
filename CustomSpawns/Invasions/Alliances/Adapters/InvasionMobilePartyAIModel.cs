using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace CustomSpawns.Invasions.Alliances.Adapters
{
    // Lets the lords of a coalition, and of a line, join the battles their allies fight nearby; see InvasionAlliedBattles.
    public sealed class InvasionMobilePartyAIModel : MobilePartyAIModel
    {
        private readonly MobilePartyAIModel _mobilePartyAIModel;
        private readonly BannerlordInvasionAlliedBattles _bannerlordInvasionAlliedBattles;

        public InvasionMobilePartyAIModel(MobilePartyAIModel decorated,
            BannerlordInvasionAlliedBattles bannerlordInvasionAlliedBattles)
        {
            _mobilePartyAIModel = decorated;
            _bannerlordInvasionAlliedBattles = bannerlordInvasionAlliedBattles;
        }

        public override float AiCheckInterval => _mobilePartyAIModel.AiCheckInterval;

        public override float FleeToNearbyPartyRadius => _mobilePartyAIModel.FleeToNearbyPartyRadius;

        public override float FleeToNearbySettlementRadius => _mobilePartyAIModel.FleeToNearbySettlementRadius;

        public override float HideoutPatrolDistanceAsDays => _mobilePartyAIModel.HideoutPatrolDistanceAsDays;

        public override float FortificationPatrolDistanceAsDays =>
            _mobilePartyAIModel.FortificationPatrolDistanceAsDays;

        public override float FortificationPortPatrolDistanceAsDays =>
            _mobilePartyAIModel.FortificationPortPatrolDistanceAsDays;

        public override float VillagePatrolDistanceAsDays => _mobilePartyAIModel.VillagePatrolDistanceAsDays;

        public override float SettlementDefendingNearbyPartyCheckRadius =>
            _mobilePartyAIModel.SettlementDefendingNearbyPartyCheckRadius;

        public override float SettlementDefendingWaitingPositionRadius =>
            _mobilePartyAIModel.SettlementDefendingWaitingPositionRadius;

        public override float NeededFoodsInDaysThresholdForSiege =>
            _mobilePartyAIModel.NeededFoodsInDaysThresholdForSiege;

        public override float NeededFoodsInDaysThresholdForRaid =>
            _mobilePartyAIModel.NeededFoodsInDaysThresholdForRaid;

        public override bool ShouldConsiderAvoiding(MobileParty party, MobileParty targetParty) =>
            _mobilePartyAIModel.ShouldConsiderAvoiding(party, targetParty);

        public override bool ShouldConsiderAttacking(MobileParty party, MobileParty targetParty) =>
            _mobilePartyAIModel.ShouldConsiderAttacking(party, targetParty);

        public override float GetPatrolRadius(MobileParty mobileParty, CampaignVec2 patrolPoint) =>
            _mobilePartyAIModel.GetPatrolRadius(mobileParty, patrolPoint);

        public override float GetSettlementNearbyThreatAndAllyCheckRadius(Settlement settlement, bool isPort) =>
            _mobilePartyAIModel.GetSettlementNearbyThreatAndAllyCheckRadius(settlement, isPort);

        public override bool ShouldPartyCheckInitiativeBehavior(MobileParty mobileParty) =>
            _mobilePartyAIModel.ShouldPartyCheckInitiativeBehavior(mobileParty);

        public override void GetBestInitiativeBehavior(MobileParty mobileParty, out AiBehavior bestInitiativeBehavior,
            out MobileParty bestInitiativeTargetParty, out float bestInitiativeBehaviorScore, out Vec2 averageEnemyVec)
        {
            _mobilePartyAIModel.GetBestInitiativeBehavior(mobileParty, out bestInitiativeBehavior,
                out bestInitiativeTargetParty, out bestInitiativeBehaviorScore, out averageEnemyVec);
            _bannerlordInvasionAlliedBattles.JoinAlliedBattle(mobileParty, ref bestInitiativeBehavior,
                ref bestInitiativeTargetParty, ref bestInitiativeBehaviorScore);
        }
    }
}
