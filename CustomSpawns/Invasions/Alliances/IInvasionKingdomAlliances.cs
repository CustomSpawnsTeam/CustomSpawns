namespace CustomSpawns.Invasions.Alliances
{
    // Makes alliances between kingdoms. Bannerlord access stays behind this boundary.
    public interface IInvasionKingdomAlliances
    {
        // Ends any war between the two kingdoms and allies them, unless they are allied already.
        void Ally(string kingdomId, string otherKingdomId);
    }
}
