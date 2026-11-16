using System.Collections.Generic;
using System.Linq;

namespace CustomSpawns.Invasions
{
    // The realm an invasion is conquering: the kingdom it first took a fief from, then each neighbour in turn.
    public sealed class InvasionConquest
    {
        private readonly HashSet<string> _warsLed;

        public InvasionConquest(string invasionClanId, string targetKingdomId, double startedDay,
            double lastFrontDay, string? leaderClanId = null, string? followerClanId = null,
            double? lastReconsideredDay = null, IEnumerable<string>? warsLed = null)
        {
            LeaderClanId = leaderClanId;
            FollowerClanId = followerClanId;
            InvasionClanId = invasionClanId;
            TargetKingdomId = targetKingdomId;
            StartedDay = startedDay;
            LastFrontDay = lastFrontDay;
            LastReconsideredDay = lastReconsideredDay ?? startedDay;
            _warsLed = new HashSet<string>(warsLed ?? Enumerable.Empty<string>());
        }

        public string InvasionClanId { get; }
        public string TargetKingdomId { get; private set; }

        // When the invasion began conquering its current target.
        public double StartedDay { get; private set; }

        // When the invasion last went to war on a kingdom of its own accord; its first conquest counts.
        public double LastFrontDay { get; private set; }

        // The invasion this one was launched to fight alongside; null for an invasion that came on its own.
        public string? LeaderClanId { get; }

        // The invasion this one last launched, as a kingdom fell; null until the first falls.
        public string? FollowerClanId { get; private set; }

        // When the invasion last weighed its target against its other neighbours; a new target counts.
        public double LastReconsideredDay { get; private set; }

        // The kingdoms whose war with the line began with this realm: it alone makes the line's peace with them.
        public IReadOnlyCollection<string> WarsLed => _warsLed;

        public void OpenFront(double today) => LastFrontDay = today;

        public void Launch(string followerClanId) => FollowerClanId = followerClanId;

        public void Reconsider(double today) => LastReconsideredDay = today;

        public void LeadWar(string kingdomId) => _warsLed.Add(kingdomId);

        public void EndWar(string kingdomId) => _warsLed.Remove(kingdomId);

        // The target has fallen, or another has become the better prey, and the invasion turns on it.
        public void Retarget(string targetKingdomId, double today)
        {
            TargetKingdomId = targetKingdomId;
            StartedDay = today;
            LastFrontDay = today;
            LastReconsideredDay = today;
        }
    }
}
