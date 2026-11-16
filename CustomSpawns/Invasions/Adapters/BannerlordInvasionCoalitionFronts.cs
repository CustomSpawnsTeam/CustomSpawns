using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace CustomSpawns.Invasions.Adapters
{
    /**
     * Who fights on each side of a coalition's war, and which capitals of the line it has cut off, worked out once an
     * hour: every lord of the coalition asks it each hour, and finding the lines is too costly to repeat for each.
     */
    public sealed class BannerlordInvasionCoalitionFronts
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly Func<InvasionState> _state;
        private readonly BannerlordInvasionCoalitions _bannerlordInvasionCoalitions;
        private readonly BannerlordInvasionRealms _bannerlordInvasionRealms;
        private readonly InvasionCapitalIsolation _invasionCapitalIsolation;
        private readonly Dictionary<IFaction, bool> _members = new();
        private HashSet<IFaction> _lineRealms = new();
        private HashSet<Settlement> _isolatedCapitals = new();
        private int _hour = -1;

        public BannerlordInvasionCoalitionFronts(Func<InvasionState> state,
            BannerlordInvasionCoalitions bannerlordInvasionCoalitions,
            BannerlordInvasionRealms bannerlordInvasionRealms, InvasionCapitalIsolation invasionCapitalIsolation)
        {
            _state = state;
            _bannerlordInvasionCoalitions = bannerlordInvasionCoalitions;
            _bannerlordInvasionRealms = bannerlordInvasionRealms;
            _invasionCapitalIsolation = invasionCapitalIsolation;
        }

        // Whether the faction stands in a coalition that has formed against a line.
        public bool IsMember(IFaction? faction)
        {
            Refresh();

            if (faction == null || _lineRealms.Count == 0)
            {
                return false;
            }

            if (!_members.TryGetValue(faction, out bool isMember))
            {
                isMember = _bannerlordInvasionCoalitions.IsCoalitionMember(faction);
                _members[faction] = isMember;
            }

            return isMember;
        }

        // Whether the faction is a realm of a line a coalition stands against.
        public bool IsLineRealm(IFaction? faction)
        {
            Refresh();
            return faction != null && _lineRealms.Contains(faction);
        }

        public IReadOnlyCollection<Settlement> FindIsolatedCapitals()
        {
            Refresh();
            return _isolatedCapitals;
        }

        private void Refresh()
        {
            int hour = (int)CampaignTime.Now.ToHours;

            if (hour == _hour)
            {
                return;
            }

            _hour = hour;
            _members.Clear();
            List<(List<IFaction> Realms, InvasionConquestRules Rules)> lines =
                _bannerlordInvasionCoalitions.FindFormedLineRealms();
            _lineRealms = new HashSet<IFaction>(lines.SelectMany(line => line.Realms));
            HashSet<Settlement> isolatedCapitals = new(lines.SelectMany(line =>
                FindIsolatedCapitals(new HashSet<IFaction>(line.Realms), line.Rules.CoalitionCapitalRing)));

            if (!isolatedCapitals.SetEquals(_isolatedCapitals))
            {
                Logger.Info("Invasion coalition capitals cut off: " +
                            string.Join(",", isolatedCapitals.Select(capital => capital.StringId)) +
                            " day=" + (int)CampaignTime.Now.ToDays);
            }

            _isolatedCapitals = isolatedCapitals;
        }

        // The capitals each realm of the line still holds, whose nearest fiefs the line holds fewer than half of.
        private IEnumerable<Settlement> FindIsolatedCapitals(HashSet<IFaction> realms, int ringSize)
        {
            if (ringSize <= 0)
            {
                yield break;
            }

            foreach (IFaction realm in realms)
            {
                string? capitalId = _state().FindFightingInvasion(_bannerlordInvasionRealms.FindInvasionClanId(realm))
                    ?.CapitalSettlementId;
                Settlement? capital = capitalId == null ? null : Settlement.Find(capitalId);

                if (capital == null || !realms.Contains(capital.MapFaction))
                {
                    continue;
                }

                IEnumerable<bool> nearestHeld = Settlement.All
                    .Where(settlement => settlement.IsFortification && settlement != capital)
                    .OrderBy(settlement => settlement.Position.DistanceSquared(capital.Position))
                    .Take(ringSize)
                    .Select(settlement => realms.Contains(settlement.MapFaction));

                if (_invasionCapitalIsolation.IsIsolated(nearestHeld, ringSize))
                {
                    yield return capital;
                }
            }
        }
    }
}
