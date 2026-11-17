using System;
using System.Collections.Generic;
using System.Linq;
using CustomSpawns.Diplomacy;
using CustomSpawns.Invasions.Adapters;
using CustomSpawns.Invasions.Alliances;
using CustomSpawns.Invasions.Coalitions.Adapters;
using CustomSpawns.Invasions.Lines.Adapters;
using NLog;
using TaleWorlds.CampaignSystem;

namespace CustomSpawns.Invasions.Conquest.Adapters
{
    /**
     * Answers what a conquering invasion's war with another faction should be and how threatening it is, which is
     * what draws its neighbours into the war as it grows, and which kingdom it conquers.
     */
    public sealed class BannerlordInvasionConquestStances : IFactionWarStanceProvider, IInvasionConquestTargets
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly Func<InvasionState> _state;
        private readonly BannerlordInvasionConfigurations _bannerlordInvasionConfigurations;
        private readonly BannerlordInvasionRealms _bannerlordInvasionRealms;
        private readonly InvasionConquests _invasionConquests;
        private readonly InvasionConquestFronts _invasionConquestFronts;
        private readonly BannerlordInvasionLines _bannerlordInvasionLines;
        private readonly BannerlordInvasionCoalitions _bannerlordInvasionCoalitions;

        public BannerlordInvasionConquestStances(Func<InvasionState> state,
            BannerlordInvasionConfigurations bannerlordInvasionConfigurations,
            BannerlordInvasionRealms bannerlordInvasionRealms, InvasionConquests invasionConquests,
            InvasionConquestFronts invasionConquestFronts, BannerlordInvasionLines bannerlordInvasionLines,
            BannerlordInvasionCoalitions bannerlordInvasionCoalitions)
        {
            _state = state;
            _bannerlordInvasionConfigurations = bannerlordInvasionConfigurations;
            _bannerlordInvasionRealms = bannerlordInvasionRealms;
            _invasionConquests = invasionConquests;
            _invasionConquestFronts = invasionConquestFronts;
            _bannerlordInvasionLines = bannerlordInvasionLines;
            _bannerlordInvasionCoalitions = bannerlordInvasionCoalitions;
        }

        public InvasionWarStance FindStance(IFaction first, IFaction second)
        {
            InvasionWarStance stance = FindStanceOf(first, second);

            return stance != InvasionWarStance.Policy ? stance : FindStanceOf(second, first);
        }

        /**
         * Only kingdoms weigh the threat: they are the ones that vote on wars. They weigh the invader's whole line,
         * not the one realm, as a war with one realm is a war with all of them once it calls its allies in, and they
         * heed less of it the more their other wars already take from them, until a coalition stands against the
         * line and they heed all of it.
         */
        public float FindThreat(IFaction threatened, IFaction other)
        {
            InvasionConquest? conquest = _invasionConquests.Find(_bannerlordInvasionRealms.FindInvasionClanId(other));
            Invasion? invasion = conquest == null ? null : _state().FindFightingInvasion(conquest.InvasionClanId);
            InvasionConquestRules? rules = invasion == null
                ? null
                : _bannerlordInvasionConfigurations.FindConquestRules(invasion.SpawnTemplateId);

            if (rules == null || !threatened.IsKingdomFaction)
            {
                return 0f;
            }

            List<IFaction> realms = _bannerlordInvasionLines.FindRealms(conquest!, other.MapFaction);

            if (realms.Contains(threatened))
            {
                return 0f;
            }

            float threat = _bannerlordInvasionLines.FindThreat(rules.Value, realms);

            return _bannerlordInvasionCoalitions.IsFormedAgainst(realms)
                ? threat
                : _invasionConquestFronts.FindStretchedThreat(threat, threatened.CurrentTotalStrength,
                    FindOtherEnemiesStrength(threatened, realms));
        }

        public float FindCoalitionThreat(IFaction first, IFaction second) =>
            _bannerlordInvasionCoalitions.FindCoalitionThreat(first, second);

        public bool IsAtCoalitionWar(IFaction first, IFaction second) =>
            _bannerlordInvasionCoalitions.StandsAgainst(first, second) ||
            _bannerlordInvasionCoalitions.StandsAgainst(second, first);

