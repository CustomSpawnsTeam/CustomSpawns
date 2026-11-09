using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CustomSpawns.Diplomacy;
using Helpers;
using NLog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace CustomSpawns.Invasions.Adapters
{
    /**
     * Keeps an invasion at war with the kingdom it is conquering and leaves every other kingdom to its own
     * diplomacy. The diplomacy rules ask it how threatening an invader is, which is what draws its neighbours
     * into the war as it grows. Once that kingdom has fallen the invader turns on the bordering kingdom it gains
     * most by fighting.
     */
    public sealed class BannerlordInvasionConquestWars : IFactionWarStanceProvider, IInvasionConquestTargets
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private const char Separator = '|';
        private readonly Func<InvasionState> _state;
        private readonly BannerlordInvasionConfigurations _bannerlordInvasionConfigurations;
        private readonly BannerlordInvasionFiefGarrisons _bannerlordInvasionFiefGarrisons;
        private InvasionConquests _invasionConquests = new();
        private readonly Dictionary<string, int> _unborderedSiegeLogDays = new();

        public BannerlordInvasionConquestWars(Func<InvasionState> state,
            BannerlordInvasionConfigurations bannerlordInvasionConfigurations,
            BannerlordInvasionFiefGarrisons bannerlordInvasionFiefGarrisons)
        {
            _state = state;
            _bannerlordInvasionConfigurations = bannerlordInvasionConfigurations;
            _bannerlordInvasionFiefGarrisons = bannerlordInvasionFiefGarrisons;
        }

        public InvasionWarStance FindStance(IFaction first, IFaction second)
        {
            InvasionWarStance stance = FindStanceOf(first, second);

            return stance != InvasionWarStance.Policy ? stance : FindStanceOf(second, first);
        }

        // Only kingdoms weigh the threat: they are the ones that vote on wars.
        public float FindThreat(IFaction threatened, IFaction other)
        {
            InvasionConquest? conquest = _invasionConquests.Find(FindInvasionClanId(other));
            Invasion? invasion = conquest == null ? null : _state().FindFightingInvasion(conquest.InvasionClanId);
            InvasionConquestRules? rules = invasion == null
                ? null
                : _bannerlordInvasionConfigurations.FindConquestRules(invasion.SpawnTemplateId);

            if (rules == null || !threatened.IsKingdomFaction || threatened == other.MapFaction)
            {
                return 0f;
            }

            IFaction invader = other.MapFaction;

            return _invasionConquests.FindThreat(rules.Value, invader.Fiefs.Count,
                Campaign.Current.Models.DiplomacyModel.GetValueOfSettlementsForFaction(invader));
        }

        // The first fief an invasion that conquers takes from a kingdom makes that kingdom its target.
        public void ObserveConquest(Clan? newOwnerClan, Clan? previousOwnerClan)
        {
            Invasion? invasion = _state().FindFightingInvasion(newOwnerClan?.StringId);

            if (invasion == null || _bannerlordInvasionConfigurations.FindConquestRules(invasion.SpawnTemplateId) == null)
            {
                return;
            }

            InvasionConquest? conquest = _invasionConquests.Begin(invasion.ClanId,
                previousOwnerClan?.Kingdom?.StringId, CampaignTime.Now.ToDays);

            if (conquest == null)
            {
                return;
            }

            Logger.Info("Invasion conquest: begun spawn=" + invasion.SpawnTemplateId +
                        " target=" + conquest.TargetKingdomId + " day=" + conquest.StartedDay);
            DeclareWarOnTarget(invasion, conquest);
        }

        public void UpdateDaily()
        {
            InvasionState state = _state();

            foreach (InvasionConquest conquest in _invasionConquests.All.ToList())
            {
                Invasion? invasion = state.FindFightingInvasion(conquest.InvasionClanId);
                InvasionConquestRules? rules = invasion == null
                    ? null
                    : _bannerlordInvasionConfigurations.FindConquestRules(invasion.SpawnTemplateId);

                if (invasion == null || rules == null)
                {
                    _invasionConquests.Forget(conquest.InvasionClanId);
                    continue;
                }

                if (HasFallen(conquest.TargetKingdomId) && !Retarget(invasion, conquest))
                {
                    continue;
                }

                DeclareWarOnTarget(invasion, conquest);
                OpenFront(invasion, conquest, rules.Value);
                LogThreat(invasion, conquest);
            }
        }

        // A war with a conquering invader is logged with the threat the other side saw in it.
        public void ObserveWar(IFaction first, IFaction second)
        {
            foreach ((IFaction declarer, IFaction invader) in new[] { (first, second), (second, first) })
            {
                string? invasionClanId = FindInvasionClanId(invader);

                if (_invasionConquests.Find(invasionClanId) != null)
                {
                    Logger.Info("Invasion threat: war kingdom=" + declarer.StringId + " invader=" + invasionClanId +
                                " fiefs=" + invader.Fiefs.Count + " threat=" + FindThreat(declarer, invader));
                }
            }
        }

        // A lord of a conquering invasion would rather besiege the kingdom it conquers than anyone else.
        public void PreferTargetSieges(MobileParty party, PartyThinkParams thinkParams)
        {
            Invasion? invasion = _state().FindFightingInvasion(party.ActualClan?.StringId);
            InvasionConquestRules? rules = invasion == null
                ? null
                : _bannerlordInvasionConfigurations.FindConquestRules(invasion.SpawnTemplateId);
            string? targetKingdomId = invasion == null ? null : FindConquestTargetKingdomId(invasion);

            if (rules == null || targetKingdomId == null)
            {
                return;
            }

            Kingdom? targetKingdom = Kingdom.All.FirstOrDefault(kingdom => kingdom.StringId == targetKingdomId);

            if (targetKingdom == null || !party.MapFaction.IsAtWarWith(targetKingdom))
            {
                return;
            }

            float strength = party.GetTotalLandStrengthWithFollowers();
            PreferTargetSieges(party, thinkParams, rules.Value, targetKingdom, false, strength);

            // Vanilla only fills the possible army members of a lord that can call an army this hour.
            if (thinkParams.PossibleArmyMembersUponArmyCreation != null)
            {
                float armyStrength = strength + thinkParams.PossibleArmyMembersUponArmyCreation.Sum(member =>
                    member.Party.GetCustomStrength(BattleSideEnum.Attacker, MapEvent.PowerCalculationContext.Siege));

                PreferTargetSieges(party, thinkParams, rules.Value, targetKingdom, true, armyStrength);
            }
        }

        /**
         * The sieges a lord weighs on its own, or with the army it could call. Vanilla only weighs a siege of a fief
         * the lord's realm holds fortifications near, so once the conquered kingdom's last fiefs lie beyond the
         * invasion's border they are never besieged; while none of its nearer fiefs can be won, they are offered here.
         * Vanilla does not weigh such a siege once it has begun either, so the lord besieging it is offered it again,
         * or it would leave the camp on its next thought.
         */
        private void PreferTargetSieges(MobileParty party, PartyThinkParams thinkParams,
            InvasionConquestRules rules, Kingdom targetKingdom, bool willGatherArmy, float strength)
        {
            List<(AIBehaviorData Behavior, float Score)> sieges = thinkParams.AIBehaviorScores
                .Where(entry => entry.Item1.AiBehavior == AiBehavior.BesiegeSettlement &&
                                entry.Item1.WillGatherArmy == willGatherArmy && entry.Item1.Party is Settlement)
                .Select(entry => (entry.Item1, entry.Item2))
                .ToList();

            Settlement? besieged = willGatherArmy ? null : party.BesiegedSettlement;

            // No siege weighed at all: vanilla ruled sieges out for this lord, for food or cohesion.
            if (sieges.Count == 0 && besieged == null)
            {
                return;
            }

            HashSet<Settlement> weighed = new(sieges.Select(siege => (Settlement)siege.Behavior.Party));
            float bestTargetScore = sieges.Where(siege => ((Settlement)siege.Behavior.Party).MapFaction == targetKingdom)
                .Select(siege => siege.Score).DefaultIfEmpty(0f).Max();
            float bestOtherScore = sieges.Where(siege => ((Settlement)siege.Behavior.Party).MapFaction != targetKingdom)
                .Select(siege => siege.Score).DefaultIfEmpty(0f).Max();

            foreach ((AIBehaviorData behavior, float score) in sieges)
            {
                if (((Settlement)behavior.Party).MapFaction == targetKingdom)
                {
                    thinkParams.SetBehaviorScore(behavior,
                        _invasionConquests.FindTargetSiegeScore(rules, score, bestOtherScore));
                }
            }

            foreach (Settlement fief in targetKingdom.Settlements.Where(settlement => settlement.IsFortification &&
                         !weighed.Contains(settlement) && (settlement == besieged ||
                             (settlement.SiegeEvent == null && settlement.Party.MapEvent == null))))
            {
                bool isBesiegingIt = fief == besieged;
                // Like vanilla, a lord weighs the siege it is in with everyone in its camp.
                float fiefStrength = isBesiegingIt && party.BesiegerCamp != null
                    ? party.BesiegerCamp.GetInvolvedPartiesForEventType().Sum(camper => camper.EstimatedStrength)
                    : strength;
                float targetScore = Campaign.Current.Models.TargetScoreCalculatingModel.GetTargetScoreForFaction(fief,
                    Army.ArmyTypes.Besieger, party, fiefStrength);
                float score = _invasionConquests.FindUnborderedTargetSiegeScore(rules, targetScore, bestTargetScore,
                    bestOtherScore, isBesiegingIt);
                AiHelper.GetBestNavigationTypeAndAdjustedDistanceOfSettlementForMobileParty(party, fief, false,
                    out MobileParty.NavigationType navigationType, out float _, out bool isFromPort);

                LogUnborderedSiege(party, fief, willGatherArmy, fiefStrength, targetScore, bestTargetScore,
                    bestOtherScore, score, navigationType);

                if (score <= 0f)
                {
                    continue;
                }

                if (navigationType != MobileParty.NavigationType.None)
                {
                    thinkParams.AddBehaviorScore((new AIBehaviorData(fief, AiBehavior.BesiegeSettlement,
                        navigationType, willGatherArmy, isFromPort, false), score));
                }
            }
        }

        // One line a day for each fief, from the first lord to weigh it, to show why it is or is not besieged.
        private void LogUnborderedSiege(MobileParty party, Settlement fief, bool willGatherArmy, float strength,
            float targetScore, float bestTargetScore, float bestOtherScore, float score,
            MobileParty.NavigationType navigationType)
        {
            int day = (int)CampaignTime.Now.ToDays;
            string key = fief.StringId + Separator + willGatherArmy;

            if (_unborderedSiegeLogDays.TryGetValue(key, out int loggedDay) && loggedDay == day)
            {
                return;
            }

            _unborderedSiegeLogDays[key] = day;
            Logger.Info(string.Format(CultureInfo.InvariantCulture,
                "Invasion unbordered siege: fief={0} party={1} army={2} strength={3:0} target_score={4:0.####} " +
                "best_target={5:0.####} best_other={6:0.####} score={7:0.####} navigation={8} day={9}",
                fief.StringId, party.StringId, willGatherArmy, strength, targetScore, bestTargetScore,
                bestOtherScore, score, navigationType, day));
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

        public Dictionary<string, string> Export() => _invasionConquests.All.ToDictionary(
            conquest => conquest.InvasionClanId,
            conquest => conquest.TargetKingdomId + Separator +
                        conquest.StartedDay.ToString("R", CultureInfo.InvariantCulture) + Separator +
                        conquest.LastFrontDay.ToString("R", CultureInfo.InvariantCulture));

        /**
         * A save without a last front dates it to the conquest. A save that marked the conquest settled names a
         * fallen target, so the invasion turns on its next one on the first day.
         */
        public void Restore(Dictionary<string, string>? saved)
        {
            _invasionConquests = new InvasionConquests();

            foreach (KeyValuePair<string, string> entry in saved ?? new Dictionary<string, string>())
            {
                string[] fields = entry.Value.Split(Separator);

                if (fields.Length >= 2 &&
                    double.TryParse(fields[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double day))
                {
                    double lastFrontDay = fields.Length >= 3 && double.TryParse(fields[2], NumberStyles.Float,
                        CultureInfo.InvariantCulture, out double front)
                        ? front
                        : day;
                    _invasionConquests.Restore(new InvasionConquest(entry.Key, fields[0], day, lastFrontDay));
                }
            }
        }

        private InvasionWarStance FindStanceOf(IFaction invader, IFaction other) =>
            _invasionConquests.FindStance(FindInvasionClanId(invader), other.StringId, other.IsKingdomFaction);

        private string? FindInvasionClanId(IFaction faction) =>
            _state().FindFightingInvasion((faction as Clan ?? (faction as Kingdom)?.RulingClan)?.StringId)?.ClanId;

        public string? FindConquestTargetKingdomId(Invasion invasion) => FindConquestTarget(invasion)?.StringId;

        private Kingdom? FindConquestTarget(Invasion invasion)
        {
            InvasionConquest? conquest = _invasionConquests.Find(invasion.ClanId);
            Kingdom? target = conquest == null
                ? null
                : Kingdom.All.FirstOrDefault(kingdom => kingdom.StringId == conquest.TargetKingdomId);

            return target?.IsEliminated == false ? target : null;
        }

        private static bool HasFallen(string kingdomId) =>
            Kingdom.All.FirstOrDefault(kingdom => kingdom.StringId == kingdomId)?.IsEliminated != false;

        private static void DeclareWarOnTarget(Invasion invasion, InvasionConquest conquest)
        {
            IFaction? invader = invasion.FindLeadingClan()?.MapFaction;
            Kingdom? target = Kingdom.All.FirstOrDefault(kingdom => kingdom.StringId == conquest.TargetKingdomId);

            if (invader != null && target != null && !target.IsEliminated && invader != target &&
                !invader.IsAtWarWith(target))
            {
                DeclareWarAction.ApplyByDefault(invader, target);
            }
        }

        // Turns a conquest whose target has fallen on the next; false while no kingdom borders the invasion.
        private bool Retarget(Invasion invasion, InvasionConquest conquest)
        {
            IFaction? invader = invasion.FindLeadingClan()?.MapFaction;
            IReadOnlyList<InvasionNeighbour> neighbours = invader == null
                ? new InvasionNeighbour[0]
                : FindNeighbours(invader);
            string? kingdomId = _invasionConquests.FindNextTarget(neighbours);

            if (kingdomId == null)
            {
                Logger.Info("Invasion conquest: no neighbour to turn on spawn=" + invasion.SpawnTemplateId +
                            " fallen=" + conquest.TargetKingdomId + " day=" + CampaignTime.Now.ToDays);
                return false;
            }

            Logger.Info("Invasion conquest: retargeted spawn=" + invasion.SpawnTemplateId +
                        " fallen=" + conquest.TargetKingdomId + " target=" + kingdomId +
                        " fiefs=" + invader!.Fiefs.Count + " neighbours=[" + string.Join(";",
                            neighbours.Select(neighbour => neighbour.KingdomId + ":" + neighbour.Strength.ToString("0"))) +
                        "] day=" + CampaignTime.Now.ToDays);
            conquest.Retarget(kingdomId, CampaignTime.Now.ToDays);
            return true;
        }

        // The kingdoms holding a fortification next to one of the invader's, as vanilla draws a realm's borders.
        private static IReadOnlyList<InvasionNeighbour> FindNeighbours(IFaction invader) =>
            invader.Fiefs
                .SelectMany(fief => fief.GetNeighborFortifications(MobileParty.NavigationType.All))
                .Select(settlement => settlement.MapFaction)
                .OfType<Kingdom>()
                .Where(kingdom => !kingdom.IsEliminated && kingdom != invader)
                .Distinct()
                .Select(kingdom => new InvasionNeighbour(kingdom.StringId, kingdom.CurrentTotalStrength))
                .ToList();

        // Only an invasion whose fiefs can all hold reaches past the wars it already has.
        private void OpenFront(Invasion invasion, InvasionConquest conquest, InvasionConquestRules rules)
        {
            IFaction? invader = invasion.FindLeadingClan()?.MapFaction;

            if (invader == null || invader.Fiefs.Count == 0 || !_bannerlordInvasionFiefGarrisons.CanExpand(invasion))
            {
                return;
            }

            string? kingdomId = _invasionConquests.FindFrontToOpen(conquest, rules, FindPrey(invader),
                CampaignTime.Now.ToDays);
            Kingdom? kingdom = Kingdom.All.FirstOrDefault(candidate => candidate.StringId == kingdomId);

            if (kingdom == null)
            {
                return;
            }

            conquest.OpenFront(CampaignTime.Now.ToDays);
            DeclareWarAction.ApplyByDefault(invader, kingdom);
            Logger.Info("Invasion conquest: front opened spawn=" + invasion.SpawnTemplateId +
                        " kingdom=" + kingdom.StringId + " fiefs=" + invader.Fiefs.Count);
        }

        // Every kingdom's fief, measured from whichever of the invader's fiefs lies nearest to it.
        private static IEnumerable<InvasionPrey> FindPrey(IFaction invader)
        {
            List<Vec2> holdings = invader.Fiefs.Select(town => town.Settlement.GatePosition.ToVec2()).ToList();

            return Town.AllFiefs
                .Where(town => town.MapFaction is Kingdom { IsEliminated: false } && town.MapFaction != invader)
                .Select(town => new InvasionPrey(town.MapFaction.StringId,
                    holdings.Min(holding => holding.Distance(town.Settlement.GatePosition.ToVec2())),
                    town.MapFaction.IsAtWarWith(invader)))
                .ToList();
        }

        private void LogThreat(Invasion invasion, InvasionConquest conquest)
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
                        _invasionConquests.FindThreat(
                            _bannerlordInvasionConfigurations.FindConquestRules(invasion.SpawnTemplateId)!.Value,
                            invader.Fiefs.Count,
                            Campaign.Current.Models.DiplomacyModel.GetValueOfSettlementsForFaction(invader)) +
                        " threshold=" +
                        Campaign.Current.Models.DiplomacyModel.GetDecisionMakingThreshold(invader) +
                        " wars=[" + string.Join(";", wars) + "]");
        }
    }
}
