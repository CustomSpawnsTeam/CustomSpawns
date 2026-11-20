using System;
using CustomSpawns.Spawn;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace CustomSpawns.AI.Behaviours
{
    /**
     * Makes a party spawned alongside a main party escort it, the same way a party of an army follows its leader.
     * It is not applicable when the main party is gone, so the escorts fall back to their own default behaviour.
     */
    public sealed class FollowSupportedPartyBehaviour : ISupportingPartyBehaviour
    {
        private readonly Func<MobileParty, bool> _isActive;
        private readonly Func<MobileParty, MobileParty.NavigationType> _getNavigationCapability;

        public FollowSupportedPartyBehaviour()
            : this(party => party.IsActive, party => party.NavigationCapability)
        {
        }

        public FollowSupportedPartyBehaviour(Func<MobileParty, bool> isActive,
            Func<MobileParty, MobileParty.NavigationType> getNavigationCapability)
        {
            _isActive = isActive;
            _getNavigationCapability = getNavigationCapability;
        }

        public static bool IsWithinSupportRange(float distanceSquared, float seeingRange)
        {
            return distanceSquared <= seeingRange * seeingRange;
        }

        public PartyBehaviourResult GetPriorityBehaviour(MobileParty party, CsPartyData partyData)
        {
            MobileParty? supportedParty = partyData.SupportedParty;
            if (supportedParty is null || ReferenceEquals(supportedParty, party) || !_isActive(supportedParty))
            {
                return PartyBehaviourResult.NotApplicable;
            }

            return PartyBehaviourResult.From(new AIBehaviorData(supportedParty, AiBehavior.EscortParty,
                _getNavigationCapability(party), false, false, false));
        }
    }
}
