using System;
using System.Collections.Generic;
using CustomSpawns.Data.Model;

namespace CustomSpawns.Data.Reader.Impl
{
    internal static class PriorityPartyBehaviourTemplateResolver
    {
        internal static void Resolve(Spawns document)
        {
            Dictionary<string, PriorityPartyBehaviourTemplate> templates =
                new(StringComparer.Ordinal);

            foreach (PriorityPartyBehaviourTemplate template in document.PriorityPartyBehaviourTemplates ??
                                                                 new List<PriorityPartyBehaviourTemplate>())
            {
                string? id = template.Id;
                if (string.IsNullOrWhiteSpace(id))
                {
                    throw new ArgumentException("PriorityPartyBehaviourTemplate has a blank Id.");
                }

                if (!template.PriorityPartyBehavioursSpecified)
                {
                    throw new ArgumentException("PriorityPartyBehaviourTemplate '" + id +
                                                "' is missing its PriorityPartyBehaviours wrapper.");
                }

                if (templates.ContainsKey(id!))
                {
                    throw new ArgumentException("Duplicate PriorityPartyBehaviourTemplate Id '" + id + "'.");
                }

                templates.Add(id!, template);
            }

            List<(Model.Spawn Spawn, PriorityPartyBehaviourTemplate Template)> references = new();
            foreach (Model.Spawn spawn in document.AllSpawns ?? new List<Model.Spawn>())
            {
                if (!spawn.PriorityPartyBehaviourTemplateIdSpecified)
                {
                    continue;
                }

                string? reference = spawn.PriorityPartyBehaviourTemplateId;
                string partyTemplate = spawn.PartyTemplate ?? "<blank>";
                if (spawn.PriorityPartyBehavioursSpecified)
                {
                    throw new ArgumentException("Spawn party_template '" + partyTemplate +
                                                "' cannot use inline PriorityPartyBehaviours and " +
                                                "PriorityPartyBehaviourTemplateId '" +
                                                (string.IsNullOrWhiteSpace(reference) ? "<blank>" : reference) + "'.");
                }

                if (string.IsNullOrWhiteSpace(reference))
                {
                    throw new ArgumentException("Spawn party_template '" + partyTemplate +
                                                "' has a blank PriorityPartyBehaviourTemplateId reference.");
                }

                if (!templates.TryGetValue(reference!, out PriorityPartyBehaviourTemplate template))
                {
                    throw new ArgumentException("Spawn party_template '" + partyTemplate +
                                                "' references unknown PriorityPartyBehaviourTemplate Id '" +
                                                reference + "'.");
                }

                references.Add((spawn, template));
            }

            foreach ((Model.Spawn spawn, PriorityPartyBehaviourTemplate template) in references)
            {
                spawn.PriorityPartyBehaviours = new List<PriorityPartyBehaviourConfig>(template.PriorityPartyBehaviours ??
                    new List<PriorityPartyBehaviourConfig>());
                spawn.PriorityPartyBehavioursSpecified = true;
            }
        }
    }
}
