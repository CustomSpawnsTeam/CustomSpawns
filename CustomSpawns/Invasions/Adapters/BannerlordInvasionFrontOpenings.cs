using NLog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

namespace CustomSpawns.Invasions.Adapters
{
    // Has a conquering invasion that can hold what it has declare war on the nearest kingdom it is at peace with.
    public sealed class BannerlordInvasionFrontOpenings
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly BannerlordInvasionFiefGarrisons _bannerlordInvasionFiefGarrisons;
        private readonly BannerlordInvasionNeighbours _bannerlordInvasionNeighbours;
        private readonly BannerlordInvasionRealms _bannerlordInvasionRealms;
        private readonly InvasionConquestFronts _invasionConquestFronts;

        public BannerlordInvasionFrontOpenings(BannerlordInvasionFiefGarrisons bannerlordInvasionFiefGarrisons,
            BannerlordInvasionNeighbours bannerlordInvasionNeighbours,
            BannerlordInvasionRealms bannerlordInvasionRealms, InvasionConquestFronts invasionConquestFronts)
        {
            _bannerlordInvasionFiefGarrisons = bannerlordInvasionFiefGarrisons;
            _bannerlordInvasionNeighbours = bannerlordInvasionNeighbours;
            _bannerlordInvasionRealms = bannerlordInvasionRealms;
            _invasionConquestFronts = invasionConquestFronts;
        }

        // Only an invasion whose fiefs can all hold reaches past the wars it already has.
        public void OpenFront(Invasion invasion, InvasionConquest conquest, InvasionConquestRules rules)
        {
            IFaction? invader = invasion.FindLeadingClan()?.MapFaction;

            if (invader == null || invader.Fiefs.Count == 0 || !_bannerlordInvasionFiefGarrisons.CanExpand(invasion))
            {
                return;
            }

            string? kingdomId = _invasionConquestFronts.FindFrontToOpen(conquest, rules,
                _bannerlordInvasionNeighbours.FindPrey(invader), CampaignTime.Now.ToDays);
            Kingdom? kingdom = _bannerlordInvasionRealms.FindKingdom(kingdomId);

            if (kingdom == null)
            {
                return;
            }

            conquest.OpenFront(CampaignTime.Now.ToDays);
            DeclareWarAction.ApplyByDefault(invader, kingdom);
            Logger.Info("Invasion conquest: front opened spawn=" + invasion.SpawnTemplateId +
                        " kingdom=" + kingdom.StringId + " fiefs=" + invader.Fiefs.Count);
        }
    }
}
