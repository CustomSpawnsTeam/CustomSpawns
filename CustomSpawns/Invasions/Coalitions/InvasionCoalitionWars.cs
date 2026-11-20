using System.Collections.Generic;
using CustomSpawns.Invasions.Alliances;

namespace CustomSpawns.Invasions.Coalitions
{
    /**
     * Every kingdom of a coalition is kept at war with each realm of the line it stands against, until the line is
     * destroyed: one too far away to want the war, or one a realm of the line made peace with before the coalition
     * formed, is drawn back into it.
     */
    public sealed class InvasionCoalitionWars
    {
        private readonly IInvasionCoalitionMembers _invasionCoalitionMembers;
        private readonly IInvasionKingdomWars _invasionKingdomWars;

        public InvasionCoalitionWars(IInvasionCoalitionMembers invasionCoalitionMembers,
            IInvasionKingdomWars invasionKingdomWars)
        {
            _invasionCoalitionMembers = invasionCoalitionMembers;
            _invasionKingdomWars = invasionKingdomWars;
        }

        public void KeepWars()
        {
            IReadOnlyList<string> invaders = _invasionCoalitionMembers.FindInvaders();

            foreach (string member in _invasionCoalitionMembers.FindCoalitionMembers())
            {
                foreach (string invader in invaders)
                {
                    _invasionKingdomWars.DeclareWar(member, invader);
                }
            }
        }
    }
}
