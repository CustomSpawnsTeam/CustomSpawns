namespace CustomSpawns.AI.Offensives
{
    public sealed class ForceRatioRules
    {
        public bool Meets(float friendlyStrength, float enemyStrength, float minimumRatio)
        {
            return enemyStrength <= 0f || friendlyStrength / enemyStrength >= minimumRatio;
        }
    }
}
