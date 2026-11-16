using CustomSpawns.Invasions;
using TaleWorlds.CampaignSystem;

namespace CustomSpawns.Diplomacy
{
    public interface IFactionWarStanceProvider
    {
        InvasionWarStance FindStance(IFaction first, IFaction second);

        // How much more the threatened faction wants war with the other. Zero when the other is no threat.
        float FindThreat(IFaction threatened, IFaction other);

        /**
         * How much two factions banded together against an invader want peace with each other, and so how far a war
         * between them is ruled out. Zero when no coalition holds them both.
         */
        float FindCoalitionThreat(IFaction first, IFaction second);

        // Whether one of the factions stands in a coalition against the other's line, which no peace ends.
        bool IsAtCoalitionWar(IFaction first, IFaction second);
    }
}
