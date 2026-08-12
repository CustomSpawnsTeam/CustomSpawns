using CustomSpawns.Spawn;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace CustomSpawns.AI
{
    public interface IPriorityPartyBehaviour
    {
        PartyBehaviourResult GetPriorityBehaviour(MobileParty party, CsPartyData partyData);
    }

    public readonly struct PartyBehaviourResult
    {
        public bool IsApplicable { get; }
        public AIBehaviorData? AIBehavior { get; }

        private PartyBehaviourResult(bool isApplicable, AIBehaviorData? aiBehavior)
        {
            IsApplicable = isApplicable;
            AIBehavior = aiBehavior;
        }

        public static PartyBehaviourResult NotApplicable => new(false, null);
        public static PartyBehaviourResult ApplicableWithoutBehavior => new(true, null);

        public static PartyBehaviourResult From(AIBehaviorData aiBehavior)
        {
            return new PartyBehaviourResult(true, aiBehavior);
        }
    }
}
