using System.Reflection;
using NLog;
using TaleWorlds.CampaignSystem;

namespace CustomSpawns.Invasions.Adapters
{
    /**
     * Turns a clan into a band of mercenaries, as the game's minor factions are. Vanilla only hires minor
     * factions as mercenaries and destroys any other clan left without a kingdom, so both flags have to be
     * set. The game exposes no setter for them; both are saved with the clan, so this is done once.
     */
    public sealed class InvasionMercenaryClans
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private static readonly PropertyInfo? IsMinorFaction = typeof(Clan).GetProperty(nameof(Clan.IsMinorFaction));
        private static readonly PropertyInfo? IsClanTypeMercenary =
            typeof(Clan).GetProperty(nameof(Clan.IsClanTypeMercenary));

        public void Hire(Clan clan)
        {
            if (IsMinorFaction?.CanWrite != true || IsClanTypeMercenary?.CanWrite != true)
            {
                Logger.Warn("Invasion mercenary clan: clan=" + clan.StringId + " flags=unavailable");
                return;
            }

            IsMinorFaction.SetValue(clan, true);
            IsClanTypeMercenary.SetValue(clan, true);
        }
    }
}
