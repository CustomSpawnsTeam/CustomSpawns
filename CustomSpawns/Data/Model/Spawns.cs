using System.Collections.Generic;
using System.Xml.Serialization;

namespace CustomSpawns.Data.Model
{
        [XmlRoot("root")]
        public class Spawns
        {
            [XmlArray("PriorityPartyBehaviourTemplates")]
            [XmlArrayItem("PriorityPartyBehaviourTemplate")]
            public List<PriorityPartyBehaviourTemplate>? PriorityPartyBehaviourTemplates { get; set; }

            [XmlElement("RegularBanditDailySpawnData")]
            public List<Spawn> AllSpawns { get; set; }
        }
}
