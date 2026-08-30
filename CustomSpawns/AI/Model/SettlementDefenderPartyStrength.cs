namespace CustomSpawns.AI.Model
{
    internal readonly struct SettlementDefenderPartyStrength
    {
        public float Strength { get; }
        public bool IsActive { get; }
        public bool IsHostileToAttacker { get; }
        public bool IsVillager { get; }
        public bool IsCaravan { get; }
        public bool IsGarrison { get; }
        public bool IsMilitia { get; }
        public bool IsLordParty { get; }
        public bool IsSettlementFaction { get; }

        public SettlementDefenderPartyStrength(float strength, bool isActive = true,
            bool isHostileToAttacker = true, bool isVillager = false, bool isCaravan = false,
            bool isGarrison = false, bool isMilitia = false, bool isLordParty = true,
            bool isSettlementFaction = true)
        {
            Strength = strength;
            IsActive = isActive;
            IsHostileToAttacker = isHostileToAttacker;
            IsVillager = isVillager;
            IsCaravan = isCaravan;
            IsGarrison = isGarrison;
            IsMilitia = isMilitia;
            IsLordParty = isLordParty;
            IsSettlementFaction = isSettlementFaction;
        }
    }
}
