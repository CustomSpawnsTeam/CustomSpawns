namespace CustomSpawns.Invasions
{
    // Declares wars between kingdoms. Bannerlord access stays behind this boundary.
    public interface IInvasionKingdomWars
    {
        // Declares war between the two kingdoms, unless they are at war already.
        void DeclareWar(string kingdomId, string enemyKingdomId);
    }
}
