namespace CustomSpawns.AI.Model
{
    internal readonly struct RoamingPartyStrength
    {
        public bool IsSelf { get; }
        public bool IsActive { get; }
        public bool IsHostile { get; }
        public object? ArmyIdentity { get; }
        public bool IsLordParty { get; }
        public float Distance { get; }
        public float PartyStrength { get; }

        public RoamingPartyStrength(bool isSelf, bool isActive, bool isHostile,
            object? armyIdentity, float distance, float partyStrength,
            bool isLordParty = false)
        {
            IsSelf = isSelf;
            IsActive = isActive;
            IsHostile = isHostile;
            ArmyIdentity = armyIdentity;
            IsLordParty = isLordParty;
            Distance = distance;
            PartyStrength = partyStrength;
        }
    }
}
