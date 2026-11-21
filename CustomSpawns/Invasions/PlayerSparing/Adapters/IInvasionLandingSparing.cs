using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace CustomSpawns.Invasions.PlayerSparing.Adapters
{
    // Whether a party may besiege a settlement, as the invasion it serves spares the player on its first landing.
    public interface IInvasionLandingSparing
    {
        // Always true for a party that serves no live invasion.
        bool MayBesiege(MobileParty party, Settlement settlement);
    }
}
