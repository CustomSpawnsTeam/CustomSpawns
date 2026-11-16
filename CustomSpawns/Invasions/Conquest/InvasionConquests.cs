using System.Collections.Generic;
using System.Linq;
using CustomSpawns.Invasions.Alliances;

namespace CustomSpawns.Invasions.Conquest
{
    // Which kingdom each invasion conquers, which invasions it was launched with, and so who it is allied with.
    public sealed class InvasionConquests
    {
        private readonly Dictionary<string, InvasionConquest> _conquestsByClanId = new();

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

        /**
         * Whether two kingdoms, known by the invasion clan each belongs to or null for none, may ally or trade: other
         * kingdoms deal with each other, and an invasion's realm only with the realms of its own line.
         */
        public bool MayMakePact(string? firstClanId, string? secondClanId) =>
            (firstClanId == null && secondClanId == null) || AreAllied(firstClanId, secondClanId);

        public IEnumerable<InvasionConquest> FindAllies(InvasionConquest conquest) =>
            All.Where(other => AreAllied(conquest.InvasionClanId, other.InvasionClanId));

        // The whole line a conquest is of: its allies, theirs, and so on. Allies are named through a shared leader,
        // so a follower one realm of the line launched is that realm's ally alone, yet of the same line.
        public List<InvasionConquest> FindLine(InvasionConquest conquest)
        {
            var line = new List<InvasionConquest> { conquest };

            for (int i = 0; i < line.Count; i++)
            {
                line.AddRange(FindAllies(line[i]).Where(ally => !line.Contains(ally)).ToList());
            }

            return line;
        }

        /**
         * The allies a war on one realm of a line calls in: every other realm of that line, as a war with one of
         * them is a war with all of them. None when the enemy is a realm of the same line.
         */
        public IEnumerable<InvasionConquest> FindAlliesCalledToWar(string? invasionClanId,
            string? enemyInvasionClanId)
        {
            InvasionConquest? conquest = Find(invasionClanId);

            return conquest == null || AreAllied(invasionClanId, enemyInvasionClanId)
                ? Enumerable.Empty<InvasionConquest>()
                : FindAllies(conquest);
        }

        /**
         * A war a kingdom begins with a realm of the line, rather than one the line is called into, is that realm's
         * to lead: it alone makes the line's peace with that kingdom.
         */
        public void BeginWar(string? invasionClanId, string kingdomId)
        {
            InvasionConquest? conquest = Find(invasionClanId);

            if (conquest == null)
            {
                return;
            }

            foreach (InvasionConquest ally in FindAllies(conquest))
            {
                ally.EndWar(kingdomId);
            }

            conquest.LeadWar(kingdomId);
        }

        public void EndWar(string? invasionClanId, string kingdomId) => Find(invasionClanId)?.EndWar(kingdomId);

        /**
         * The realm leading the line's war with a kingdom: the one that war began with or, for a war that began
         * before the line kept count, the line's leader. Null when there is neither.
         */
        public InvasionConquest? FindWarLeader(InvasionConquest conquest, string kingdomId) =>
            FindAllies(conquest).Append(conquest).FirstOrDefault(realm => realm.WarsLed.Contains(kingdomId)) ??
            FindLeader(conquest);

        public void Restore(InvasionConquest conquest) => _conquestsByClanId[conquest.InvasionClanId] = conquest;

        // A loaded save replaces every conquest, so the adapters sharing this registry all see what it holds.
        public void Clear() => _conquestsByClanId.Clear();

        public void Forget(string invasionClanId) => _conquestsByClanId.Remove(invasionClanId);

        /**
         * A realm that conquers stays at war with its target and leaves every other kingdom to choose: they
         * fight it, or not, as they would any neighbour. Minor factions and bandits keep their configuration.
         * Two allied invasions never fight each other, whatever their configuration says.
         *
         * A line goes to war and makes peace as one: every realm of it stays at war with each of the line's
         * targets, and a realm makes no peace of its own with a kingdom whose war another realm of the line leads
         * and still fights. The leading realm makes that peace for all of them.
         */
        public InvasionWarStance FindStance(string? invasionClanId, string otherFactionId, bool otherIsKingdom,
            string? otherInvasionClanId = null, bool isWarLeaderAtWarWithOther = false)
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

            if (otherFactionId == conquest.TargetKingdomId ||
                FindAllies(conquest).Any(ally => ally.TargetKingdomId == otherFactionId))
            {
                return InvasionWarStance.War;
            }

            if (otherIsKingdom && isWarLeaderAtWarWithOther &&
                FindWarLeader(conquest, otherFactionId) is { } warLeader && warLeader != conquest)
            {
                return InvasionWarStance.War;
            }

            return otherIsKingdom ? InvasionWarStance.Open : InvasionWarStance.Policy;
        }
    }
}
