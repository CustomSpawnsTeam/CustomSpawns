using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Xml;
using System.Xml.Serialization;

namespace CustomSpawns.Data.Model
{
    [XmlRoot(ElementName="RegularBanditDailySpawnData")]
    public class Spawn
    {
        private XmlAttribute[]? _nonProcessedAttributes;
        private List<string>? _overridenSettlementSpawns;
        private List<string>? _overridenClanSpawns;
        private List<string>? _overridenKingdomSpawns;
        private List<string>? _overridenCultureSpawns;
        private List<string>? _supportingParties;

        [DefaultValue(1), XmlElement(ElementName = "MaximumOnMap")]
        public int MaximumOnMap { get; set; } = 1;

        [DefaultValue(1f), XmlElement(ElementName = "ChanceOfSpawn")]
        public float ChanceOfSpawn { get; set; } = 1f;

        [DefaultValue(0f), XmlElement(ElementName="DevestationLinearMultiplier")]
        public float DevestationLinearMultiplier { get; set; }

        [DefaultValue(0f), XmlElement(ElementName="MinimumDevestationToSpawn")]
        public float MinimumDevestationToSpawn { get; set; }

        [DefaultValue(0f), XmlElement(ElementName="ChanceInverseConstant")]
        public float ChanceInverseConstant { get; set; }

        [DefaultValue("Unnamed"), XmlElement(ElementName="Name")]
        public string Name { get; set; } = "Unnamed";

        [DefaultValue(1), XmlElement(ElementName = "RepeatSpawnRolls")]
        public int RepeatSpawnRolls { get; set; } = 1;

        [XmlElement(ElementName="SpawnMessage")]
        public string? SpawnMessage { get; set; }

        [XmlElement(ElementName="SpawnMessageColor")]
        public string? SpawnMessageColor { get; set; }

        [XmlElement(ElementName="DeathMessage")]
        public string? DeathMessage { get; set; }

        [XmlElement(ElementName = "DeathMessageColor")]
        public string? DeathMessageColor { get; set; }

        [DefaultValue(0f), XmlElement(ElementName="DynamicSpawnChancePeriod")]
        public float DynamicSpawnChancePeriod { get; set; }

        [DefaultValue(0f), XmlElement(ElementName = "DynamicSpawnChanceEffect")]
        public float DynamicSpawnChanceEffect { get; set; }

        [DefaultValue(false), XmlElement(ElementName="GetClanFromSettlement")]
        public bool InheritClanFromSettlement { get; set; }

        [XmlElement(ElementName="PartyType")]
        public string? PartyType { get; set; }
        
        [DefaultValue(false), XmlElement(ElementName="PatrolAroundSpawn")]
        public bool IsConfiguredToPatrolAroundSpawn { get; set; }

        [DefaultValue(0f), XmlElement(ElementName = "MinimumNumberOfDaysUntilSpawn")]
        public int MinimumNumberOfDaysUntilSpawn { get; set; }

        [DefaultValue(true), XmlElement(ElementName = "AttackClosestIfIdleForADay")]
        public bool AttackClosestIfIdleForADay { get; set; } = true;

        [DefaultValue(""), XmlElement(ElementName = "TrySpawnAt")]
        public string TrySpawnAt { get; set; } = "";

        [DefaultValue(""), XmlElement(ElementName="SpawnSound")]
        public string SpawnSound { get; set; } = "";
        
        [DefaultValue(""), XmlElement(ElementName="SpawnMessageBoxTitle")]
        public string SpawnMessageBoxTitle { get; set; } = "";

        [DefaultValue(""), XmlElement(ElementName="SpawnMessageBoxText")]
        public string SpawnMessageBoxText { get; set; } = "";

        [DefaultValue("Ok"), XmlElement(ElementName="SpawnMessageBoxButton")]
        public string SpawnMessageBoxButton { get; set; } = "Ok";

        [DefaultValue(false), XmlElement(ElementName="SpawnMessageBoxPause")]
        public bool SpawnMessageBoxPause { get; set; }

        [XmlElement(ElementName="PatrolAroundClosestLestInterruptedAndSwitch")]
        public PatrolAroundConfig? PatrolAroundConfig { get; set; }

        [XmlAttribute(AttributeName="party_template")]
        public string PartyTemplate { get; set; }

        [XmlAttribute(AttributeName="party_template_prisoners")]
        public string? PrisonerPartyTemplate { get; set; }

        [XmlAttribute(AttributeName="spawn_clan")]
        public string? Faction { get; set; }
        
        [XmlAttribute(AttributeName="bandit_clan")]
        public string? BanditFaction { get; set; }

        // must be public so that the deserialiser can set the value
        [XmlAnyAttribute]
        public XmlAttribute[] XAttributes {
            set
            {
                if (_nonProcessedAttributes == null)
                {
                    _nonProcessedAttributes = value;
                }
            }
            get => Array.Empty<XmlAttribute>();
        }
        
        [XmlIgnore]
        public List<string> OverridenCultureSpawns
        {
            get
            {
                if (_overridenCultureSpawns == null)
                {
                    _overridenCultureSpawns = FindValueWithAttributeStartingWith("overriden_spawn_culture_"); 
                }
                return _overridenCultureSpawns;
            }
        }
        
        [XmlIgnore]
        public List<string> OverridenClanSpawns
        {
            get
            {
                if (_overridenClanSpawns == null)
                {
                    _overridenClanSpawns = FindValueWithAttributeStartingWith("overriden_spawn_clan_"); 
                }
                return _overridenClanSpawns;
            }
        }

        [XmlIgnore]
        public List<string> OverridenKingdomSpawns
        {
            get
            {
                if (_overridenKingdomSpawns == null)
                {
                    _overridenKingdomSpawns = FindValueWithAttributeStartingWith("overriden_spawn_kingdom_"); 
                }
                return _overridenKingdomSpawns;
            }
        }

        [XmlIgnore]
        public List<string> OverridenSettlementSpawns
        {
            get
            {
                if (_overridenSettlementSpawns == null)
                {
                    _overridenSettlementSpawns = FindValueWithAttributeStartingWith("overriden_spawn_settlement_"); 
                }
                return _overridenSettlementSpawns;
            }
        }

        [XmlIgnore]
        public List<string> SupportingPartyTemplates
        {
            get
            {
                if (_supportingParties == null)
                {
                    _supportingParties = FindValueWithAttributeStartingWith("spawn_along_with"); 
                }
                return _supportingParties;
            }
        }

        private List<string> FindValueWithAttributeStartingWith(string name)
        {
            return _nonProcessedAttributes?
                .Where(attribute => attribute.Name.StartsWith(name))
                .Select(attribute => attribute.Value)
                .ToList() ?? new();
        }
    }
}