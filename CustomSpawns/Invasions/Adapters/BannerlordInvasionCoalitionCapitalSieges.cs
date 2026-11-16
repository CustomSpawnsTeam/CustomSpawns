using System.Collections.Generic;
using System.Linq;
using NLog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;

namespace CustomSpawns.Invasions.Adapters
{
    /**
     * Steers a lord of a coalition to a cut-off capital of the line. Vanilla opens a siege only with twice the
     * defenders' strength of its own, which no realm's army has against a capital's garrison swelled with the waves it
     * banks, so the capital is judged against the whole coalition's strength around it.
     */
    public sealed class BannerlordInvasionCoalitionCapitalSieges
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly BannerlordInvasionAlliedSieges _bannerlordInvasionAlliedSieges;
        private readonly BannerlordInvasionCoalitionForces _bannerlordInvasionCoalitionForces;
        private readonly BannerlordInvasionSiegeOffers _bannerlordInvasionSiegeOffers;
        private readonly InvasionCoalitionWarScores _invasionCoalitionWarScores;

        public BannerlordInvasionCoalitionCapitalSieges(BannerlordInvasionAlliedSieges bannerlordInvasionAlliedSieges,
            BannerlordInvasionCoalitionForces bannerlordInvasionCoalitionForces,
            BannerlordInvasionSiegeOffers bannerlordInvasionSiegeOffers,
            InvasionCoalitionWarScores invasionCoalitionWarScores)
        {
            _bannerlordInvasionAlliedSieges = bannerlordInvasionAlliedSieges;
            _bannerlordInvasionCoalitionForces = bannerlordInvasionCoalitionForces;
            _bannerlordInvasionSiegeOffers = bannerlordInvasionSiegeOffers;
            _invasionCoalitionWarScores = invasionCoalitionWarScores;
        }

        // A cut-off capital of the line, weighed by vanilla or not, is wanted above every other siege in reach.
        public void PreferCapitalSiege(MobileParty party, PartyThinkParams thinkParams, Settlement capital,
            bool willGatherArmy, float strength, List<(AIBehaviorData Behavior, float Score)> sieges, float bestScore,
            float townDistance)
        {
            (AIBehaviorData Behavior, float Score) weighed =
                sieges.FirstOrDefault(siege => siege.Behavior.Party == capital);
            bool isWeighed = weighed.Behavior.Party != null;

            if (isWeighed && weighed.Score > 0f)
            {
                thinkParams.SetBehaviorScore(weighed.Behavior,
                    _invasionCoalitionWarScores.FindCapitalScore(weighed.Score, true, bestScore));
                return;
            }

            if (!_invasionCoalitionWarScores.IsCapitalInReach(capital.Position.Distance(party.Position),
                    townDistance) || !party.MapFaction.IsAtWarWith(capital.MapFaction))
            {
                return;
            }

            BesiegerCamp? camp = _bannerlordInvasionAlliedSieges.FindAlliedCamp(party, capital);

            // A siege of the capital by anyone outside the coalition cannot be joined.
            if (camp == null && capital.SiegeEvent != null && party.BesiegedSettlement != capital)
            {
                return;
            }

            float coalitionStrength = FindCoalitionStrength(party, thinkParams, capital, willGatherArmy,
                strength + (camp?.GetInvolvedPartiesForEventType().Sum(camper => camper.EstimatedStrength) ?? 0f),
                townDistance);
            float score = _invasionCoalitionWarScores.FindCapitalScore(
                _bannerlordInvasionSiegeOffers.FindTargetScore(party, capital, coalitionStrength), false, bestScore);

            if (score <= 0f)
            {
                return;
            }

            if (isWeighed)
            {
                thinkParams.SetBehaviorScore(weighed.Behavior, score);
            }
            else if (_bannerlordInvasionSiegeOffers.AddSiege(party, thinkParams, capital, willGatherArmy, score) <= 0f)
            {
                return;
            }

            Logger.Debug("Invasion coalition capital siege weighed: party=" + party.StringId + " capital=" +
                         capital.StringId + " army=" + willGatherArmy + " strength=" + strength +
                         " coalition_strength=" + coalitionStrength + " score=" + score);
        }

        // The lord's strength, with its camp's, and that of every free force of the coalition in reach of the capital.
        private float FindCoalitionStrength(MobileParty party, PartyThinkParams thinkParams, Settlement capital,
            bool willGatherArmy, float strength, float townDistance)
        {
            ICollection<MobileParty> counted = willGatherArmy && thinkParams.PossibleArmyMembersUponArmyCreation != null
                ? new HashSet<MobileParty>(thinkParams.PossibleArmyMembersUponArmyCreation)
                : new HashSet<MobileParty>();

            return _invasionCoalitionWarScores.FindCoalitionSiegeStrength(strength,
                _bannerlordInvasionCoalitionForces.FindStrengthsAround(capital, party, counted,
                    distance => _invasionCoalitionWarScores.IsCampInReach(distance, townDistance)));
        }
    }
}
