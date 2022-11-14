using System.Collections.Generic;
using System.Xml.Serialization;

namespace CustomSpawns.Data.Model.Dialogue
{
    [XmlRoot("root")]
    public class Dialogues
    {
        [XmlElement(ElementName = "Dialogue")]
        public List<Dialogue> AllDialogues
        {
            get;
            set;
        } = new();
    }
}