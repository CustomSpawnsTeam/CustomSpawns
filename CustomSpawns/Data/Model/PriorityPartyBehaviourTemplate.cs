using System.Collections.Generic;
using System.Xml.Serialization;

namespace CustomSpawns.Data.Model
{
    public class PriorityPartyBehaviourTemplate
    {
        private List<PriorityPartyBehaviourConfig>? _priorityPartyBehaviours;

        [XmlElement("Id")]
        public string? Id { get; set; }

        [XmlArray("PriorityPartyBehaviours")]
        [XmlArrayItem("BesiegeClosestSettlement", Type = typeof(BesiegeClosestSettlementConfig))]
        [XmlArrayItem("RaidClosestVillage", Type = typeof(RaidClosestVillageConfig))]
        [XmlArrayItem("PatrolAroundClosestLestInterruptedAndSwitch",
            Type = typeof(PatrolAroundClosestLestInterruptedAndSwitchConfig))]
        public List<PriorityPartyBehaviourConfig>? PriorityPartyBehaviours
        {
            get => _priorityPartyBehaviours;
            set
            {
                _priorityPartyBehaviours = value;
                if (value?.Count > 0)
                {
                    PriorityPartyBehavioursSpecified = true;
                }
            }
        }

        [XmlIgnore]
        public bool PriorityPartyBehavioursSpecified { get; set; }
    }
}
