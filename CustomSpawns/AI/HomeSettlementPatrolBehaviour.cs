using CustomSpawns.Spawn;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace CustomSpawns.AI
{
    public sealed class HomeSettlementPatrolBehaviour : IFallbackPartyBehaviour
    {
        public PartyBehaviourResult GetPriorityBehaviour(MobileParty party, CsPartyData partyData)
        {
            Settlement? patrolTarget = party.HomeSettlement;
            if (patrolTarget is null)
            {
                return PartyBehaviourResult.ApplicableWithoutBehavior;
            }

            return PartyBehaviourResult.From(new AIBehaviorData(patrolTarget,
                AiBehavior.PatrolAroundPoint, party.NavigationCapability, false, false, false));
        }

        public void SyncData(IDataStore dataStore)
        {
        }
    }
}
