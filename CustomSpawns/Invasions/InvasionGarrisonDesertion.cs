using System;

namespace CustomSpawns.Invasions
{
    /**
     * Whether an invasion's garrison keeps the men it was sent. Vanilla sizes a garrison's wage budget and
     * its party size for a kingdom's own levies, and an invader's veterans cost several times as much, so a
     * garrison topped up from the capital would desert the moment it arrived and be topped up again the day
     * after. A garrison whose morale breaks still runs, as any other, and so does one its clan cannot pay.
     * The crowding vanilla takes off a party's morale is not counted against it: the capital banks the
     * invasion's reserve in its garrison, far past the party size limit, by design.
     */
    public sealed class InvasionGarrisonDesertion
    {
        // Vanilla's daily desertion from a garrison whose wages went unpaid.
        private const int UnpaidDesertionCap = 5;

        public bool KeepsTroops(bool isGarrison, bool isHeldByInvader, float morale, int membersOverLimit,
            int moraleThreshold) =>
            isGarrison && isHeldByInvader && morale + CrowdingPenalty(membersOverLimit) >= moraleThreshold;

        // How many of a kept garrison still desert today because its clan could not pay them.
        public int UnpaidDesertions(float unpaidWages, int healthyMembers, int regularMembers) =>
            unpaidWages > 0f ? Math.Min(Math.Min(UnpaidDesertionCap, healthyMembers), regularMembers) : 0;

        // Vanilla's morale penalty for a party over its size limit.
        private static float CrowdingPenalty(int membersOverLimit) =>
            membersOverLimit > 0 ? (float)Math.Sqrt(membersOverLimit) : 0f;
    }
}
