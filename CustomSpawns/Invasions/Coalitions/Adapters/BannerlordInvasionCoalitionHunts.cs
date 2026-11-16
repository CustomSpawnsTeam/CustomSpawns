using System.Collections.Generic;
using System.Linq;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Map;
using TaleWorlds.CampaignSystem.Party;

namespace CustomSpawns.Invasions.Coalitions.Adapters
{
    /**
     * Sends the lords and armies of a coalition after the line's field parties they can beat, together with the
     * coalition's parties already on them. Vanilla only goes after enemies near the lord's own fiefs, shies from those
     * near the enemy's, and never sends an attacking army after one.
     */
    public sealed class BannerlordInvasionCoalitionHunts
    {
        // How far vanilla looks for a party to go after, in encounter distances.
        private const float SightInEncounterDistances = 45f;
        private const float MinimumPreyAggressiveness = 0.1f;
        private readonly BannerlordInvasionCoalitionFronts _bannerlordInvasionCoalitionFronts;
        private readonly InvasionCoalitionWarScores _invasionCoalitionWarScores;

        public BannerlordInvasionCoalitionHunts(BannerlordInvasionCoalitionFronts bannerlordInvasionCoalitionFronts,
            InvasionCoalitionWarScores invasionCoalitionWarScores)
        {
            _bannerlordInvasionCoalitionFronts = bannerlordInvasionCoalitionFronts;
            _invasionCoalitionWarScores = invasionCoalitionWarScores;
        }

        // A lord in a siege, or riding in another's army, is left to vanilla.
        public void HuntLineParties(MobileParty party, PartyThinkParams thinkParams)
        {
            if (party.IsMainParty || party.LeaderHero == null || party.IsCaravan || party.IsCurrentlyAtSea ||
                (party.Army != null && party.Army.LeaderParty != party) || party.BesiegerCamp != null ||
                party.CurrentSettlement?.SiegeEvent != null ||
                !_bannerlordInvasionCoalitionFronts.IsMember(party.MapFaction))
            {
                return;
            }

            float sight = Campaign.Current.Models.EncounterModel.NeededMaximumLandDistanceForEncounteringMobileParty *
                          SightInEncounterDistances;
            List<MobileParty> preys = FindPartiesAround(party, sight).Where(candidate => IsPrey(party, candidate))
                .ToList();

            if (preys.Count == 0)
            {
                return;
            }

            float bestOtherScore = thinkParams.AIBehaviorScores
                .Where(entry => entry.Item1.AiBehavior != AiBehavior.GoAroundParty)
                .Select(entry => entry.Item2).DefaultIfEmpty(0f).Max();
            float strength = party.Army?.EstimatedStrength ?? party.Party.EstimatedStrength;

            foreach (MobileParty prey in preys)
            {
                Hunt(party, thinkParams, prey, sight, strength, bestOtherScore);
            }
        }

        private void Hunt(MobileParty party, PartyThinkParams thinkParams, MobileParty prey, float sight,
            float strength, float bestOtherScore)
        {
            MobileParty.NavigationType navigationType;
            float distance;

            if (party.CurrentSettlement == null)
            {
                AiHelper.GetBestNavigationTypeAndDistanceOfMobilePartyForMobileParty(party, prey,
                    out navigationType, out distance);
            }
            else
            {
                AiHelper.GetBestNavigationTypeAndAdjustedDistanceOfSettlementForMobileParty(prey,
                    party.CurrentSettlement, false, out navigationType, out distance, out bool _);
            }

            if (navigationType == MobileParty.NavigationType.None || distance >= sight)
            {
                return;
            }

            (AIBehaviorData Behavior, float Score) weighed = thinkParams.AIBehaviorScores
                .Where(entry => entry.Item1.AiBehavior == AiBehavior.GoAroundParty && entry.Item1.Party == prey)
                .Select(entry => (entry.Item1, entry.Item2))
                .FirstOrDefault();
            float huntersStrength = strength + FindPartiesAround(prey, sight)
                .Where(hunter => IsHunting(party, hunter, prey))
                .Sum(hunter => hunter.Army?.EstimatedStrength ?? hunter.Party.EstimatedStrength);
            float preyStrength = prey.Army?.EstimatedStrength ?? prey.Party.EstimatedStrength;
            float score = _invasionCoalitionWarScores.FindHuntScore(weighed.Score, bestOtherScore,
                1f - distance / sight, huntersStrength, preyStrength);

            if (weighed.Behavior.Party != null)
            {
                thinkParams.SetBehaviorScore(weighed.Behavior, score);
            }
            else if (score > 0f)
            {
                thinkParams.AddBehaviorScore((new AIBehaviorData(prey, AiBehavior.GoAroundParty, navigationType,
                    false, false, false), score));
            }
        }

        // A field party of the line at war with the lord, leading its own army if in one, out of any fortification.
        private bool IsPrey(MobileParty party, MobileParty candidate) =>
            candidate.IsActive && candidate.IsLordParty && !candidate.IsCurrentlyAtSea &&
            (candidate.Army == null || candidate.Army.LeaderParty == candidate) &&
            candidate.CurrentSettlement == null && candidate.Aggressiveness > MinimumPreyAggressiveness &&
            !candidate.ShouldBeIgnored && _bannerlordInvasionCoalitionFronts.IsLineRealm(candidate.MapFaction) &&
            candidate.MapFaction.IsAtWarWith(party.MapFaction);

        // Another party of the coalition, leading its own army if in one, already going after the prey.
        private bool IsHunting(MobileParty party, MobileParty hunter, MobileParty prey) =>
            hunter != party && (hunter.Army == null || hunter.Army.LeaderParty == hunter) &&
            ((hunter.DefaultBehavior == AiBehavior.GoAroundParty && hunter.TargetParty == prey) ||
             (hunter.ShortTermBehavior == AiBehavior.EngageParty && hunter.ShortTermTargetParty == prey)) &&
            _bannerlordInvasionCoalitionFronts.IsMember(hunter.MapFaction);

        private static IEnumerable<MobileParty> FindPartiesAround(MobileParty center, float radius)
        {
            LocatableSearchData<MobileParty> data =
                MobileParty.StartFindingLocatablesAroundPosition(center.Position.ToVec2(), radius);

            for (MobileParty found = MobileParty.FindNextLocatable(ref data);
                 found != null;
                 found = MobileParty.FindNextLocatable(ref data))
            {
                yield return found;
            }
        }
    }
}
