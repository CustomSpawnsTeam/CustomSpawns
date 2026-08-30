namespace CustomSpawns.AI.Model
{
    internal readonly struct RaidTargetCandidate<T>
    {
        public T Target { get; }
        public bool IsValid { get; }
        public float Distance { get; }
        public float DefenderStrength { get; }

        public RaidTargetCandidate(T target, bool isValid, float distance, float defenderStrength)
        {
            Target = target;
            IsValid = isValid;
            Distance = distance;
            DefenderStrength = defenderStrength;
        }
    }
}
