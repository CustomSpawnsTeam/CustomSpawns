using System;
using TaleWorlds.CampaignSystem;

namespace CustomSpawns.Data
{
    public static class DataUtils
    {
        public static float GetCurrentDynamicSpawnCoeff(float period)
        { 

            float cur = Campaign.Current.Models.CampaignTimeModel.CampaignStartTime.ElapsedDaysUntilNow * (2.9f / period);

            return Math.Max((cur * cur * cur) - 2 * (cur * cur) - (1 / 2) * cur + 1, 0);
        }
    }
}
