using System.Collections.Generic;
using System.Linq;
using CustomSpawns.Invasions.Adapters;
using CustomSpawns.Invasions.PlayerSparing;
using NLog;
using TaleWorlds.CampaignSystem;

namespace CustomSpawns.Invasions.Conquest.Adapters
{
    /**
     * Turns a conquering invasion on another kingdom, once its target falls or as borders move. Until its line has
     * beaten a kingdom it never turns on the player's.
     */
    public sealed class BannerlordInvasionRetargets
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly BannerlordInvasionNeighbours _bannerlordInvasionNeighbours;
        private readonly InvasionConquests _invasionConquests;
        private readonly InvasionTargetChoice _invasionTargetChoice;
        private readonly BannerlordInvasionConfigurations _bannerlordInvasionConfigurations;
        private readonly InvasionPlayerSparing _invasionPlayerSparing;

        public BannerlordInvasionRetargets(BannerlordInvasionNeighbours bannerlordInvasionNeighbours,
            InvasionConquests invasionConquests, InvasionTargetChoice invasionTargetChoice,
            BannerlordInvasionConfigurations bannerlordInvasionConfigurations,
            InvasionPlayerSparing invasionPlayerSparing)
        {
            _bannerlordInvasionNeighbours = bannerlordInvasionNeighbours;
            _invasionConquests = invasionConquests;
            _invasionTargetChoice = invasionTargetChoice;
            _bannerlordInvasionConfigurations = bannerlordInvasionConfigurations;
            _invasionPlayerSparing = invasionPlayerSparing;
        }

        /**
         * Turns a conquest whose target has fallen on the next; false while no kingdom borders the invasion. The new
         * realm a fall opens is landed by BannerlordInvasionLineRefills.
         */
        public bool Retarget(Invasion invasion, InvasionConquest conquest)
        {
            IFaction? invader = invasion.FindLeadingClan()?.MapFaction;
            IReadOnlyList<InvasionNeighbour> neighbours = invader == null
                ? new InvasionNeighbour[0]
                : FindCandidates(invasion, invader);
            string? kingdomId = _invasionTargetChoice.FindNextTarget(neighbours, FindAlliedTargets(conquest));

            if (kingdomId == null)
            {
                Logger.Info("Invasion conquest: no neighbour to turn on spawn=" + invasion.SpawnTemplateId +
                            " fallen=" + conquest.TargetKingdomId + " day=" + CampaignTime.Now.ToDays);
                return false;
            }

            Logger.Info("Invasion conquest: retargeted spawn=" + invasion.SpawnTemplateId +
                        " fallen=" + conquest.TargetKingdomId + " target=" + kingdomId +
                        " fiefs=" + invader!.Fiefs.Count + " neighbours=[" +
                        _bannerlordInvasionNeighbours.Format(neighbours) + "] day=" + CampaignTime.Now.ToDays);
            conquest.Retarget(kingdomId, CampaignTime.Now.ToDays);
            return true;
        }

        // Turns a conquest on a neighbour that has become a much better prey than its target, as wars move borders.
        public void Reconsider(Invasion invasion, InvasionConquest conquest, InvasionConquestRules rules)
        {
            IFaction? invader = invasion.FindLeadingClan()?.MapFaction;

            if (invader == null || rules.ReconsiderTargetIntervalInDays <= 0f)
            {
                return;
            }

            IReadOnlyList<InvasionNeighbour> neighbours = FindCandidates(invasion, invader);
            string previousTargetId = conquest.TargetKingdomId;
            string? kingdomId = _invasionTargetChoice.Reconsider(conquest, rules, neighbours,
                FindAlliedTargets(conquest), CampaignTime.Now.ToDays);

            if (kingdomId == null)
            {
                return;
            }

            conquest.Retarget(kingdomId, CampaignTime.Now.ToDays);
            Logger.Debug("Invasion conquest: reconsidered spawn=" + invasion.SpawnTemplateId + " from=" +
                        previousTargetId + " target=" + kingdomId + " fiefs=" + invader.Fiefs.Count +
                        " neighbours=[" + _bannerlordInvasionNeighbours.Format(neighbours) + "] day=" +
                        CampaignTime.Now.ToDays);
        }

        private IReadOnlyList<InvasionNeighbour> FindCandidates(Invasion invasion, IFaction invader) =>
            _invasionPlayerSparing.FindTargetCandidates(
                _bannerlordInvasionConfigurations.FindLineTemplateId(invasion.SpawnTemplateId),
                _bannerlordInvasionNeighbours.FindNeighbours(invader));

        private IEnumerable<string> FindAlliedTargets(InvasionConquest conquest) =>
            _invasionConquests.FindAllies(conquest).Select(ally => ally.TargetKingdomId).ToList();
    }
}
