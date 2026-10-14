using CustomSpawns.Data.Adapter;
using TaleWorlds.CampaignSystem.Settlements;

namespace CustomSpawns.AI.Model
{
    public readonly struct SiegeTargetCandidate
    {
        public Settlement Target { get; }
        public bool IsActive { get; }
        public bool IsHostile { get; }
        public SpawnSettlementType Type { get; }
        public bool HasCompatibleSiege { get; }
        public float Distance { get; }
        public float DefenderStrength { get; }
        public string TargetId { get; }
        public bool HasPort { get; }

        public SiegeTargetCandidate(Settlement target, bool isActive, bool isHostile,
            SpawnSettlementType type, bool hasCompatibleSiege, float distance, float defenderStrength,
            string targetId = "", bool hasPort = false)
        {
            Target = target;
            IsActive = isActive;
            IsHostile = isHostile;
            Type = type;
            HasCompatibleSiege = hasCompatibleSiege;
            Distance = distance;
            DefenderStrength = defenderStrength;
            TargetId = targetId;
            HasPort = hasPort;
        }
    }
}
