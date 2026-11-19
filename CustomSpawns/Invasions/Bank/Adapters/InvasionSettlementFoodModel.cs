using CustomSpawns.Invasions.Adapters;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;

namespace CustomSpawns.Invasions.Bank.Adapters
{
    // An invasion's banked reserve does not eat its town's food; see InvasionBankRations.
    public sealed class InvasionSettlementFoodModel : SettlementFoodModel
    {
        private static readonly TextObject SupplyTrainText = new("Invasion supply train");
        private readonly SettlementFoodModel _settlementFoodModel;
        private readonly IInvaderClanQuery _invaderClanQuery;
        private readonly InvasionBankRations _invasionBankRations;

        public InvasionSettlementFoodModel(SettlementFoodModel decorated, IInvaderClanQuery invaderClanQuery,
            InvasionBankRations invasionBankRations)
        {
            _settlementFoodModel = decorated;
            _invaderClanQuery = invaderClanQuery;
            _invasionBankRations = invasionBankRations;
        }

        public override int FoodStocksUpperLimit => _settlementFoodModel.FoodStocksUpperLimit;

        public override int NumberOfProsperityToEatOneFood => _settlementFoodModel.NumberOfProsperityToEatOneFood;

        public override int NumberOfMenOnGarrisonToEatOneFood =>
            _settlementFoodModel.NumberOfMenOnGarrisonToEatOneFood;

        public override int CastleFoodStockUpperLimitBonus => _settlementFoodModel.CastleFoodStockUpperLimitBonus;

        public override ExplainedNumber CalculateTownFoodStocksChange(Town town, bool includeMarketStocks = true,
            bool includeDescriptions = false)
        {
            ExplainedNumber change = _settlementFoodModel.CalculateTownFoodStocksChange(town, includeMarketStocks,
                includeDescriptions);
            MobileParty? garrison = town.GarrisonParty;

            if (garrison == null)
            {
                return change;
            }

            float spared = _invasionBankRations.FoodSpared(
                _invaderClanQuery.IsInvaderClan(town.OwnerClan?.StringId), garrison.Party.NumberOfAllMembers,
                garrison.Party.PartySizeLimit, NumberOfMenOnGarrisonToEatOneFood);

            if (spared > 0f)
            {
                change.Add(spared, SupplyTrainText);
            }

            return change;
        }
    }
}
