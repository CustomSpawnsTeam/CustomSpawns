
using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;
using CustomSpawns.Data.Model.Dialogue;
using CustomSpawns.ModIntegration;
using CustomSpawns.Utils;

namespace CustomSpawns.Data.Reader.Impl
{
    public class DialogueDataReader : AbstractDataReader<DialogueDataReader, IList<Dialogue>>
    {
        private readonly MessageBoxService _messageBoxService;
        private readonly Model.Dialogue.Dialogues _rootDialogueData = new ();

        public DialogueDataReader(SubModService subModService, MessageBoxService messageBoxService)
        {
            _messageBoxService = messageBoxService;
            LoadDialogueDataFromAllSubMods(subModService);
        }

        public override IList<Dialogue> Data
        {
            get => _rootDialogueData.AllDialogues.AsReadOnly();
        }

        private void LoadDialogueDataFromAllSubMods(SubModService subModService)
        {
            foreach (var subMod in subModService.GetAllLoadedSubMods())
            {
                string path = Path.Combine(subMod.CustomSpawnsDirectoryPath, "CustomDialogue.xml");
                if (File.Exists(path))  
                {
                    try
                    {
                        IList<Dialogue> dialogues = ParseDialogueFile(path).AllDialogues;
                        _rootDialogueData.AllDialogues.AddRange(dialogues);
                    }
                    catch (ArgumentException e)
                    {
                        _messageBoxService.ShowCustomSpawnsErrorMessage(e, " Dialogue System XML loading");
                    }
                }
            }
        }

        private Model.Dialogue.Dialogues ParseDialogueFile(string filePath)
        {
            XmlSerializer serialiser = new XmlSerializer(typeof(Model.Dialogue.Dialogues));
            using Stream writer = new FileStream(filePath, FileMode.Open);
            try
            {
                return (Model.Dialogue.Dialogues) serialiser.Deserialize(writer);
            }
            catch (ArgumentException e)
            {
                _messageBoxService.ShowCustomSpawnsErrorMessage(e, "the parsing of " + filePath);
                return new Model.Dialogue.Dialogues
                {
                    AllDialogues = new()
                };
            }
        }
    }
}
