using CustomSpawns.Invasions;
using TaleWorlds.CampaignSystem;

namespace CustomSpawns.Diplomacy
{
    public interface IFactionWarStanceProvider
    {
        InvasionWarStance FindStance(IFaction first, IFaction second);

        // How much more the threatened faction wants war with the other. Zero when the other is no threat.
        float FindThreat(IFaction threatened, IFaction other);
    }
}
