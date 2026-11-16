using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace CustomSpawns.Invasions.Adapters
{
    /**
     * The invasion lines grown so large that every other kingdom bands together against them: those kingdoms make
     * peace with each other and stay at war with the line, until it is destroyed or, when so configured, beaten:
     * none of its capitals held and too few fiefs left. The player's kingdom stands with them only once the player
     * has joined.
     */
    public sealed class BannerlordInvasionCoalitions : IInvasionCoalitionMembers
    {
        private readonly Func<InvasionState> _state;
        private readonly BannerlordInvasionConfigurations _bannerlordInvasionConfigurations;
        private readonly BannerlordInvasionRealms _bannerlordInvasionRealms;
        private readonly InvasionConquests _invasionConquests;
        private readonly InvasionConquestFronts _invasionConquestFronts;
        private readonly BannerlordInvasionLines _bannerlordInvasionLines;
        private readonly InvasionCoalitionPlayerChoice _invasionCoalitionPlayerChoice;
        private readonly InvasionStandingCoalitions _invasionStandingCoalitions;

        public BannerlordInvasionCoalitions(Func<InvasionState> state,
            BannerlordInvasionConfigurations bannerlordInvasionConfigurations,
            BannerlordInvasionRealms bannerlordInvasionRealms, InvasionConquests invasionConquests,
            InvasionConquestFronts invasionConquestFronts, BannerlordInvasionLines bannerlordInvasionLines,
            InvasionCoalitionPlayerChoice invasionCoalitionPlayerChoice,
            InvasionStandingCoalitions invasionStandingCoalitions)
        {
            _state = state;
            _bannerlordInvasionConfigurations = bannerlordInvasionConfigurations;
            _bannerlordInvasionRealms = bannerlordInvasionRealms;
            _invasionConquests = invasionConquests;
            _invasionConquestFronts = invasionConquestFronts;
            _bannerlordInvasionLines = bannerlordInvasionLines;
            _invasionCoalitionPlayerChoice = invasionCoalitionPlayerChoice;
            _invasionStandingCoalitions = invasionStandingCoalitions;
        }

        /**
         * How much the two kingdoms want peace with each other, and how far a war between them is ruled out: the
         * whole threat of the strongest line a coalition stands against, when both of them stand in it. Invasion
         * realms never join one. Zero when no coalition holds the two of them.
         */
        public float FindCoalitionThreat(IFaction first, IFaction second)
        {
            if (!IsCoalitionMember(first) || !IsCoalitionMember(second))
            {
                return 0f;
            }

            return FindFormedLines().Select(line => line.Threat).DefaultIfEmpty(0f).Max();
        }

        public IReadOnlyList<string> FindCoalitionMembers() =>
            FindFormedLines().Count == 0
                ? new List<string>()
                : Kingdom.All.Where(IsCoalitionMember).Select(kingdom => kingdom.StringId).ToList();

        public IReadOnlyList<string> FindInvaders() =>
            FindFormedLines().SelectMany(line => line.Realms).Where(realm => !realm.IsEliminated)
                .Select(realm => realm.StringId).Distinct().ToList();

        // Whether the kingdom stands in a coalition against the line the realm is of.
        public bool StandsAgainst(IFaction kingdom, IFaction realm) =>
            IsCoalitionMember(kingdom) && FindFormedLines().Any(line => line.Realms.Contains(realm));

        // The realm leading the first line a coalition stands against; null while none does.
        public IFaction? FindInvader() => FindFormedLines().Select(line => line.Leader).FirstOrDefault();

        // Breaks up the coalition against every line with no realm left standing, or beaten.
        public void DisbandBeatenLines()
        {
            int allFiefs = Town.AllFiefs.Count();

            _invasionStandingCoalitions.DisbandAllBut(FindLines()
                .Where(line => line.Realms.Any(realm => !realm.IsEliminated) &&
                               !_invasionConquestFronts.IsCoalitionSpent(line.Rules,
                                   line.Realms.Sum(realm => realm.Fiefs.Count), allFiefs, HoldsACapital(line.Realms)))
                .Select(line => line.LineId));
        }

        // Whether a coalition stands against the realms, a line's.
        public bool IsFormedAgainst(IReadOnlyCollection<IFaction> realms) =>
            FindFormedLines().Any(line => line.Realms.Intersect(realms).Any());

        // The realms and rules of each line a coalition stands against.
        public List<(List<IFaction> Realms, InvasionConquestRules Rules)> FindFormedLineRealms() =>
            FindFormedLines().Select(line => (line.Realms, line.Rules)).ToList();

        // A kingdom that stands in a coalition once one forms: any but an invasion realm, the player's once joined.
        public bool IsCoalitionMember(IFaction faction) =>
            faction is Kingdom { IsEliminated: false } kingdom &&
            _bannerlordInvasionRealms.FindInvasionClanId(faction) == null &&
            _invasionCoalitionPlayerChoice.Includes(kingdom.RulingClan == Clan.PlayerClan);

        private bool HoldsACapital(List<IFaction> realms)
        {
            InvasionState state = _state();

            return state.Invasions.Any(invasion =>
                invasion.FindLeadingClan()?.MapFaction is { } realm && realms.Contains(realm) &&
                state.HoldsCapital(invasion));
        }

        // Each line a coalition stands against, with the realm leading it and the threat its realms make together.
        private List<(List<IFaction> Realms, IFaction? Leader, float Threat, InvasionConquestRules Rules)>
            FindFormedLines()
        {
            int allFiefs = Town.AllFiefs.Count();

            return FindLines()
                .Where(line => _invasionStandingCoalitions.StandsAgainst(line.LineId,
                    _invasionConquestFronts.IsCoalitionFormed(line.Rules, line.Realms.Sum(realm => realm.Fiefs.Count),
                        allFiefs)))
                .Select(line => (line.Realms, _bannerlordInvasionRealms.FindRealm(line.Launcher),
                    _bannerlordInvasionLines.FindThreat(line.Rules, line.Realms), line.Rules))
                .ToList();
        }

        // Each conquering line, once, by the spawn template of its first invasion, with its realms and rules.
        private List<(string LineId, List<IFaction> Realms, InvasionConquest Launcher, InvasionConquestRules Rules)>
            FindLines()
        {
            var lines = new List<(string, List<IFaction>, InvasionConquest, InvasionConquestRules)>();

            foreach (InvasionConquest conquest in _invasionConquests.All)
            {
                Invasion? invasion = _state().FindFightingInvasion(conquest.InvasionClanId);
                InvasionConquestRules? rules = invasion == null
                    ? null
                    : _bannerlordInvasionConfigurations.FindConquestRules(invasion.SpawnTemplateId);
                List<IFaction> realms = _invasionConquests.FindLine(conquest)
                    .Select(_bannerlordInvasionRealms.FindRealm).OfType<IFaction>().Distinct().ToList();

                // Every realm of a line names the same line, so each is counted once.
                if (rules == null || lines.Any(line => line.Item2.Intersect(realms).Any()))
                {
                    continue;
                }

                string lineId = _bannerlordInvasionConfigurations.FindLeaderTemplateId(invasion!.SpawnTemplateId) ??
                                invasion.SpawnTemplateId;
                lines.Add((lineId, realms, _invasionConquests.FindLauncher(conquest), rules.Value));
            }

            return lines;
        }
    }
}
