using System.Collections.Generic;
using System.Linq;
using CustomSpawns.Invasions.Alliances.Adapters;
using CustomSpawns.Invasions.Conquest.Adapters;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;

namespace CustomSpawns.Invasions.Coalitions.Adapters
{
    /**
     * Steers the lords of a coalition to retake the line's fiefs together: they would rather besiege the line than
     * anyone else, join the camps the other kingdoms of the coalition lay there, and turn on a capital of the line
     * once it is cut off.
     */
    public sealed class BannerlordInvasionCoalitionSieges
    {
        private readonly BannerlordInvasionCoalitionFronts _bannerlordInvasionCoalitionFronts;
        private readonly BannerlordInvasionAlliedSieges _bannerlordInvasionAlliedSieges;
        private readonly BannerlordInvasionCoalitionCapitalSieges _bannerlordInvasionCoalitionCapitalSieges;
        private readonly BannerlordInvasionSiegeOffers _bannerlordInvasionSiegeOffers;
        private readonly InvasionCoalitionWarScores _invasionCoalitionWarScores;

        public BannerlordInvasionCoalitionSieges(BannerlordInvasionCoalitionFronts bannerlordInvasionCoalitionFronts,
            BannerlordInvasionAlliedSieges bannerlordInvasionAlliedSieges,
            BannerlordInvasionCoalitionCapitalSieges bannerlordInvasionCoalitionCapitalSieges,
            BannerlordInvasionSiegeOffers bannerlordInvasionSiegeOffers,
            InvasionCoalitionWarScores invasionCoalitionWarScores)
        {
            _bannerlordInvasionCoalitionFronts = bannerlordInvasionCoalitionFronts;
            _bannerlordInvasionAlliedSieges = bannerlordInvasionAlliedSieges;
            _bannerlordInvasionCoalitionCapitalSieges = bannerlordInvasionCoalitionCapitalSieges;
            _bannerlordInvasionSiegeOffers = bannerlordInvasionSiegeOffers;
            _invasionCoalitionWarScores = invasionCoalitionWarScores;
        }

        public void PreferLineSieges(MobileParty party, PartyThinkParams thinkParams)
        {
            if (party.IsMainParty || !_bannerlordInvasionCoalitionFronts.IsMember(party.MapFaction))
            {
                return;
            }

            float strength = party.GetTotalLandStrengthWithFollowers();
            PreferLineSieges(party, thinkParams, false, strength);

            // Vanilla only fills the possible army members of a lord that can call an army this hour.
            if (thinkParams.PossibleArmyMembersUponArmyCreation != null)
            {
                float armyStrength = strength + thinkParams.PossibleArmyMembersUponArmyCreation.Sum(member =>
                    member.Party.GetCustomStrength(BattleSideEnum.Attacker, MapEvent.PowerCalculationContext.Siege));

                PreferLineSieges(party, thinkParams, true, armyStrength);
            }
        }

        /**
         * The sieges a lord weighs on its own, or with the army it could call. Vanilla weighs no siege another
         * kingdom lays, nor one of a fief far from the lord's realm, so the camps of the coalition and a cut-off
         * capital beyond the border are offered here.
         */
        private void PreferLineSieges(MobileParty party, PartyThinkParams thinkParams, bool willGatherArmy,
            float strength)
        {
            List<(AIBehaviorData Behavior, float Score)> sieges = thinkParams.AIBehaviorScores
                .Where(entry => entry.Item1.AiBehavior == AiBehavior.BesiegeSettlement &&
                                entry.Item1.WillGatherArmy == willGatherArmy && entry.Item1.Party is Settlement)
                .Select(entry => (entry.Item1, entry.Item2))
                .ToList();

            // No siege weighed at all: vanilla ruled sieges out for this lord, for food or cohesion.
            if (sieges.Count == 0 || party.MapFaction.FactionMidSettlement == null)
            {
                return;
            }

            IReadOnlyCollection<Settlement> capitals = _bannerlordInvasionCoalitionFronts.FindIsolatedCapitals();
            float bestOtherScore = sieges.Where(siege => !IsLineFief((Settlement)siege.Behavior.Party))
                .Select(siege => siege.Score).DefaultIfEmpty(0f).Max();
            float bestScore = bestOtherScore;

            foreach ((AIBehaviorData behavior, float score) in sieges.Where(siege =>
                         IsLineFief((Settlement)siege.Behavior.Party) &&
                         !capitals.Contains((Settlement)siege.Behavior.Party)))
            {
                float lineScore = _invasionCoalitionWarScores.FindLineSiegeScore(score, bestOtherScore);
                thinkParams.SetBehaviorScore(behavior, lineScore);
                bestScore = System.Math.Max(bestScore, lineScore);
            }

            HashSet<Settlement> weighed = new(sieges.Select(siege => (Settlement)siege.Behavior.Party));
            float townDistance =
                Campaign.Current.GetAverageDistanceBetweenClosestTwoTownsWithNavigationType(MobileParty.NavigationType
                    .All);

            foreach (Settlement fief in Settlement.All.Where(settlement => settlement.IsFortification &&
                         !weighed.Contains(settlement) && !capitals.Contains(settlement) && IsLineFief(settlement) &&
                         _invasionCoalitionWarScores.IsCampInReach(
                             settlement.Position.Distance(party.Position), townDistance)))
            {
                BesiegerCamp? camp = _bannerlordInvasionAlliedSieges.FindAlliedCamp(party, fief);

                if (camp != null)
                {
                    float campStrength = strength + camp.GetInvolvedPartiesForEventType()
                        .Sum(camper => camper.EstimatedStrength);
                    float campScore = _invasionCoalitionWarScores.FindCampScore(
                        _bannerlordInvasionSiegeOffers.FindTargetScore(party, fief, campStrength), bestScore);
                    bestScore = System.Math.Max(bestScore,
                        _bannerlordInvasionSiegeOffers.AddSiege(party, thinkParams, fief, willGatherArmy, campScore));
                }
            }

            foreach (Settlement capital in capitals)
            {
                _bannerlordInvasionCoalitionCapitalSieges.PreferCapitalSiege(party, thinkParams, capital,
                    willGatherArmy, strength, sieges, bestScore, townDistance);
            }
        }

        private bool IsLineFief(Settlement settlement) =>
            _bannerlordInvasionCoalitionFronts.IsLineRealm(settlement.MapFaction);
    }
}
