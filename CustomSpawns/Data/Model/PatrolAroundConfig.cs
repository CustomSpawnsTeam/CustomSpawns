using System.ComponentModel;
using System.Xml.Serialization;

namespace CustomSpawns.Data.Model
{
    [XmlRoot(ElementName = "PatrolAroundClosestLestInterruptedAndSwitch")]
    public class PatrolAroundConfig
    {
        [DefaultValue(0f), XmlAttribute(AttributeName = "min_stable_days")]
        public float MinStableDays { get; set; }

        [DefaultValue(10f), XmlAttribute(AttributeName = "max_stable_days")]
        public float MaxStableDays { get; set; }

        [XmlAttribute(AttributeName = "try_patrol_around")]
        public string? PatrolLocationType { get; set; }

        [XmlText]
        public bool IsEnabled { get; set; }
    }
}
