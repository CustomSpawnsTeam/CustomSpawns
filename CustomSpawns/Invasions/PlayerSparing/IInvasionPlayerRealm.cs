namespace CustomSpawns.Invasions.PlayerSparing
{
    // Who the player is and who holds a settlement, as the invasion's first landing and first target see it.
    // Bannerlord access stays behind this boundary.
    public interface IInvasionPlayerRealm
    {
        string? FindPlayerClanId();

        // The kingdom the player rules or serves, as a vassal or as a mercenary alike; null for a clan of its own.
        string? FindPlayerKingdomId();

        string? FindOwnerClanId(string settlementId);

        // The kingdom the settlement's owner rules or serves; null for an owner outside any kingdom.
        string? FindOwnerKingdomId(string settlementId);
    }
}
