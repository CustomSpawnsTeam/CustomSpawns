using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace CustomSpawns.Invasions.Reinforcements.Adapters
{
    public sealed class BannerlordInvasionCommissariat : IInvasionCommissariat
    {
        public int FindMenPerFoodPerDay() =>
            Campaign.Current.Models.MobilePartyFoodConsumptionModel.NumberOfMenOnMapToEatOneFood;

        public float FindMinimumFoodDaysForArmy() =>
            Campaign.Current.Models.ArmyManagementCalculationModel.MinimumNeededFoodInDaysToCallToArmy;

        public void AddFood(string partyId, int food) =>
            Campaign.Current.CampaignObjectManager.Find<MobileParty>(partyId)?.ItemRoster.AddToCounts(DefaultItems.Grain, food);
    }
}
