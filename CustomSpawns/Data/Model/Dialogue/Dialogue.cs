using System.Collections.Generic;
using System.Xml.Serialization;
using CustomSpawns.Data.Model.Reward;

namespace CustomSpawns.Data.Model.Dialogue
{
    // API V1
    public class Dialogue
    {
        public DialogueType Type { get; set; } = DialogueType.MapEncounter;

        [XmlAttribute(AttributeName="consequence")]
        public string? Consequence { get; set; }

        [XmlAttribute(AttributeName = "player")]
        public bool IsPlayerDialogue { get; set; }

        [XmlElement(ElementName="AlternativeDialogue")]
        public List<Dialogue>? Parents { get; set; }

        [XmlAttribute(AttributeName = "text")]
        public string Text { get; set; } = "Insert a dialogue text to override this";

        [XmlAttribute(AttributeName="condition")]
        public string? Condition { get; set; }

        [XmlElement(ElementName="Dialogue")]
        public List<Dialogue>? Options { get; set; }
    }
}