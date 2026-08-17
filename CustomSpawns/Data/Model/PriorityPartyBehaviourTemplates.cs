using System.Collections.Generic;
using System.Xml.Serialization;

namespace CustomSpawns.Data.Model
{
    [XmlRoot("PriorityPartyBehaviourTemplates")]
    public class PriorityPartyBehaviourTemplates
    {
        [XmlElement("PriorityPartyBehaviourTemplate")]
        public List<PriorityPartyBehaviourTemplate>? AllTemplates { get; set; }
    }
}
