using TaleWorlds.CampaignSystem;

namespace CustomSpawns.Diplomacy
{
    public class ConstantWarFactionDiplomacyProvider : IFactionDiplomacyProvider
    {
        public bool IsAtWar(IFaction attacker, IFaction warTarget)
        {
            return FactionManager.IsAtWarAgainstFaction(attacker, warTarget);
        }
    }
}
