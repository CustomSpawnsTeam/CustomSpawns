using System;
using System.Collections.Generic;
using System.Linq;
using CustomSpawns.Diplomacy;
using NLog;
using TaleWorlds.CampaignSystem;

namespace CustomSpawns.Invasions.Adapters
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

        public BannerlordInvasionConquestStances(Func<InvasionState> state,
            BannerlordInvasionConfigurations bannerlordInvasionConfigurations,
            BannerlordInvasionRealms bannerlordInvasionRealms, InvasionConquests invasionConquests,
            InvasionConquestFronts invasionConquestFronts)
        {
            _state = state;
            _bannerlordInvasionConfigurations = bannerlordInvasionConfigurations;
            _bannerlordInvasionRealms = bannerlordInvasionRealms;
            _invasionConquests = invasionConquests;
            _invasionConquestFronts = invasionConquestFronts;
        }

        public InvasionWarStance FindStance(IFaction first, IFaction second)
        {
            InvasionWarStance stance = FindStanceOf(first, second);

            return stance != InvasionWarStance.Policy ? stance : FindStanceOf(second, first);
        }

        // Only kingdoms weigh the threat: they are the ones that vote on wars.
        public float FindThreat(IFaction threatened, IFaction other)
        {
            InvasionConquest? conquest = _invasionConquests.Find(_bannerlordInvasionRealms.FindInvasionClanId(other));
            Invasion? invasion = conquest == null ? null : _state().FindFightingInvasion(conquest.InvasionClanId);
            InvasionConquestRules? rules = invasion == null
                ? null
                : _bannerlordInvasionConfigurations.FindConquestRules(invasion.SpawnTemplateId);

            if (rules == null || !threatened.IsKingdomFaction || threatened == other.MapFaction)
            {
                return 0f;
            }

            IFaction invader = other.MapFaction;

            return _invasionConquestFronts.FindThreat(rules.Value, invader.Fiefs.Count,
                Campaign.Current.Models.DiplomacyModel.GetValueOfSettlementsForFaction(invader));
        }

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

            Logger.Info("Invasion threat: spawn=" + invasion.SpawnTemplateId + " target=" + conquest.TargetKingdomId +
                        " fiefs=" + invader.Fiefs.Count + " threat=" +
                        _invasionConquestFronts.FindThreat(
                            _bannerlordInvasionConfigurations.FindConquestRules(invasion.SpawnTemplateId)!.Value,
                            invader.Fiefs.Count,
                            Campaign.Current.Models.DiplomacyModel.GetValueOfSettlementsForFaction(invader)) +
                        " threshold=" +
                        Campaign.Current.Models.DiplomacyModel.GetDecisionMakingThreshold(invader) +
                        " wars=[" + string.Join(";", wars) + "]");
        }

        private InvasionWarStance FindStanceOf(IFaction invader, IFaction other) =>
            _invasionConquests.FindStance(_bannerlordInvasionRealms.FindInvasionClanId(invader), other.StringId,
                other.IsKingdomFaction, _bannerlordInvasionRealms.FindInvasionClanId(other));

        private Kingdom? FindConquestTarget(Invasion invasion)
        {
            InvasionConquest? conquest = _invasionConquests.Find(invasion.ClanId);
            Kingdom? target = conquest == null ? null : _bannerlordInvasionRealms.FindKingdom(conquest.TargetKingdomId);

            return target?.IsEliminated == false ? target : null;
        }
    }
}
