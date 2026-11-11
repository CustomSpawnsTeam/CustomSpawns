using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace CustomSpawns.Invasions.Adapters
{
    // The kingdoms around a conquering invasion's realm, as the target and front rules weigh them.
    public sealed class BannerlordInvasionNeighbours
    {
        private readonly BannerlordInvasionLaunches _bannerlordInvasionLaunches;
        private readonly BannerlordInvasionRealms _bannerlordInvasionRealms;
        private readonly InvasionConquests _invasionConquests;

        public BannerlordInvasionNeighbours(BannerlordInvasionLaunches bannerlordInvasionLaunches,
            BannerlordInvasionRealms bannerlordInvasionRealms, InvasionConquests invasionConquests)
        {
            _bannerlordInvasionLaunches = bannerlordInvasionLaunches;
            _bannerlordInvasionRealms = bannerlordInvasionRealms;
            _invasionConquests = invasionConquests;
        }

        /**
         * The kingdoms holding a fortification next to one of the invader's, as vanilla draws a realm's borders,
         * with how many such fortifications each holds and whether it has a town a fleet could land in. An allied
         * invasion's realm is no neighbour to turn on: allies conquer together, never each other.
         */
        public IReadOnlyList<InvasionNeighbour> FindNeighbours(IFaction invader) =>
            invader.Fiefs
                .SelectMany(fief => fief.GetNeighborFortifications(MobileParty.NavigationType.All))
                .Distinct()
                .Where(settlement => settlement.MapFaction is Kingdom { IsEliminated: false } &&
                                     settlement.MapFaction != invader &&
                                     !_invasionConquests.AreAllied(_bannerlordInvasionRealms.FindInvasionClanId(invader),
                                         _bannerlordInvasionRealms.FindInvasionClanId(settlement.MapFaction)))
                .GroupBy(settlement => (Kingdom)settlement.MapFaction)
                .Select(border => new InvasionNeighbour(border.Key.StringId, border.Key.CurrentTotalStrength,
                    border.Count(), _bannerlordInvasionLaunches.HasCoast(border.Key), border.Key.IsAtWarWith(invader)))
                .ToList();

        // Every kingdom's fief, measured from whichever of the invader's fiefs lies nearest to it.
        public IEnumerable<InvasionPrey> FindPrey(IFaction invader)
        {
            List<Vec2> holdings = invader.Fiefs.Select(town => town.Settlement.GatePosition.ToVec2()).ToList();

            return Town.AllFiefs
                .Where(town => town.MapFaction is Kingdom { IsEliminated: false } && town.MapFaction != invader)
                .Select(town => new InvasionPrey(town.MapFaction.StringId,
                    holdings.Min(holding => holding.Distance(town.Settlement.GatePosition.ToVec2())),
                    town.MapFaction.IsAtWarWith(invader)))
                .ToList();
        }

        public string Format(IEnumerable<InvasionNeighbour> neighbours) =>
            string.Join(";", neighbours.Select(neighbour => neighbour.KingdomId + ":" +
                                                            neighbour.Strength.ToString("0") + ":border" +
                                                            neighbour.BorderFortifications +
                                                            (neighbour.IsCoastal ? ":coast" : "") +
                                                            (neighbour.IsAtWar ? ":war" : "")));
    }
}
