using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;

namespace CustomSpawns.Invasions.Adapters
{
    internal sealed class InvasionSettlementFoodModel : SettlementFoodModel
    {
        private static readonly TextObject InvasionSupplies = new TextObject("{=CustomSpawnsInvasionSupplies}Invasion supplies");
        private readonly SettlementFoodModel _settlementFoodModel;
        private readonly IInvasionFoodSupply _foodSupply;

        internal InvasionSettlementFoodModel(SettlementFoodModel inner, IInvasionFoodSupply foodSupply)
        {
            _settlementFoodModel = inner;
            _foodSupply = foodSupply;
        }

        public override int FoodStocksUpperLimit => _settlementFoodModel.FoodStocksUpperLimit;
        public override int NumberOfProsperityToEatOneFood => _settlementFoodModel.NumberOfProsperityToEatOneFood;
        public override int NumberOfMenOnGarrisonToEatOneFood => _settlementFoodModel.NumberOfMenOnGarrisonToEatOneFood;
        public override int CastleFoodStockUpperLimitBonus => _settlementFoodModel.CastleFoodStockUpperLimitBonus;

        public override ExplainedNumber CalculateTownFoodStocksChange(Town town, bool includeMarketStocks = true,
            bool includeDescriptions = false)
        {
            ExplainedNumber result = _settlementFoodModel.CalculateTownFoodStocksChange(town, includeMarketStocks, includeDescriptions);
            InvasionFoodTerms? terms = _foodSupply.FindTerms(town);
            if (terms == null)
            {
                return result;
            }
            // The shortfall has to be read from the inner model: asking the campaign for the food model
            // would come straight back here.
            float supply = InvasionFoodSupportRules.DailySupply(result.ResultNumber, terms.Value.MinimumGarrison,
                NumberOfMenOnGarrisonToEatOneFood, terms.Value.TownAllowance);
            if (supply != 0f) result.Add(supply, InvasionSupplies, null!);
            return result;
        }
    }
}
