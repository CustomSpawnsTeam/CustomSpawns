using System;

namespace CustomSpawns.Invasions
{
    /**
     * What an invasion's banked reserve eats from the town it is banked in: nothing. The capital banks the
     * invasion's reserve in its garrison, far past the party size limit, and vanilla feeds every one of those men
     * from the town's stocks, one food a day for every twenty. A bank of 3,500 ate 178 a day, which no town
     * covers: the town starved, the starving garrison lay wounded and its loyalty fell, until the militia
     * outnumbered what was left standing and the town rebelled, taking the whole reserve with it. The men within
     * the limit eat as any garrison does; the reserve beyond it lives off the invasion's own supply train.
     */
    public sealed class InvasionBankRations
    {
        public float FoodSpared(bool isHeldByInvader, int garrisonMembers, int partySizeLimit, int menToEatOneFood) =>
            isHeldByInvader && menToEatOneFood > 0
                ? (float)Math.Max(0, garrisonMembers - partySizeLimit) / menToEatOneFood
                : 0f;
    }
}
