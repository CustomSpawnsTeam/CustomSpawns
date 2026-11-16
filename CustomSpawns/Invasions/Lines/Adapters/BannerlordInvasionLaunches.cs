using System;
using System.Collections.Generic;
using System.Linq;
using CustomSpawns.Data.Dto;
using CustomSpawns.Spawn;
using CustomSpawns.Utils;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace CustomSpawns.Invasions.Lines.Adapters
{
    /**
     * Lands an invasion launched on behalf of another in a coastal town. The towns it could land in are measured
     * from the fiefs of its line's first realm, so it can land close by and open a front the whole line shares.
     */
    public sealed class BannerlordInvasionLaunches
    {
        private readonly SpawnGroupService _spawnGroupService;
        private readonly Func<Settlement, bool> _isCoastalTown;

        public BannerlordInvasionLaunches(SpawnGroupService spawnGroupService, Func<Settlement, bool> isCoastalTown)
        {
            _spawnGroupService = spawnGroupService;
            _isCoastalTown = isCoastalTown;
        }

        public bool HasCoast(Kingdom kingdom) =>
            kingdom.Settlements.Any(settlement => settlement.IsTown && _isCoastalTown(settlement));

        /**
         * Every coastal town of a standing kingdom outside the allies, with how far it lies from the nearest fief of
         * the first realm, or of any ally once the first realm holds none.
         */
        public IReadOnlyList<InvasionLandingSite> FindLandingSites(IReadOnlyCollection<IFaction> allies,
            IFaction? firstRealm)
        {
            IEnumerable<IFaction> anchors = firstRealm != null && firstRealm.Fiefs.Any()
                ? new[] { firstRealm }
                : allies;
            List<Vec2> holdings = anchors.SelectMany(ally => ally.Fiefs)
                .Select(town => town.Settlement.GatePosition.ToVec2()).ToList();

            return Town.AllTowns
                .Where(town => town.MapFaction is Kingdom { IsEliminated: false } &&
                               !allies.Contains(town.MapFaction) && _isCoastalTown(town.Settlement))
                .Select(town => new InvasionLandingSite(town.Settlement.StringId, town.MapFaction.StringId,
                    holdings.Count == 0
                        ? 0f
                        : holdings.Min(holding => holding.Distance(town.Settlement.GatePosition.ToVec2()))))
                .ToList();
        }

        /**
         * Lands the spawn for the given clan, its configured one or a copy founded for it; the clan's id, or null
         * when the landing town is gone or the spawn failed.
         */
        public string? Launch(SpawnDto spawn, string landingSettlementId, Clan clan)
        {
            Settlement? landing = Settlement.Find(landingSettlementId);
            MobileParty? party = landing == null ? null : _spawnGroupService.Spawn(spawn, landing, clan);

            if (party == null)
            {
                return null;
            }

            if (spawn.SpawnMessage != null)
            {
                UX.ShowMessage(spawn.SpawnMessage.Information, spawn.SpawnMessage.Color, landing!.Name.ToString());
            }

            return clan.StringId;
        }
    }
}
