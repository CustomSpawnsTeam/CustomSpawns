using TaleWorlds.CampaignSystem;

namespace CustomSpawns.Diplomacy
{
    public interface ISuzerainProvider
    {
        bool IsVassal(IFaction warTarget);
        
        IFaction? GetSuzerain(IFaction clan);
    }
}