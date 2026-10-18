using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace CustomSpawns.Invasions.Adapters
{
    /**
     * Native army mutations needed by the capital objective. The domain decides when an army is ready;
     * this adapter only applies that decision to Bannerlord's called-party roster.
     */
    public sealed class BannerlordCapitalObjectiveArmy
    {
        public void ReleaseUnattachedInvitations(string commanderPartyId)
        {
            MobileParty? commander = MobileParty.All.FirstOrDefault(candidate =>
                string.Equals(candidate.StringId, commanderPartyId, StringComparison.Ordinal));

            if (commander == null)
            {
                return;
            }

            ReleaseUnattachedInvitations(commander);
        }

        public void ReleaseUnattachedInvitations(MobileParty commander)
        {
            Army? army = commander.Army;

            if (army == null || army.LeaderParty != commander)
            {
                return;
            }

            MobileParty[] unarrivedParties = army.Parties.Where(party =>
                    party != commander && party.AttachedTo != commander)
                .ToArray();
            foreach (MobileParty unarrivedParty in unarrivedParties)
            {
                unarrivedParty.Army = null;
            }
        }
    }
}
