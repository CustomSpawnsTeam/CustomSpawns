namespace CustomSpawns.AI.Model
{
    internal enum SettlementOffensivePhase
    {
        Idle = 0,
        Search = 1,
        Recon = 2,
        Approach = 3,
        Active = 4,

        // Committed to the target and holding near it while the rest of the force comes up.
        Gather = 5,
        Cooldown = 6
    }
}
