using System;

namespace CustomSpawns.Invasions
{
    // Asks the player whether their kingdom joins a coalition. Bannerlord access stays behind this boundary.
    public interface IInvasionCoalitionInvitations
    {
        // The kingdom the player rules and could bring into a coalition; null while they rule none.
        string? FindPlayerKingdomId();

        // Asks the player to join; whichever answer they choose is called back once.
        void AskToJoin(Action join, Action decline);
    }
}
