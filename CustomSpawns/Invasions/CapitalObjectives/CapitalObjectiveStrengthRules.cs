using CustomSpawns.AI.Offensives;

namespace CustomSpawns.Invasions.CapitalObjectives
{
    public readonly struct CapitalDefenceForces
    {
        public CapitalDefenceForces(float assembledFieldStrength, float garrisonAndOccupantStrength,
            float besiegerAndNearbyHostileStrength)
        {
            AssembledFieldStrength = assembledFieldStrength;
            GarrisonAndOccupantStrength = garrisonAndOccupantStrength;
            BesiegerAndNearbyHostileStrength = besiegerAndNearbyHostileStrength;
        }

        public float AssembledFieldStrength { get; }
        public float GarrisonAndOccupantStrength { get; }
        public float BesiegerAndNearbyHostileStrength { get; }
    }

    public readonly struct CapitalAttackForces
    {
        public CapitalAttackForces(float assembledFieldStrength, float settlementDefenderStrength,
            float nearbyHostileStrength)
        {
            AssembledFieldStrength = assembledFieldStrength;
            SettlementDefenderStrength = settlementDefenderStrength;
            NearbyHostileStrength = nearbyHostileStrength;
        }

        public float AssembledFieldStrength { get; }
        public float SettlementDefenderStrength { get; }
        public float NearbyHostileStrength { get; }
    }

    public sealed class CapitalObjectiveStrengthRules
    {
        private readonly ForceRatioRules _forceRatioRules;

        public CapitalObjectiveStrengthRules(ForceRatioRules forceRatioRules)
        {
            _forceRatioRules = forceRatioRules;
        }

        public bool CanDefend(CapitalDefenceForces forces, float minimumRoamingRatio)
        {
            float friendlyStrength = forces.AssembledFieldStrength + forces.GarrisonAndOccupantStrength;
            return _forceRatioRules.Meets(friendlyStrength, forces.BesiegerAndNearbyHostileStrength,
                minimumRoamingRatio);
        }

        public bool CanAttack(CapitalAttackForces forces, float minimumTargetRatio,
            float minimumRoamingRatio)
        {
            return _forceRatioRules.Meets(forces.AssembledFieldStrength,
                       forces.SettlementDefenderStrength, minimumTargetRatio) &&
                   _forceRatioRules.Meets(forces.AssembledFieldStrength,
                       forces.NearbyHostileStrength, minimumRoamingRatio);
        }
    }
}
