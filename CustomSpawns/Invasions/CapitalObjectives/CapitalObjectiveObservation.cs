namespace CustomSpawns.Invasions.CapitalObjectives
{
    public readonly struct CapitalObjectiveObservation
    {
        public CapitalObjectiveObservation(double nowDays, bool ownsDesignatedCapital,
            bool designatedCapitalIsBesieged, bool commanderIsEligible,
            bool targetHasMatchingKingdomSiege, bool targetIsOwnedByInvasion,
            CapitalDefenceForces defenceForces, CapitalAttackForces attackForces,
            string? designatedCapitalSiegeEpisodeId = null)
        {
            NowDays = nowDays;
            OwnsDesignatedCapital = ownsDesignatedCapital;
            DesignatedCapitalIsBesieged = designatedCapitalIsBesieged;
            CommanderIsEligible = commanderIsEligible;
            TargetHasMatchingKingdomSiege = targetHasMatchingKingdomSiege;
            TargetIsOwnedByInvasion = targetIsOwnedByInvasion;
            DefenceForces = defenceForces;
            AttackForces = attackForces;
            DesignatedCapitalSiegeEpisodeId = designatedCapitalSiegeEpisodeId;
        }

        public double NowDays { get; }
        public bool OwnsDesignatedCapital { get; }
        public bool DesignatedCapitalIsBesieged { get; }
        public bool CommanderIsEligible { get; }
        public bool TargetHasMatchingKingdomSiege { get; }
        public bool TargetIsOwnedByInvasion { get; }
        public CapitalDefenceForces DefenceForces { get; }
        public CapitalAttackForces AttackForces { get; }
        public string? DesignatedCapitalSiegeEpisodeId { get; }
    }
}
