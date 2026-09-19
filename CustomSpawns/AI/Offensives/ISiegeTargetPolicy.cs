using System;
using System.Collections.Generic;
using CustomSpawns.AI.Model;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace CustomSpawns.AI.Offensives
{
    internal interface ISiegeTargetPolicy
    {
        bool IsCandidateAllowed(MobileParty party, SiegeTargetCandidate candidate);
        bool IsExistingTargetAllowed(MobileParty party, Settlement target, bool matchingNativeAssault);

        // The ordering key target selection ranks candidates by, resolved once per selection.
        Func<SiegeTargetCandidate, float> GetTargetDistance(MobileParty party);

        // The parties whose strength a garrison is weighed against: whoever attacks the town together.
        IReadOnlyList<MobileParty> GetAttackingForce(MobileParty party);
    }

    internal sealed class NoRestrictionSiegeTargetPolicy : ISiegeTargetPolicy
    {
        public bool IsCandidateAllowed(MobileParty party, SiegeTargetCandidate candidate) => true;

        public bool IsExistingTargetAllowed(MobileParty party, Settlement target, bool matchingNativeAssault) => true;

        public Func<SiegeTargetCandidate, float> GetTargetDistance(MobileParty party) =>
            candidate => candidate.Distance;

        public IReadOnlyList<MobileParty> GetAttackingForce(MobileParty party) => new[] { party };
    }
}
