using System;
using System.Collections.Generic;
using CustomSpawns.AI.Model;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace CustomSpawns.AI.Offensives
{
    public interface ISiegeTargetPolicy
    {
        bool IsCandidateAllowed(MobileParty party, SiegeTargetCandidate candidate);
        bool IsExistingTargetAllowed(MobileParty party, Settlement target, bool matchingNativeAssault);

        // Whether a siege somebody else is already running against this settlement is one this party may
        // join, so that it supports the siege instead of the settlement being ruled out as a target.
        bool IsJoinableSiege(MobileParty party, Settlement settlement);

        // The ordering key target selection ranks candidates by, resolved once per selection.
        Func<SiegeTargetCandidate, float> GetTargetDistance(MobileParty party);

        // The parties whose strength a garrison is weighed against: whoever attacks the town together.
        IReadOnlyList<MobileParty> GetAttackingForce(MobileParty party);

        // Whether this party commits to an objective before it is strong enough to take it, massing on
        // the objective and asking the ratio again of the force that has arrived.
        bool GathersBeforeAssault(MobileParty party);
    }

    public sealed class NoRestrictionSiegeTargetPolicy : ISiegeTargetPolicy
    {
        public bool IsCandidateAllowed(MobileParty party, SiegeTargetCandidate candidate) => true;

        public bool IsExistingTargetAllowed(MobileParty party, Settlement target, bool matchingNativeAssault) => true;

        public bool IsJoinableSiege(MobileParty party, Settlement settlement) => false;

        public Func<SiegeTargetCandidate, float> GetTargetDistance(MobileParty party) =>
            candidate => candidate.Distance;

        public IReadOnlyList<MobileParty> GetAttackingForce(MobileParty party) => new[] { party };

        public bool GathersBeforeAssault(MobileParty party) => false;
    }
}
