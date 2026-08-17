using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using CustomSpawns.Data.Model;
using CustomSpawns.ModIntegration;
using CustomSpawns.Utils;
using Path = System.IO.Path;

namespace CustomSpawns.Data.Reader.Impl
{
    public class PartyBehaviourDataReader
        : AbstractDataReader<PartyBehaviourDataReader, IList<PriorityPartyBehaviourTemplate>>
    {
        private readonly MessageBoxService _messageBoxService;
        private readonly IList<PriorityPartyBehaviourTemplate> _templates;

        public PartyBehaviourDataReader(SubModService subModService, MessageBoxService messageBoxService)
        {
            _messageBoxService = messageBoxService;
            _templates = LoadAllPartyBehaviourDataFromAllSubMods(subModService);
        }

        public override IList<PriorityPartyBehaviourTemplate> Data => _templates;

        private IList<PriorityPartyBehaviourTemplate> LoadAllPartyBehaviourDataFromAllSubMods(
            SubModService subModService)
        {
            List<PriorityPartyBehaviourTemplate> templates = new();
            foreach (var subMod in subModService.GetAllLoadedSubMods())
            {
                string filePath = Path.Combine(subMod.CustomSpawnsDirectoryPath, "PartyBehaviours.xml");
                if (!File.Exists(filePath))
                {
                    continue;
                }

                using Stream stream = new FileStream(filePath, FileMode.Open);
                DeserializeInto(stream, templates,
                    error => _messageBoxService.ShowCustomSpawnsErrorMessage(error, "the parsing of " + filePath));
            }

            return templates;
        }

        internal static void DeserializeInto(Stream stream, IList<PriorityPartyBehaviourTemplate> templates,
            Action<System.Exception> onError)
        {
            XmlSerializer serialiser = new XmlSerializer(typeof(PriorityPartyBehaviourTemplates));
            PriorityPartyBehaviourTemplates document;
            try
            {
                document = serialiser.Deserialize(stream) as PriorityPartyBehaviourTemplates
                           ?? throw new InvalidOperationException(
                               "Party behaviour data XML root deserialized to null.");
            }
            catch (InvalidOperationException error)
            {
                onError(error);
                return;
            }

            foreach (PriorityPartyBehaviourTemplate template in document.AllTemplates ??
                                                                new List<PriorityPartyBehaviourTemplate>())
            {
                string? id = template.Id;
                if (string.IsNullOrWhiteSpace(id))
                {
                    onError(new ArgumentException("PriorityPartyBehaviourTemplate has a blank Id."));
                    continue;
                }

                if (!template.PriorityPartyBehavioursSpecified)
                {
                    onError(new ArgumentException("PriorityPartyBehaviourTemplate '" + id +
                                                  "' is missing its PriorityPartyBehaviours wrapper."));
                    continue;
                }

                if (templates.Any(known => string.Equals(known.Id, id, StringComparison.Ordinal)))
                {
                    onError(new ArgumentException("Duplicate PriorityPartyBehaviourTemplate Id '" + id + "'."));
                    continue;
                }

                templates.Add(template);
            }
        }
    }
}
