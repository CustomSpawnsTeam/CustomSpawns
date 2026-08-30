namespace CustomSpawns.AI.Model
{
    internal readonly struct RoamingPartyStrength
    {
        public bool IsSelf { get; }
        public bool IsActive { get; }
        public bool IsHostile { get; }
        public object? ArmyIdentity { get; }
        public float Distance { get; }
        public float PartyStrength { get; }

        public RoamingPartyStrength(bool isSelf, bool isActive, bool isHostile,
            object? armyIdentity, float distance, float partyStrength)
        {
            IsSelf = isSelf;
            IsActive = isActive;
            IsHostile = isHostile;
            ArmyIdentity = armyIdentity;
            Distance = distance;
            PartyStrength = partyStrength;
        }
    }
}
