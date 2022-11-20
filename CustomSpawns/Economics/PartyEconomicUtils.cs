using System;
using System.Collections.Generic;
using System.Linq;
using CustomSpawns.Utils;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Map;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace CustomSpawns.Economics
{
    public static class PartyEconomicUtils
    {

        public static void PartyReplenishFood(MobileParty mobileParty)
        {
            if (mobileParty is null)
            {
                return;
            }

            float remainingDaysBeforeStarving = mobileParty.TotalFoodAtInventory / Math.Max(-mobileParty.FoodChange, 1f);
            if (remainingDaysBeforeStarving >= 2f)
            {
                return;
            }

            List<Settlement> villageFoodProducers = Settlement.All
                .Where(s => s.IsVillage && (s.Village?.VillageType?.PrimaryProduction?.IsFood ?? false))
                .ToList();

            Settlement? localFoodProducerVillage = CampaignUtils.GetNearestSettlement(villageFoodProducers, new List<IMapPoint>(1)
            {
                mobileParty
            });

            ItemObject localFood;
            if (localFoodProducerVillage != null)
            {
                localFood = localFoodProducerVillage.Village.VillageType.PrimaryProduction;   
            }
            else
            {
                localFood = DefaultItems.Grain;
            }
            int neededFood = (int) Math.Ceiling(2f * Math.Max(-mobileParty.FoodChange, 1f));
            mobileParty.ItemRoster.AddToCounts(localFood, neededFood);
        }

    }
}
