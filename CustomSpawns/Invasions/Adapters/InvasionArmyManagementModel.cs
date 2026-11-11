using CustomSpawns.Invasions.CapitalObjectives;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace CustomSpawns.Invasions.Adapters
{
    // Whether a clan fights for a live invasion.
    public interface IInvaderClanQuery
    {
        bool IsInvaderClan(string? clanId);
    }

    /**
     * Raising an army costs an invasion's clans no influence. An invasion arrives without any standing at
     * court, so vanilla's price for calling lords to arms would keep it from ever gathering one, however
     * many men it lands.
     *
     * Capital relief gathering also keeps its cohesion until the objective allows it to advance, and a garrison
     * army keeps it for as long as its lord lives.
     */
    public sealed class InvasionArmyManagementModel : ArmyManagementCalculationModel
    {
        // Vanilla refuses an army to a clan holding no more influence than this, and the refusal is written
        // into CanLordCreateArmy itself rather than asked of any model.
        private const float VanillaArmyInfluenceThreshold = 100f;

        private readonly ArmyManagementCalculationModel _armyManagementCalculationModel;
        private readonly IInvaderClanQuery _invaderClanQuery;
        private readonly IInvasionCapitalObjectiveQuery _invasionCapitalObjectiveQuery;
        private readonly ClanInfluenceLift _clanInfluenceLift;
        private readonly IInvasionGarrisonArmyLeaders _invasionGarrisonArmyLeaders;

        public InvasionArmyManagementModel(ArmyManagementCalculationModel decorated,
            IInvaderClanQuery invaderClanQuery, IInvasionCapitalObjectiveQuery invasionCapitalObjectiveQuery,
            ClanInfluenceLift clanInfluenceLift, IInvasionGarrisonArmyLeaders invasionGarrisonArmyLeaders)
        {
            _armyManagementCalculationModel = decorated;
            _invaderClanQuery = invaderClanQuery;
            _invasionCapitalObjectiveQuery = invasionCapitalObjectiveQuery;
            _clanInfluenceLift = clanInfluenceLift;
            _invasionGarrisonArmyLeaders = invasionGarrisonArmyLeaders;
        }

        public override float AIMobilePartySizeRatioToCallToArmy => _armyManagementCalculationModel.AIMobilePartySizeRatioToCallToArmy;

        public override float PlayerMobilePartySizeRatioToCallToArmy =>
            _armyManagementCalculationModel.PlayerMobilePartySizeRatioToCallToArmy;

        public override float MinimumNeededFoodInDaysToCallToArmy => _armyManagementCalculationModel.MinimumNeededFoodInDaysToCallToArmy;

        public override float MaximumDistanceToCallToArmy => _armyManagementCalculationModel.MaximumDistanceToCallToArmy;

        public override int InfluenceValuePerGold => _armyManagementCalculationModel.InfluenceValuePerGold;

        public override int AverageCallToArmyCost => _armyManagementCalculationModel.AverageCallToArmyCost;

        public override int CohesionThresholdForDispersion => _armyManagementCalculationModel.CohesionThresholdForDispersion;

        public override float MaximumWaitTime => _armyManagementCalculationModel.MaximumWaitTime;

        // The clan's influence is lifted over vanilla's threshold for the length of the decorated call and
        // put back the moment it returns, so nothing but the refusal ever sees it.
        public override bool CanLordCreateArmy(MobileParty leaderParty, out MBList<MobileParty> possibleArmyMembers)
        {
            Clan? clan = leaderParty.LeaderHero?.Clan;

            if (clan == null || !_invaderClanQuery.IsInvaderClan(clan.StringId))
            {
                return _armyManagementCalculationModel.CanLordCreateArmy(leaderParty, out possibleArmyMembers);
            }

            MBList<MobileParty> members = new MBList<MobileParty>();
            bool canCreate = _clanInfluenceLift.While(clan, VanillaArmyInfluenceThreshold + 1f,
                () => _armyManagementCalculationModel.CanLordCreateArmy(leaderParty, out members));
            possibleArmyMembers = members;
            return canCreate;
        }

        // What each lord called to the army costs its leader's clan, which vanilla takes the moment he joins.
        public override int CalculatePartyInfluenceCost(MobileParty armyLeaderParty, MobileParty party) =>
            _invaderClanQuery.IsInvaderClan(armyLeaderParty.LeaderHero?.Clan?.StringId)
                ? 0
                : _armyManagementCalculationModel.CalculatePartyInfluenceCost(armyLeaderParty, party);

        public override float DailyBeingAtArmyInfluenceAward(MobileParty armyMemberParty) =>
            _armyManagementCalculationModel.DailyBeingAtArmyInfluenceAward(armyMemberParty);

        public override bool CanPlayerCreateArmy(out TextObject disabledReason) =>
            _armyManagementCalculationModel.CanPlayerCreateArmy(out disabledReason);

        public override int CalculateTotalInfluenceCost(Army army, float percentage) =>
            IsInvaderArmy(army) ? 0 : _armyManagementCalculationModel.CalculateTotalInfluenceCost(army, percentage);

        public override float GetPartySizeScore(MobileParty party) => _armyManagementCalculationModel.GetPartySizeScore(party);

        public override bool CheckPartyEligibility(MobileParty party, out TextObject explanation) =>
            _armyManagementCalculationModel.CheckPartyEligibility(party, out explanation);

        public override int GetPartyRelation(Hero hero) => _armyManagementCalculationModel.GetPartyRelation(hero);

        public override ExplainedNumber CalculateDailyCohesionChange(Army army, bool includeDescriptions = false) =>
            KeepsArmyTogether(army)
                ? new ExplainedNumber(0f, includeDescriptions)
                : _armyManagementCalculationModel.CalculateDailyCohesionChange(army, includeDescriptions);

        public override int CalculateNewCohesion(Army army, PartyBase newParty, int calculatedCohesion, int sign) =>
            KeepsArmyTogether(army)
                ? 100
                : _armyManagementCalculationModel.CalculateNewCohesion(army, newParty, calculatedCohesion, sign);

        // Keeping an army together costs an invasion nothing either, or its armies would scatter as soon as
        // they gathered.
        public override int GetCohesionBoostInfluenceCost(Army army, int percentageToBoost = 100) =>
            IsInvaderArmy(army) ? 0 : _armyManagementCalculationModel.GetCohesionBoostInfluenceCost(army, percentageToBoost);

        private bool IsInvaderArmy(Army army) =>
            _invaderClanQuery.IsInvaderClan(army.LeaderParty?.LeaderHero?.Clan?.StringId);

        // A garrison army fights on until its lord is beaten, so it never runs out of cohesion either.
        private bool KeepsArmyTogether(Army army)
        {
            MobileParty? commander = army?.LeaderParty;

            if (_invasionGarrisonArmyLeaders.LeadsGarrisonArmy(commander?.LeaderHero?.StringId))
            {
                return true;
            }

            string? kingdomId = commander?.MapFaction?.StringId;
            return kingdomId != null &&
                   _invasionCapitalObjectiveQuery.FindForKingdom(kingdomId)
                       ?.KeepsArmyTogether(commander!.StringId) == true;
        }
    }
}
