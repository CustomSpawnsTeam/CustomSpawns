using TaleWorlds.CampaignSystem;

namespace CustomSpawns.Diplomacy
{
    public interface IFactionDiplomacyProvider
    {
        bool IsAtWar(IFaction attacker, IFaction warTarget);
    }
}