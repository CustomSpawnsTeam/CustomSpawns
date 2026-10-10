using System;

namespace CustomSpawns.Invasions
{
    /**
     * The capital garrison read as the invasion's troop bank. Once the capital is held a wave of
     * reinforcements is paid into that garrison whole, and the lords of the invasion draw their warbands
     * back out of it. Nothing limits what the bank may hold: the faction strength ceiling is what stops
     * the waves coming, so the bank is only ever as large as that ceiling lets the invasion grow.
     *
     * The minimum garrison is a floor for everyone. It is the size the invasion's supplies are sized to
     * feed, so the men standing above it are the only ones that are ever spendable -- and the only ones
     * that ever starve.
     */
    internal static class InvasionTroopBank
    {
        // What a lord may take out: whatever stands above the floor, and never more than he can lead.
        internal static int Drawable(int wanted, int garrisonCount, int minimumGarrison) =>
            Math.Max(0, Math.Min(wanted, garrisonCount - minimumGarrison));
    }
}
