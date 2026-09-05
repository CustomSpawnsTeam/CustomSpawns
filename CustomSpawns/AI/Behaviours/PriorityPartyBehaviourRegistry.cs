using System;
using System.Collections.Generic;
using CustomSpawns.Data.Dto;

namespace CustomSpawns.AI.Behaviours
{
    public sealed class PriorityPartyBehaviourRegistry
    {
        private readonly IReadOnlyDictionary<Type, IPriorityPartyBehaviour> _behaviours;

        public PriorityPartyBehaviourRegistry(IEnumerable<IPriorityPartyBehaviour> behaviours)
        {
            var registry = new Dictionary<Type, IPriorityPartyBehaviour>();
            foreach (IPriorityPartyBehaviour behaviour in behaviours ??
                     throw new ArgumentNullException(nameof(behaviours)))
            {
                if (behaviour is null)
                {
                    throw new ArgumentException("Priority party behaviour providers cannot contain null.",
                        nameof(behaviours));
                }

                Type configurationType = behaviour.ConfigurationType;
                if (!typeof(PriorityPartyBehaviourData).IsAssignableFrom(configurationType))
                {
                    throw new ArgumentException($"{behaviour.GetType().Name} registered unsupported configuration " +
                                                $"type {configurationType.FullName}.", nameof(behaviours));
                }

                if (registry.ContainsKey(configurationType))
                {
                    throw new ArgumentException($"A provider is already registered for {configurationType.FullName}.",
                        nameof(behaviours));
                }

                registry.Add(configurationType, behaviour);
            }

            _behaviours = registry;
        }

        public bool TryGet(Type configurationType, out IPriorityPartyBehaviour behaviour)
        {
            return _behaviours.TryGetValue(configurationType, out behaviour!);
        }
    }
}
