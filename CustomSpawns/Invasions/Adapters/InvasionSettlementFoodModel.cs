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
        private readonly SettlementFoodModel _inner;
        private readonly Func<Town, float> _contribution;

        internal InvasionSettlementFoodModel(SettlementFoodModel inner, Func<Town, float> contribution)
        {
            _inner = inner;
            _contribution = contribution;
        }

        public override int FoodStocksUpperLimit => _inner.FoodStocksUpperLimit;
        public override int NumberOfProsperityToEatOneFood => _inner.NumberOfProsperityToEatOneFood;
        public override int NumberOfMenOnGarrisonToEatOneFood => _inner.NumberOfMenOnGarrisonToEatOneFood;
        public override int CastleFoodStockUpperLimitBonus => _inner.CastleFoodStockUpperLimitBonus;

        public override ExplainedNumber CalculateTownFoodStocksChange(Town town, bool includeMarketStocks = true,
            bool includeDescriptions = false)
        {
            ExplainedNumber result = _inner.CalculateTownFoodStocksChange(town, includeMarketStocks, includeDescriptions);
            float contribution = _contribution(town);
            if (contribution != 0f) result.Add(contribution, OverseasSupplies, null!);
            return result;
        }
    }
}
