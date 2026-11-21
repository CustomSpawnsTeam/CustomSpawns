using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace CustomSpawns.Invasions.PlayerSparing.Adapters
{
    /**
     * Reads the player and a settlement's owner from the campaign. A clan under a mercenary contract has its
     * kingdom set like a vassal's, so a mercenary's kingdom counts as the player's too.
     */
    public sealed class BannerlordInvasionPlayerRealm : IInvasionPlayerRealm
    {
        public string? FindPlayerClanId() => Clan.PlayerClan?.StringId;

        public string? FindPlayerKingdomId() => Clan.PlayerClan?.Kingdom?.StringId;

        public string? FindOwnerClanId(string settlementId) => Settlement.Find(settlementId)?.OwnerClan?.StringId;

        public string? FindOwnerKingdomId(string settlementId) =>
            Settlement.Find(settlementId)?.OwnerClan?.Kingdom?.StringId;
    }
}
