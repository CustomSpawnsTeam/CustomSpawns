using System;
using System.Linq;
using CustomSpawns.Invasions.Adapters;
using NLog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace CustomSpawns.Invasions.GarrisonArmies.Adapters
{
    /**
     * Sends an invasion realm's ruler out of a besieging army he does not lead, whether it is a garrison army or one
     * the AI raised. Vanilla puts a kingdom's ruler at the head of any siege camp he is in, and only the camp's head
     * orders the assault; a ruler following the army only escorts it, so the walls fall, the food runs out, and
     * nobody ever storms the place. Released, he either leads the camp and storms it himself, or leaves and hands the
     * camp back to the army's lord.
     */
    public sealed class InvasionGarrisonArmyRulerRelease
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly Func<InvasionState> _state;

        public InvasionGarrisonArmyRulerRelease(Func<InvasionState> state)
        {
            _state = state;
        }

        public void Release()
        {
            foreach (Kingdom kingdom in _state().Invasions.Select(invasion => invasion.FindLeadingClan()?.Kingdom)
                         .OfType<Kingdom>().Distinct().ToList())
            {
                foreach (Army army in kingdom.Armies.Where(army => army.LeaderParty?.BesiegedSettlement != null)
                             .ToList())
                {
                    Release(army);
                }
            }
        }

        private static void Release(Army army)
        {
            foreach (MobileParty ruler in army.Parties.Where(party => party != army.LeaderParty &&
                         party.LeaderHero is { IsKingdomLeader: true } && party.MapEvent == null).ToList())
            {
                ruler.Army = null;
                Logger.Info("Invasion army: ruler released leader=" + army.LeaderParty.LeaderHero?.StringId +
                            " ruler=" + ruler.LeaderHero.StringId + " party=" + ruler.StringId + " besieging=" +
                            army.LeaderParty.BesiegedSettlement?.StringId);
            }
        }
    }
}
