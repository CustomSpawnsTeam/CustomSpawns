using System.Linq;
using CustomSpawns.Invasions.Adapters;
using Moq;
using NUnit.Framework;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Settlements;

namespace CustomSpawns.Tests.Invasions
{
    public class InvasionSettlementFoodModelTests
    {
        [Test]
        public void Decorator_ForwardsAllPropertiesAndCalculationFlags()
        {
            var inner = new Mock<SettlementFoodModel>(MockBehavior.Strict);
            inner.SetupGet(model => model.FoodStocksUpperLimit).Returns(11);
            inner.SetupGet(model => model.NumberOfProsperityToEatOneFood).Returns(12);
            inner.SetupGet(model => model.NumberOfMenOnGarrisonToEatOneFood).Returns(13);
            inner.SetupGet(model => model.CastleFoodStockUpperLimitBonus).Returns(14);
            inner.Setup(model => model.CalculateTownFoodStocksChange(null!, false, true))
                .Returns(new ExplainedNumber(20, true));
            var model = new InvasionSettlementFoodModel(inner.Object, _ => 5f);

            Assert.That(model.FoodStocksUpperLimit, Is.EqualTo(11));
            Assert.That(model.NumberOfProsperityToEatOneFood, Is.EqualTo(12));
            Assert.That(model.NumberOfMenOnGarrisonToEatOneFood, Is.EqualTo(13));
            Assert.That(model.CastleFoodStockUpperLimitBonus, Is.EqualTo(14));

            ExplainedNumber result = model.CalculateTownFoodStocksChange(null!, false, true);

            Assert.That(result.ResultNumber, Is.EqualTo(25f));
            Assert.That(result.GetLines().Any(line => line.name.ToString() == "Overseas supplies"), Is.True);
            inner.Verify(model => model.CalculateTownFoodStocksChange(null!, false, true), Times.Once);
        }

        [Test]
        public void Decorator_RepeatedQueriesOnlyReadTheContribution()
        {
            var inner = new Mock<SettlementFoodModel>(MockBehavior.Strict);
            inner.Setup(model => model.CalculateTownFoodStocksChange(null!, true, false)).Returns(new ExplainedNumber(20));
            int contributionCalls = 0;
            var model = new InvasionSettlementFoodModel(inner.Object, _ =>
            {
                contributionCalls++;
                return 0f;
            });

            model.CalculateTownFoodStocksChange(null!, true, false);
            model.CalculateTownFoodStocksChange(null!, true, false);

            Assert.That(contributionCalls, Is.EqualTo(2));
            inner.Verify(model => model.CalculateTownFoodStocksChange(null!, true, false), Times.Exactly(2));
        }
    }
}
