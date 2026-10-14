using System;
using CustomSpawns.Data.Dto;
using CustomSpawns.Data.Model;

namespace CustomSpawns.Data.Adapter
{
    /**
     * Adapts one kind of priority party behaviour configuration into the data its AI behaviour reads.
     * One implementation per behaviour keeps each behaviour's defaults and validation in its own file,
     * mirroring how IPriorityPartyBehaviour gives each behaviour its own provider.
     */
    public interface IPriorityPartyBehaviourConfigAdapter
    {
        Type ConfigurationType { get; }
        PriorityPartyBehaviourData Adapt(PriorityPartyBehaviourConfig configuration);
    }

    public abstract class PriorityPartyBehaviourConfigAdapter<TConfiguration>
        : IPriorityPartyBehaviourConfigAdapter
        where TConfiguration : PriorityPartyBehaviourConfig
    {
        public Type ConfigurationType => typeof(TConfiguration);

        public PriorityPartyBehaviourData Adapt(PriorityPartyBehaviourConfig configuration)
        {
            if (!(configuration is TConfiguration typedConfiguration))
            {
                throw new ArgumentException($"{GetType().Name} cannot handle {configuration.GetType().Name}.",
                    nameof(configuration));
            }

            return Adapt(typedConfiguration);
        }

        protected abstract PriorityPartyBehaviourData Adapt(TConfiguration configuration);
    }
}
