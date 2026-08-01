using System;

namespace CustomSpawns.Diplomacy
{
    public class ForceNoKingdomScoreProvider
    {
        private readonly NonMercenaryCustomSpawnsFactionsProvider _factionsProvider;

        public ForceNoKingdomScoreProvider(NonMercenaryCustomSpawnsFactionsProvider factionsProvider)
        {
            _factionsProvider = factionsProvider;
        }

        public float GetJoinScore(string clanId, Func<float> wrappedScore)
        {
            return IsForceNoKingdom(clanId) ? float.MinValue : wrappedScore();
        }

        public float GetLeaveScore(string clanId, Func<float> wrappedScore)
        {
            return IsForceNoKingdom(clanId) ? float.MaxValue : wrappedScore();
        }

        private bool IsForceNoKingdom(string clanId)
        {
            return _factionsProvider.GetNonMercenaryCustomSpawnsFactions().Contains(clanId);
        }
    }
}
