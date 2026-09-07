namespace CustomSpawns.AI.Model
{
    internal readonly struct RoamingPartyStrength
    {
        public bool IsSelf { get; }
        public bool IsActive { get; }
        public bool IsHostile { get; }
        public object? ArmyIdentity { get; }
        public bool IsLordParty { get; }
        public bool IsFleeing { get; }
        public float Distance { get; }
        public float PartyStrength { get; }

        public RoamingPartyStrength(bool isSelf, bool isActive, bool isHostile,
            object? armyIdentity, float distance, float partyStrength,
            bool isLordParty = false, bool isFleeing = false)
        {
            IsSelf = isSelf;
            IsActive = isActive;
            IsHostile = isHostile;
            ArmyIdentity = armyIdentity;
            IsLordParty = isLordParty;
            IsFleeing = isFleeing;
            Distance = distance;
            PartyStrength = partyStrength;
        }
    }
}