        // A war with a conquering invader is logged with the threat the other side saw in it.
        public void ObserveWar(IFaction first, IFaction second)
        {
            foreach ((IFaction declarer, IFaction invader) in new[] { (first, second), (second, first) })
            {
                string? invasionClanId = _bannerlordInvasionRealms.FindInvasionClanId(invader);

                if (_invasionConquests.Find(invasionClanId) != null)
                {
                    Logger.Info("Invasion threat: war kingdom=" + declarer.StringId + " invader=" + invasionClanId +
                                " fiefs=" + invader.Fiefs.Count + " threat=" + FindThreat(declarer, invader));
                }
            }
        }

        public double MeasureConquestTargetStrength(Invasion invasion)
        {
            Kingdom? target = FindConquestTarget(invasion);

            if (target == null)
            {
                return 0;
            }

            return target.Clans.Sum(clan =>
                clan.WarPartyComponents.Where(component => component.MobileParty.IsActive)
                    .Sum(component => (double)component.MobileParty.Party.EstimatedStrength) +
                clan.Fiefs.Sum(fief => (double)(fief.GarrisonParty?.Party.EstimatedStrength ?? 0f)));
        }

        public string? FindConquestTargetKingdomId(Invasion invasion) => FindConquestTarget(invasion)?.StringId;

        public void LogThreat(Invasion invasion, InvasionConquest conquest)
        {
            IFaction? invader = invasion.FindLeadingClan()?.MapFaction;

            if (invader == null)
            {
                return;
            }

            IEnumerable<string> wars = Kingdom.All
                .Where(kingdom => !kingdom.IsEliminated && kingdom != invader && kingdom.IsAtWarWith(invader))
                .Select(kingdom => kingdom.StringId);

            List<IFaction> realms = _bannerlordInvasionLines.FindRealms(conquest, invader);

            Logger.Debug("Invasion threat: spawn=" + invasion.SpawnTemplateId + " target=" + conquest.TargetKingdomId +
                        " fiefs=" + invader.Fiefs.Count + " line_fiefs=" + realms.Sum(realm => realm.Fiefs.Count) +
                        " threat=" + _bannerlordInvasionLines.FindThreat(
                            _bannerlordInvasionConfigurations.FindConquestRules(invasion.SpawnTemplateId)!.Value,
                            realms) +
                        " coalition=" + _bannerlordInvasionCoalitions.IsFormedAgainst(realms) +
                        " threshold=" +
                        Campaign.Current.Models.DiplomacyModel.GetDecisionMakingThreshold(invader) +
                        " wars=[" + string.Join(";", wars) + "]");
        }

        private InvasionWarStance FindStanceOf(IFaction invader, IFaction other)
        {
            string? invasionClanId = _bannerlordInvasionRealms.FindInvasionClanId(invader);

            return _invasionConquests.FindStance(invasionClanId, other.StringId, other.IsKingdomFaction,
                _bannerlordInvasionRealms.FindInvasionClanId(other), IsWarLeaderAtWarWith(invasionClanId, other));
        }

        /**
         * Whether another realm leading the line's war with the other faction still fights it. A realm leading the
         * war itself is never asked: vanilla answers a war between factions with no stance yet by asking this
         * stance again, which would never end.
         */
        private bool IsWarLeaderAtWarWith(string? invasionClanId, IFaction other)
        {
            InvasionConquest? conquest = _invasionConquests.Find(invasionClanId);
            InvasionConquest? leader =
                conquest == null ? null : _invasionConquests.FindWarLeader(conquest, other.StringId);
            IFaction? leaderRealm = leader == null || leader == conquest
                ? null
                : _bannerlordInvasionRealms.FindRealm(leader);

            return leaderRealm != null && leaderRealm != other && leaderRealm.IsAtWarWith(other);
        }

        // The kingdoms the threatened one already fights, the invader's line left out.
        private static float FindOtherEnemiesStrength(IFaction threatened, ICollection<IFaction> realms) =>
            Kingdom.All.Where(kingdom => !kingdom.IsEliminated && !realms.Contains(kingdom) &&
                                         threatened.IsAtWarWith(kingdom))
                .Sum(kingdom => kingdom.CurrentTotalStrength);

        private Kingdom? FindConquestTarget(Invasion invasion)
        {
            InvasionConquest? conquest = _invasionConquests.Find(invasion.ClanId);
            Kingdom? target = conquest == null ? null : _bannerlordInvasionRealms.FindKingdom(conquest.TargetKingdomId);

            return target?.IsEliminated == false ? target : null;
        }
    }
}
