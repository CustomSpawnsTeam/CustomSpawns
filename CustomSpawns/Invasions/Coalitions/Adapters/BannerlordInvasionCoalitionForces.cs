using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace CustomSpawns.Invasions.Coalitions.Adapters
{
    /**
     * The coalition's free field forces, worked out once an hour: every lord of the coalition weighing a capital's
     * siege asks which of them are near enough to join it.
     */
    public sealed class BannerlordInvasionCoalitionForces
    {
        private readonly BannerlordInvasionCoalitionFronts _bannerlordInvasionCoalitionFronts;
        private List<(MobileParty Party, float Strength)> _forces = new();
        private int _hour = -1;

        public BannerlordInvasionCoalitionForces(BannerlordInvasionCoalitionFronts bannerlordInvasionCoalitionFronts)
        {
            _bannerlordInvasionCoalitionFronts = bannerlordInvasionCoalitionFronts;
        }

        /**
         * The strength of each of the coalition's forces in reach of the fief, but for the lord's own, its army's,
         * and those of the parties it counts already.
         */
        public IEnumerable<float> FindStrengthsAround(Settlement fief, MobileParty party,
            ICollection<MobileParty> counted, Func<float, bool> isInReach)
        {
            Refresh();
            return _forces.Where(force => force.Party != party && !counted.Contains(force.Party) &&
                                          (party.Army == null || force.Party.Army != party.Army) &&
                                          isInReach(force.Party.Position.Distance(fief.Position)))
                .Select(force => force.Strength);
        }

        private void Refresh()
        {
            int hour = (int)CampaignTime.Now.ToHours;

            if (hour == _hour)
            {
                return;
            }

            _hour = hour;
            _forces = MobileParty.AllLordParties.Where(IsFreeForce)
                .Select(force => (force, force.GetTotalLandStrengthWithFollowers()))
                .ToList();
        }

        // A lord, or the leader of its army, of the coalition, not held by a siege or at sea.
        private bool IsFreeForce(MobileParty party) =>
            party.IsActive && !party.IsMainParty && party.LeaderHero != null &&
            (party.Army == null || party.Army.LeaderParty == party) && party.BesiegedSettlement == null &&
            party.CurrentSettlement?.SiegeEvent == null && !party.IsCurrentlyAtSea &&
            _bannerlordInvasionCoalitionFronts.IsMember(party.MapFaction);
    }
}
