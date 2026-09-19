using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;

namespace CustomSpawns.Invasions.Adapters
{
    internal sealed class InvasionSettlementFoodModel : SettlementFoodModel
    {
        private static readonly TextObject OverseasSupplies = new TextObject("{=CustomSpawnsOverseasSupplies}Overseas supplies");
        private readonly SettlementFoodModel _settlementFoodModel;
        private readonly Func<Town, float> _contribution;

        internal InvasionSettlementFoodModel(SettlementFoodModel inner, Func<Town, float> contribution)
        {
            _settlementFoodModel = inner;
            _contribution = contribution;
        }

        public override int FoodStocksUpperLimit => _settlementFoodModel.FoodStocksUpperLimit;
        public override int NumberOfProsperityToEatOneFood => _settlementFoodModel.NumberOfProsperityToEatOneFood;
        public override int NumberOfMenOnGarrisonToEatOneFood => _settlementFoodModel.NumberOfMenOnGarrisonToEatOneFood;
        public override int CastleFoodStockUpperLimitBonus => _settlementFoodModel.CastleFoodStockUpperLimitBonus;

        public override ExplainedNumber CalculateTownFoodStocksChange(Town town, bool includeMarketStocks = true,
            bool includeDescriptions = false)
        {
            ExplainedNumber result = _settlementFoodModel.CalculateTownFoodStocksChange(town, includeMarketStocks, includeDescriptions);
            float contribution = _contribution(town);
            if (contribution != 0f) result.Add(contribution, OverseasSupplies, null!);
            return result;
        }
    }
}
