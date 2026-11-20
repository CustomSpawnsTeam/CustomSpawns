using System;
using System.Collections.Generic;
using System.Linq;
using CustomSpawns.Invasions.Adapters;
using CustomSpawns.Invasions.Conquest;
using NLog;
using TaleWorlds.CampaignSystem;

namespace CustomSpawns.Invasions.Lines.Adapters
{
    /**
     * Lands the next realm of every conquering invasion line still owed one; see InvasionLineRefill. A fallen
     * kingdom no invasion clan ruled counts only for the lines that were conquering it when it fell; see
     * InvasionBeatenKingdoms. The line's realms are every
     * invasion of its first spawn still fighting: the first itself, its configured followers and its copies,
     * and every founded one whose kingdom has fallen, since a fallen realm is never replaced.
     */
    public sealed class BannerlordInvasionLineRefills
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly Func<InvasionState> _state;
        private readonly BannerlordInvasionConfigurations _bannerlordInvasionConfigurations;
        private readonly BannerlordInvasionFollowerLaunches _bannerlordInvasionFollowerLaunches;
        private readonly InvasionConquests _invasionConquests;
        private readonly IInvasionClanRepository _invasionClans;
        private readonly IInvasionCapitalRepository _invasionCapitals;
        private readonly InvasionBeatenKingdoms _invasionBeatenKingdoms;
        private readonly InvasionLineRefill _invasionLineRefill = new();

        public BannerlordInvasionLineRefills(Func<InvasionState> state,
            BannerlordInvasionConfigurations bannerlordInvasionConfigurations,
            BannerlordInvasionFollowerLaunches bannerlordInvasionFollowerLaunches,
            InvasionConquests invasionConquests, IInvasionClanRepository invasionClans,
            IInvasionCapitalRepository invasionCapitals, InvasionBeatenKingdoms invasionBeatenKingdoms)
        {
            _state = state;
            _bannerlordInvasionConfigurations = bannerlordInvasionConfigurations;
            _bannerlordInvasionFollowerLaunches = bannerlordInvasionFollowerLaunches;
            _invasionConquests = invasionConquests;
            _invasionClans = invasionClans;
            _invasionCapitals = invasionCapitals;
            _invasionBeatenKingdoms = invasionBeatenKingdoms;
        }

        public void CheckDaily()
        {
            int day = (int)CampaignTime.Now.ToDays;

            if (!_invasionLineRefill.IsCheckDay(day))
            {
                return;
            }

            InvasionState state = _state();
            List<string> fallenKingdomIds = Kingdom.All.Where(kingdom => kingdom.IsEliminated && !IsInvasionRealm(kingdom))
                .Select(kingdom => kingdom.StringId).ToList();
            List<Invasion> fighting = state.Invasions
                .Where(invasion => state.FindFightingInvasion(invasion.ClanId) == invasion).ToList();

            foreach (IGrouping<string, Invasion> line in fighting.GroupBy(FindLineTemplateId))
            {
                InvasionConquestRules? rules = _bannerlordInvasionConfigurations.FindConquestRules(line.Key);

                if (rules == null)
                {
                    continue;
                }

                List<Invasion> invasions = line.ToList();
                int fallen = _invasionBeatenKingdoms.CountBeatenBy(line.Key, fallenKingdomIds);
                int fallenRealms = state.FallenRealmTemplateIds.Count(templateId =>
                    FindLineTemplateId(templateId) == line.Key);
                bool hasStandingRealm = invasions.Any(invasion =>
                    invasion.FindLeadingClan()?.Kingdom is { IsEliminated: false });
                InvasionConquest? launcher = FindLauncher(invasions);
                bool launch = launcher != null &&
                              _invasionLineRefill.ShouldLaunch(fallen, invasions.Count + fallenRealms,
                                  hasStandingRealm, rules.Value.MaxKingdoms);

                Logger.Debug("Invasion line check: line=" + line.Key + " fallen=" + fallen + " invasions=" +
                            invasions.Count + " fallen_realms=" + fallenRealms + " max_realms=" + rules.Value.MaxKingdoms + " standing_realm=" +
                            hasStandingRealm + " launch=" + launch +
                            " day=" + day);

                if (launch)
                {
                    _bannerlordInvasionFollowerLaunches.LaunchFollower(launcher!);
                }
            }
        }

        // A copy runs its line's first spawn; a configured follower names the spawn it follows.
        private string FindLineTemplateId(Invasion invasion) => FindLineTemplateId(invasion.SpawnTemplateId);

        private string FindLineTemplateId(string spawnTemplateId) =>
            _bannerlordInvasionConfigurations.FindLeaderTemplateId(spawnTemplateId) ?? spawnTemplateId;

        // The line's leader when it still conquers, else whichever realm of the line does; null before any has.
        private InvasionConquest? FindLauncher(IEnumerable<Invasion> invasions)
        {
            List<InvasionConquest> conquests = invasions.Select(invasion => _invasionConquests.Find(invasion.ClanId))
                .OfType<InvasionConquest>().ToList();
            InvasionConquest? conquest = conquests.FirstOrDefault(candidate => candidate.LeaderClanId == null) ??
                                         conquests.FirstOrDefault();

            return conquest == null ? null : _invasionConquests.FindLauncher(conquest);
        }

        // A retired invasion realm loses its ruling clan, but its capital stays on record.
        private bool IsInvasionRealm(Kingdom kingdom) =>
            (kingdom.RulingClan != null && _invasionClans.FindSpawnTemplate(kingdom.RulingClan.StringId) != null) ||
            _invasionCapitals.FindAll().Any(capital => capital.InvaderKingdomId == kingdom.StringId);
    }
}
