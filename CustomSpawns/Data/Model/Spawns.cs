using System.Collections.Generic;
using System.Xml.Serialization;

namespace CustomSpawns.Data.Model
{
        [XmlRoot("root")]
        public class Spawns
        {
            [XmlElement("RegularBanditDailySpawnData")]
            public List<Spawn> AllSpawns { get; set; }
        }
}