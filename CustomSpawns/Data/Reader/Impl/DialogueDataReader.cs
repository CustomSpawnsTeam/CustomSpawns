
using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using CustomSpawns.Data.Model;
using CustomSpawns.Dialogues;
using CustomSpawns.Exception;
using CustomSpawns.ModIntegration;
using CustomSpawns.Utils;

namespace CustomSpawns.Data.Reader.Impl
{
    public class DialogueDataReader : AbstractDataReader<DialogueDataReader, IList<Dialogue>>
    {
        private readonly DialogueConsequenceInterpretor _dialogueConsequenceInterpretor;
        private readonly DialogueConditionInterpretor _dialogueConditionInterpretor;
        private readonly MessageBoxService _messageBoxService;
        private readonly List<Dialogue> _rootDialogueData = new ();
        private int _currentId = 0;

        public DialogueDataReader(SubModService subModService, MessageBoxService messageBoxService,
            DialogueConsequenceInterpretor consequenceInterpretor, DialogueConditionInterpretor conditionInterpretor)
        {
            _dialogueConsequenceInterpretor = consequenceInterpretor;
            _dialogueConditionInterpretor = conditionInterpretor;
            _messageBoxService = messageBoxService;
            LoadDialogueDataFromAllSubMods(subModService);
            AddDefaultDialogue();
        }

        public override IList<Dialogue> Data
        {
            get => _rootDialogueData.AsReadOnly();
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
                        ParseDialogueFile(path);
                    }
                    catch (ArgumentException e)
                    {
                        _messageBoxService.ShowCustomSpawnsErrorMessage(e, " Dialogue System XML loading");
                    }
                }
            }
        }

        private void AddDefaultDialogue()
        {
            DefaultDialogue defaultDialogue = new (_dialogueConditionInterpretor, _dialogueConsequenceInterpretor);
            _rootDialogueData.AddRange(defaultDialogue.DefaultDialogueData);
        }

        private void ParseDialogueFile(string filePath)
        {
            XmlDocument doc = new ();
            doc.Load(filePath);

            foreach (XmlNode node in doc.DocumentElement)
            {
                
                ParseDialogueNode(node, null);

            }
        }

        private void ParseDialogueNode(XmlNode node, Dialogue XMLParent)
        {
            if (node.NodeType == XmlNodeType.Comment)
                return;

            Dialogue dat;

            if(node.Name == "AlternativeDialogue")
            {
                //Dialogue alternative to parent.
                if(XMLParent == null)
                {
                    throw new ArgumentException(node.Name + " is not a valid Custom Spawns Dialogue Token!");
                }

                dat = InitializeDialogueNode(node, XMLParent.Parent, XMLParent); // initialize with same parameters as our XML parent.

                return;
            }

            if (node.Name != "Dialogue")
            {
                throw new ArgumentException(node.Name + " is not a valid Custom Spawns Dialogue Token!");
            }

            //regular dialogue node.

            dat = InitializeDialogueNode(node, XMLParent, null);

            //Now process children.

            foreach (XmlNode child in node)
            {
                ParseDialogueNode(child, dat);
            }
        }

        private Dialogue InitializeDialogueNode(XmlNode node, Dialogue xmlParent, Dialogue alternativeTarget)
        {
            Dialogue dat = new Dialogue();

            dat.InjectedToTaleworlds = false;

            //NODE RELATIONS

            dat.Parent = xmlParent;

            if(dat.Parent == null)
            {
                _rootDialogueData.Add(dat);
            }
            else
            {
                xmlParent.Children.Add(dat);
            }

            if(alternativeTarget == null)
            {
                dat.Children = new List<Dialogue>();
                dat.Dialogue_ID = "CS_Dialogue_" + _currentId;
                _currentId++;
            }
            else
            {
                dat.Children = alternativeTarget.Children;

                dat.Dialogue_ID = alternativeTarget.Dialogue_ID;
            }

            //NODE PROPERTIES

            if (node.Attributes["condition"] != null)
            {
                dat.Condition = _dialogueConditionInterpretor.ParseCondition(node.Attributes["condition"].Value);
            }

            if (node.Attributes["consequence"] != null)
            {
                dat.Consequence = _dialogueConsequenceInterpretor.ParseConsequence(node.Attributes["consequence"].Value);
            }

            bool isPlayerDialogue;

            if (!bool.TryParse(node.Attributes["player"]?.Value, out isPlayerDialogue))
            {
                dat.IsPlayerDialogue = false;
            }
            else
            {
                dat.IsPlayerDialogue = isPlayerDialogue;
            }

            dat.DialogueText = node.Attributes["text"]?.Value;


            return dat;
        }



        #region Condition Parsing



        #endregion

        #region Consequence Parsing



        #endregion

    }
}
