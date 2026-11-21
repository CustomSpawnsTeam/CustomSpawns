using System;
using System.Collections.Generic;
using CustomSpawns.AI.Model;
using CustomSpawns.AI.Offensives;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace CustomSpawns.Invasions.PlayerSparing.Adapters
{
    /**
     * Rules the settlements the player holds out of an invasion's first landing, on top of whatever the policy it
     * wraps allows. With every candidate ruled out the party has no siege target, and falls back to raiding and
     * patrolling until one opens up, as it does when no coast is in reach.
     */
    public sealed class InvasionPlayerSparingSiegeTargetPolicy : ISiegeTargetPolicy
    {
        private readonly ISiegeTargetPolicy _siegeTargetPolicy;
        private readonly IInvasionLandingSparing _invasionLandingSparing;

        public InvasionPlayerSparingSiegeTargetPolicy(ISiegeTargetPolicy siegeTargetPolicy,
            IInvasionLandingSparing invasionLandingSparing)
        {
            _siegeTargetPolicy = siegeTargetPolicy;
            _invasionLandingSparing = invasionLandingSparing;
        }

        public bool IsCandidateAllowed(MobileParty party, SiegeTargetCandidate candidate) =>
            _invasionLandingSparing.MayBesiege(party, candidate.Target) &&
            _siegeTargetPolicy.IsCandidateAllowed(party, candidate);

        // An assault already under way is fought out, as the wrapped policy lets it be.
        public bool IsExistingTargetAllowed(MobileParty party, Settlement target, bool matchingNativeAssault) =>
            (matchingNativeAssault || _invasionLandingSparing.MayBesiege(party, target)) &&
            _siegeTargetPolicy.IsExistingTargetAllowed(party, target, matchingNativeAssault);

        public bool IsJoinableSiege(MobileParty party, Settlement settlement) =>
            _siegeTargetPolicy.IsJoinableSiege(party, settlement);

        public Func<SiegeTargetCandidate, float> GetTargetDistance(MobileParty party) =>
            _siegeTargetPolicy.GetTargetDistance(party);

        public IReadOnlyList<MobileParty> GetAttackingForce(MobileParty party) =>
            _siegeTargetPolicy.GetAttackingForce(party);

        public bool GathersBeforeAssault(MobileParty party) => _siegeTargetPolicy.GathersBeforeAssault(party);
    }
}
