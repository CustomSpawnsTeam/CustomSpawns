namespace CustomSpawns.AI.Model
{
    /**
     * A party of an attacking force as the target sees it: how far it still is from the gate, how far it
     * can see, and what it would bring to an assault.
     */
    internal readonly struct AttackingPartyStrength
    {
        public float DistanceToTarget { get; }
        public float SeeingRange { get; }
        public float PartyStrength { get; }

        public AttackingPartyStrength(float distanceToTarget, float seeingRange,
            float partyStrength)
        {
            DistanceToTarget = distanceToTarget;
            SeeingRange = seeingRange;
            PartyStrength = partyStrength;
        }
    }
}
