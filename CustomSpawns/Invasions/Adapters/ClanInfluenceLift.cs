using System;
using System.Reflection;
using NLog;
using TaleWorlds.CampaignSystem;

namespace CustomSpawns.Invasions.Adapters
{
    /**
     * Holds a clan's influence at a floor for the length of one call, then puts it back exactly.
     *
     * The influence is written to its field rather than through Clan.Influence: the property pays the clan
     * leader Steward experience for every point it lowers, and putting a lift back would otherwise reward
     * influence nobody spent. A game version without that field gets no lift at all, which leaves vanilla's
     * own rules in place rather than breaking them.
     */
    public sealed class ClanInfluenceLift
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private static readonly FieldInfo? InfluenceField =
            typeof(Clan).GetField("_influence", BindingFlags.Instance | BindingFlags.NonPublic);

        public ClanInfluenceLift()
        {
            if (InfluenceField == null)
            {
                Logger.Warn("Clan influence field not found: invasion clans keep vanilla's army influence rule");
            }
        }

        public T While<T>(Clan clan, float floor, Func<T> call)
        {
            float influence = clan.Influence;

            if (influence >= floor || InfluenceField == null)
            {
                return call();
            }

            InfluenceField.SetValue(clan, floor);

            try
            {
                return call();
            }
            finally
            {
                InfluenceField.SetValue(clan, influence);
            }
        }
    }
}
