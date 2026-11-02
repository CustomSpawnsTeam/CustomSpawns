using TaleWorlds.CampaignSystem;

namespace CustomSpawns.Invasions.Adapters
{
    // The living clan an invasion's realm, culture and wars are read from, once its founder may have died out.
    public static class InvasionLeadership
    {
        public static Clan? FindLeadingClan(this Invasion invasion)
        {
            string? leadingClanId = invasion.FindLeadingClan(
                clanId => FindClan(clanId)?.IsEliminated == false,
                clanId => FindClan(clanId)?.Kingdom?.RulingClan?.StringId);

            return leadingClanId == null ? null : FindClan(leadingClanId);
        }

        private static Clan? FindClan(string clanId) => Clan.FindFirst(clan => clan.StringId == clanId);
    }
}
