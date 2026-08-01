using System;

namespace CustomSpawns.Diplomacy
{
    public class ForceNoKingdomScoreProvider
    {
        internal const float ForcedJoinScore = -1_000_000_000f;
        internal const float ForcedLeaveScore = 1_000_000_000f;
        private readonly NonMercenaryCustomSpawnsFactionsProvider _factionsProvider;

        public ForceNoKingdomScoreProvider(NonMercenaryCustomSpawnsFactionsProvider factionsProvider)
        {
            _factionsProvider = factionsProvider;
        }

        public float GetJoinScore(string clanId, Func<float> wrappedScore)
        {
            return IsForceNoKingdom(clanId) ? ForcedJoinScore : wrappedScore();
        }

        public float GetLeaveScore(string clanId, Func<float> wrappedScore)
        {
            return IsForceNoKingdom(clanId) ? ForcedLeaveScore : wrappedScore();
        }

        private bool IsForceNoKingdom(string clanId)
        {
            return _factionsProvider.GetNonMercenaryCustomSpawnsFactions().Contains(clanId);
        }
    }
}
