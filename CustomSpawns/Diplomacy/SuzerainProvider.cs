using TaleWorlds.CampaignSystem;

namespace CustomSpawns.Diplomacy
{
    public class SuzerainProvider : ISuzerainProvider
    {
        public bool IsVassal(IFaction faction)
        {
            return faction.MapFaction is Kingdom;
        }

        public IFaction? GetSuzerain(IFaction faction)
        {
            return IsVassal(faction) ? faction.MapFaction : null;
        }
    }
}