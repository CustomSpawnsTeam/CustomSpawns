using System.Linq;
using TaleWorlds.CampaignSystem;

namespace CustomSpawns.Diplomacy
{
    public class BannerlordClanFinder : IClanFinder
    {
        public Clan? FindClan(string clanId)
        {
            return Clan.All.FirstOrDefault(candidate => candidate.StringId == clanId);
        }
    }
}