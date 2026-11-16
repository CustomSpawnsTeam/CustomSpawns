using System.Collections.Generic;
using CustomSpawns.Invasions.Alliances;

namespace CustomSpawns.Invasions.Coalitions
{
    /**
     * The kingdoms of a coalition stand together as the realms of an invasion line do: every two of them are kept
     * at peace and allied while the coalition holds. The player's kingdom is asked once whether it joins, and is
     * allied with the rest only if it does. Once the line is destroyed the coalition breaks up: the alliances are
     * no longer renewed and lapse, and the player's answer is forgotten.
     */
    public sealed class InvasionCoalitionAlliances
    {
        private readonly IInvasionCoalitionMembers _invasionCoalitionMembers;
        private readonly IInvasionKingdomAlliances _invasionKingdomAlliances;
        private readonly IInvasionCoalitionInvitations _invasionCoalitionInvitations;
        private readonly InvasionCoalitionPlayerChoice _invasionCoalitionPlayerChoice;

        public InvasionCoalitionAlliances(IInvasionCoalitionMembers invasionCoalitionMembers,
            IInvasionKingdomAlliances invasionKingdomAlliances,
            IInvasionCoalitionInvitations invasionCoalitionInvitations,
            InvasionCoalitionPlayerChoice invasionCoalitionPlayerChoice)
        {
            _invasionCoalitionMembers = invasionCoalitionMembers;
            _invasionKingdomAlliances = invasionKingdomAlliances;
            _invasionCoalitionInvitations = invasionCoalitionInvitations;
            _invasionCoalitionPlayerChoice = invasionCoalitionPlayerChoice;
        }

        public void KeepAlliances()
        {
            IReadOnlyList<string> members = _invasionCoalitionMembers.FindCoalitionMembers();

            if (members.Count == 0)
            {
                _invasionCoalitionPlayerChoice.Forget();
                return;
            }

            InvitePlayer();

            for (int index = 0; index < members.Count; index++)
            {
                for (int other = index + 1; other < members.Count; other++)
                {
                    _invasionKingdomAlliances.Ally(members[index], members[other]);
                }
            }
        }

        // A player who joins is allied with the coalition at once, not at the next day's renewal.
        private void InvitePlayer()
        {
            if (!_invasionCoalitionPlayerChoice.ShouldAsk || _invasionCoalitionInvitations.FindPlayerKingdomId() == null)
            {
                return;
            }

            _invasionCoalitionPlayerChoice.Ask();
            _invasionCoalitionInvitations.AskToJoin(() =>
            {
                _invasionCoalitionPlayerChoice.Join();
                KeepAlliances();
            }, _invasionCoalitionPlayerChoice.Decline);
        }
    }
}
