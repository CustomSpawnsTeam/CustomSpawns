using System.Collections.Generic;
using System.Linq;

namespace CustomSpawns.Invasions
{
    // Which kingdom each invasion conquers, which invasions it was launched with, and so who it is allied with.
    public sealed class InvasionConquests
    {
        private readonly Dictionary<string, InvasionConquest> _conquestsByClanId = new();

        // Never saved: the realms that see a kingdom fall all turn on their next target the same day.
        private readonly HashSet<string> _claimedFallenKingdomIds = new();

        public IReadOnlyCollection<InvasionConquest> All => _conquestsByClanId.Values;

        public InvasionConquest? Find(string? invasionClanId) =>
            invasionClanId != null && _conquestsByClanId.TryGetValue(invasionClanId, out InvasionConquest conquest)
                ? conquest
                : null;

        /**
         * The first fief an invasion takes from a kingdom names the realm it conquers; later conquests,
         * whoever they are taken from, leave that choice alone until that realm falls. A fief taken from a clan outside any kingdom
         * names nothing.
         */
        public InvasionConquest? Begin(string invasionClanId, string? previousOwnerKingdomId, double today)
        {
            if (previousOwnerKingdomId == null || _conquestsByClanId.ContainsKey(invasionClanId))
            {
                return null;
            }

            var conquest = new InvasionConquest(invasionClanId, previousOwnerKingdomId, today, today);
            _conquestsByClanId.Add(invasionClanId, conquest);
            return conquest;
        }

        /**
         * An invasion launched on behalf of another, each time a kingdom falls, conquers a kingdom of its own as an
         * ally of the one it was launched for; the launcher remembers the last it launched.
         */
        public InvasionConquest? BeginFollowing(string followerClanId, InvasionConquest leader,
            string? targetKingdomId, double today)
        {
            if (targetKingdomId == null || followerClanId == leader.InvasionClanId ||
                _conquestsByClanId.ContainsKey(followerClanId))
            {
                return null;
            }

            var conquest = new InvasionConquest(followerClanId, targetKingdomId, today, today,
                leader.InvasionClanId);
            _conquestsByClanId.Add(followerClanId, conquest);
            leader.Launch(followerClanId);
            return conquest;
        }

        // The invasion a follower was launched for, while that one still conquers.
        public InvasionConquest? FindLeader(InvasionConquest conquest) => Find(conquest.LeaderClanId);

        /**
         * Whoever launches the next follower when this conquest's target falls: the leader of its line, so every
         * follower is the ally of every other. With that leader gone, the conquest launches its own.
         */
        public InvasionConquest FindLauncher(InvasionConquest conquest) => FindLeader(conquest) ?? conquest;

        /**
         * The kingdom the newest realm of the line conquers, the follower its launcher last launched. Every older
         * realm of the line joins it there, however far it lies, so the newest opens the front and the others come
         * to help instead of each conquering alone. Null when the line has no follower that still conquers, or for
         * that follower.
         */
        public string? FindFrontToJoin(InvasionConquest conquest)
        {
            InvasionConquest? newest = Find(FindLauncher(conquest).FollowerClanId);

            return newest == null || newest == conquest ? null : newest.TargetKingdomId;
        }

        /**
         * Realms of one line that conquer a kingdom together all see it fall on the same day, yet it opens only one
         * new realm: the first to claim the fall launches the follower. False once the fall has been claimed.
         */
        public bool ClaimFall(string kingdomId) => _claimedFallenKingdomIds.Add(kingdomId);

        /**
         * A leader and the followers launched for it, any two of them, while both still conquer. Followers of one
         * leader stay allied with each other even once that leader has fallen.
         */
        public bool AreAllied(string? firstClanId, string? secondClanId)
        {
            InvasionConquest? first = Find(firstClanId);
            InvasionConquest? second = Find(secondClanId);

            return first != null && second != null && first != second &&
                   (first.LeaderClanId == second.InvasionClanId || second.LeaderClanId == first.InvasionClanId ||
                    (first.LeaderClanId != null && first.LeaderClanId == second.LeaderClanId));
        }

        public IEnumerable<InvasionConquest> FindAllies(InvasionConquest conquest) =>
            All.Where(other => AreAllied(conquest.InvasionClanId, other.InvasionClanId));

        public void Restore(InvasionConquest conquest) => _conquestsByClanId[conquest.InvasionClanId] = conquest;

        // A loaded save replaces every conquest, so the adapters sharing this registry all see what it holds.
        public void Clear()
        {
            _conquestsByClanId.Clear();
            _claimedFallenKingdomIds.Clear();
        }

        public void Forget(string invasionClanId) => _conquestsByClanId.Remove(invasionClanId);

        /**
         * A realm that conquers stays at war with its target and leaves every other kingdom to choose: they
         * fight it, or not, as they would any neighbour. Minor factions and bandits keep their configuration.
         * Two allied invasions never fight each other, whatever their configuration says.
         */
        public InvasionWarStance FindStance(string? invasionClanId, string otherFactionId, bool otherIsKingdom,
            string? otherInvasionClanId = null)
        {
            if (AreAllied(invasionClanId, otherInvasionClanId))
            {
                return InvasionWarStance.Peace;
            }

            InvasionConquest? conquest = Find(invasionClanId);

            if (conquest == null)
            {
                return InvasionWarStance.Policy;
            }

            if (otherFactionId == conquest.TargetKingdomId)
            {
                return InvasionWarStance.War;
            }

            return otherIsKingdom ? InvasionWarStance.Open : InvasionWarStance.Policy;
        }
    }
}
