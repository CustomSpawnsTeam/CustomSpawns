using TaleWorlds.CampaignSystem;

namespace CustomSpawns.Diplomacy
{
    // The clans loaded into the campaign, looked up by id.
    public interface IClanFinder
    {
        Clan? FindClan(string clanId);
    }
}