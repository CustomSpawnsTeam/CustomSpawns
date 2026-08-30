using CustomSpawns.Data.Adapter;
using TaleWorlds.CampaignSystem.Settlements;

namespace CustomSpawns.AI.Model
{
    internal readonly struct SiegeTargetCandidate
    {
        public Settlement Target { get; }
        public bool IsActive { get; }
        public bool IsHostile { get; }
        public SpawnSettlementType Type { get; }
        public bool HasCompatibleSiege { get; }
        public float Distance { get; }
        public float DefenderStrength { get; }

        public SiegeTargetCandidate(Settlement target, bool isActive, bool isHostile,
            SpawnSettlementType type, bool hasCompatibleSiege, float distance, float defenderStrength)
        {
            Target = target;
            IsActive = isActive;
            IsHostile = isHostile;
            Type = type;
            HasCompatibleSiege = hasCompatibleSiege;
            Distance = distance;
            DefenderStrength = defenderStrength;
        }
    }
}
