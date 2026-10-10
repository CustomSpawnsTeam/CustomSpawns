namespace CustomSpawns.AI.Model
{
    internal readonly struct RoamingPartyStrength
    {
        public bool IsSelf { get; }
        public bool IsActive { get; }
        public bool IsHostile { get; }
        public bool IsLordParty { get; }
        public bool IsFleeing { get; }
        public bool IsNonCombatant { get; }
        public float Distance { get; }
        public float PartyStrength { get; }

        public RoamingPartyStrength(bool isSelf, bool isActive, bool isHostile,
            float distance, float partyStrength,
            bool isLordParty = false, bool isFleeing = false, bool isNonCombatant = false)
        {
            IsSelf = isSelf;
            IsActive = isActive;
            IsHostile = isHostile;
            IsLordParty = isLordParty;
            IsFleeing = isFleeing;
            IsNonCombatant = isNonCombatant;
            Distance = distance;
            PartyStrength = partyStrength;
        }
    }
}
