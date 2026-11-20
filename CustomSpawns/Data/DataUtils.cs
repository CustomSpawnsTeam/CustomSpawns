using System;
using TaleWorlds.CampaignSystem;

namespace CustomSpawns.Data
{
    public static class DataUtils
    {
        public static float GetCurrentDynamicSpawnCoeff(float period)
        {
            return DynamicSpawnCoeff(Campaign.Current.Models.CampaignTimeModel.CampaignStartTime.ElapsedDaysUntilNow, period);
        }

        // The curve repeats every period. Left unwrapped, its cubic term grows without bound and turns a
        // fraction-of-a-percent spawn chance into a near-certain one a few hundred days in.
        public static float DynamicSpawnCoeff(float elapsedDays, float period)
        {
            float cur = elapsedDays % period * (2.9f / period);

            return Math.Max((cur * cur * cur) - 2 * (cur * cur) - (1 / 2) * cur + 1, 0);
        }
    }
}
