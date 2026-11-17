using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CustomSpawns.Invasions.Adapters;
using CustomSpawns.Invasions.Alliances.Adapters;
using Helpers;
using NLog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;

namespace CustomSpawns.Invasions.Conquest.Adapters
{
    // Steers the lords of a conquering invasion to besiege the kingdom it conquers before any other.
    public sealed class BannerlordInvasionTargetSieges
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private const char Separator = '|';
        private readonly Func<InvasionState> _state;
        private readonly BannerlordInvasionConfigurations _bannerlordInvasionConfigurations;
        private readonly IInvasionConquestTargets _invasionConquestTargets;
        private readonly InvasionTargetSiegeScores _invasionTargetSiegeScores;
        private readonly BannerlordInvasionAlliedSieges _bannerlordInvasionAlliedSieges;
        private readonly Dictionary<string, int> _unborderedSiegeLogDays = new();

        public BannerlordInvasionTargetSieges(Func<InvasionState> state,
            BannerlordInvasionConfigurations bannerlordInvasionConfigurations,
            IInvasionConquestTargets invasionConquestTargets, InvasionTargetSiegeScores invasionTargetSiegeScores,
            BannerlordInvasionAlliedSieges bannerlordInvasionAlliedSieges)
        {
            _state = state;
            _bannerlordInvasionConfigurations = bannerlordInvasionConfigurations;
            _invasionConquestTargets = invasionConquestTargets;
            _invasionTargetSiegeScores = invasionTargetSiegeScores;
            _bannerlordInvasionAlliedSieges = bannerlordInvasionAlliedSieges;
        }

        // A lord of a conquering invasion would rather besiege the kingdom it conquers than anyone else.
        public void PreferTargetSieges(MobileParty party, PartyThinkParams thinkParams)
        {
            Invasion? invasion = _state().FindFightingInvasion(party.ActualClan?.StringId);
            InvasionConquestRules? rules = invasion == null
                ? null
                : _bannerlordInvasionConfigurations.FindConquestRules(invasion.SpawnTemplateId);
            string? targetKingdomId = invasion == null
                ? null
                : _invasionConquestTargets.FindConquestTargetKingdomId(invasion);

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
         * or it would leave the camp on its next thought. Nor does it weigh a siege an allied realm of the line lays,
         * which is offered here so the lord joins it.
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
                        _invasionTargetSiegeScores.FindTargetSiegeScore(rules, score, bestOtherScore));
                }
            }

            // Vanilla scores a siege from the middle of the realm's fiefs and throws for a realm with none, such as a
            // launched invasion that has only just landed; its first siege is left to vanilla.
            if (party.MapFaction.FactionMidSettlement == null)
            {
                return;
            }

            foreach (Settlement fief in targetKingdom.Settlements.Where(settlement => settlement.IsFortification &&
                         !weighed.Contains(settlement) && (settlement == besieged ||
                             (settlement.SiegeEvent == null && settlement.Party.MapEvent == null) ||
                             _bannerlordInvasionAlliedSieges.FindAlliedCamp(party, settlement) != null)))
            {
                bool isBesiegingIt = fief == besieged;
                BesiegerCamp? alliedCamp = isBesiegingIt
                    ? null
                    : _bannerlordInvasionAlliedSieges.FindAlliedCamp(party, fief);
                // Like vanilla, a lord weighs the siege it is in, or would join, with everyone in its camp.
                float fiefStrength = isBesiegingIt && party.BesiegerCamp != null
                    ? party.BesiegerCamp.GetInvolvedPartiesForEventType().Sum(camper => camper.EstimatedStrength)
                    : strength + (alliedCamp?.GetInvolvedPartiesForEventType()
                        .Sum(camper => camper.EstimatedStrength) ?? 0f);
                float targetScore = Campaign.Current.Models.TargetScoreCalculatingModel.GetTargetScoreForFaction(fief,
                    Army.ArmyTypes.Besieger, party, fiefStrength);
                float score = alliedCamp != null
                    ? _invasionTargetSiegeScores.FindAlliedTargetSiegeScore(rules, targetScore, bestTargetScore,
                        bestOtherScore)
                    : _invasionTargetSiegeScores.FindUnborderedTargetSiegeScore(rules, targetScore, bestTargetScore,
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
            Logger.Debug(string.Format(CultureInfo.InvariantCulture,
                "Invasion unbordered siege: fief={0} party={1} army={2} strength={3:0} target_score={4:0.####} " +
                "best_target={5:0.####} best_other={6:0.####} score={7:0.####} navigation={8} day={9}",
                fief.StringId, party.StringId, willGatherArmy, strength, targetScore, bestTargetScore,
                bestOtherScore, score, navigationType, day));
        }
    }
}
