using System;
using CustomSpawns.Data.Dto;
using CustomSpawns.Spawn;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace CustomSpawns.AI.Behaviours
{
    public interface IPriorityPartyBehaviour : ISyncsCampaignData
    {
        Type ConfigurationType { get; }
        PartyBehaviourResult GetPriorityBehaviour(MobileParty party, CsPartyData partyData,
            PriorityPartyBehaviourData configuration);
    }

    public abstract class PriorityPartyBehaviour<TConfiguration> : IPriorityPartyBehaviour
        where TConfiguration : PriorityPartyBehaviourData
    {
        public Type ConfigurationType => typeof(TConfiguration);

        /**
         * The registry dispatches on the exact configuration type a behaviour declares, so the cast cannot
         * fail for anything the resolver hands over.
         */
        public PartyBehaviourResult GetPriorityBehaviour(MobileParty party, CsPartyData partyData,
            PriorityPartyBehaviourData configuration)
        {
            return GetPriorityBehaviour(party, partyData, (TConfiguration)configuration);
        }

        public abstract PartyBehaviourResult GetPriorityBehaviour(MobileParty party, CsPartyData partyData,
            TConfiguration configuration);

        public virtual void SyncData(IDataStore dataStore)
        {
        }
    }

    public interface IFallbackPartyBehaviour : ISyncsCampaignData
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
