using System;
using CustomSpawns.Data.Dto;
using CustomSpawns.Spawn;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace CustomSpawns.AI
{
    public interface IPriorityPartyBehaviour
    {
        Type ConfigurationType { get; }
        PartyBehaviourResult GetPriorityBehaviour(MobileParty party, CsPartyData partyData,
            PriorityPartyBehaviourData configuration);
        void SyncData(IDataStore dataStore);
    }

    public abstract class PriorityPartyBehaviour<TConfiguration> : IPriorityPartyBehaviour
        where TConfiguration : PriorityPartyBehaviourData
    {
        public Type ConfigurationType => typeof(TConfiguration);

        public PartyBehaviourResult GetPriorityBehaviour(MobileParty party, CsPartyData partyData,
            PriorityPartyBehaviourData configuration)
        {
            if (!(configuration is TConfiguration typedConfiguration))
            {
                throw new ArgumentException($"{GetType().Name} cannot handle {configuration.GetType().Name}.",
                    nameof(configuration));
            }

            return GetPriorityBehaviour(party, partyData, typedConfiguration);
        }

        public abstract PartyBehaviourResult GetPriorityBehaviour(MobileParty party, CsPartyData partyData,
            TConfiguration configuration);

        public virtual void SyncData(IDataStore dataStore)
        {
        }
    }

    public interface IFallbackPartyBehaviour
    {
        PartyBehaviourResult GetPriorityBehaviour(MobileParty party, CsPartyData partyData);
        void SyncData(IDataStore dataStore);
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
