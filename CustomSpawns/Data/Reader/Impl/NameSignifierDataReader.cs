using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Xml;
using CustomSpawns.Data.Model;
using CustomSpawns.Exception;
using CustomSpawns.ModIntegration;
using CustomSpawns.Utils;

namespace CustomSpawns.Data.Reader.Impl
{
    public class NameSignifierDataReader : AbstractDataReader<NameSignifierDataReader, IDictionary<string, NameSignifier>>
    {
        private readonly MessageBoxService _messageBoxService;
        private IDictionary<string, NameSignifier> _nameSignifiers = new Dictionary<string, NameSignifier>();
        public NameSignifierDataReader(SubModService subModService, MessageBoxService messageBoxService)
        {
            _messageBoxService = messageBoxService;
            LoadNameSignifierDataFromAllSubMods(subModService);
        }

        public override IDictionary<string, NameSignifier> Data
        {
            get => new ReadOnlyDictionary<string, NameSignifier>(_nameSignifiers);
        }

        private void LoadNameSignifierDataFromAllSubMods(SubModService subModService)
        {
            foreach(var subMod in subModService.GetAllLoadedSubMods())
            {
                string path = Path.Combine(subMod.CustomSpawnsDirectoryPath, "NameSignifiers.xml");
                if (File.Exists(path))
                {
                    try
                    {
                        ConstructFromXML(path);
                    }
                    catch (ArgumentException e)
                    {
                        _messageBoxService.ShowCustomSpawnsErrorMessage(e, "An error occured while parsing " + path);
                    }
                }
            }
        }
        
        private void ConstructFromXML(string path)
        {
            XmlDocument doc = new ();
            doc.Load(path);
            foreach (XmlNode node in doc.DocumentElement)
            {
                if (node.NodeType == XmlNodeType.Comment)
                    continue;
                if (node.Attributes["id"] == null || node.Attributes["value"] == null)
                {
                    throw new ArgumentException("There must be an id and value attribute defined for each element in NameSignifiers.xml");
                }
                string id = PartyId(node);
                if (_nameSignifiers.ContainsKey(id))
                {
                    continue;
                }
                NameSignifier nameSignifier = new ();
                nameSignifier.IdToName = PartyName(node);
                nameSignifier.IdToSpeedModifier = SpeedModifier(node);
                nameSignifier.IdToFollowMainParty = IsConfiguredToFollowMainParty(node);
                nameSignifier.IdToBaseSpeedOverride = BaseSpeedOverride(node);
                _nameSignifiers.Add(id, nameSignifier);
            }
        }

        private string PartyId(XmlNode node)
        {
            if (node.Attributes["id"] == null)
            {
                throw new ArgumentException("There must be an id attribute defined for each element in NameSignifiers.xml");
            }
            return node.Attributes["id"].InnerText;
        }

        private string PartyName(XmlNode node)
        {
            if (node.Attributes["id"] == null || node.Attributes["value"] == null)
            {
                throw new ArgumentException("There must be an id and value attribute defined for each element in NameSignifiers.xml");
            }
            return node.Attributes["value"].InnerText;
        }

        private float SpeedModifier(XmlNode node)
        {
            if (node.Attributes["speed_modifier"] != null)
            {
                float result;
                if (!float.TryParse(node.Attributes["speed_modifier"].InnerText, out result))
                {
                    throw new ArgumentException("Please enter a valid float for the speed modifier!");
                }
                return result;
            }
            return 0f;
        }
        
        private bool IsConfiguredToFollowMainParty(XmlNode node)
        {
            if (node.Attributes["escort_main_party"] != null)
            {
                bool result;
                if(!bool.TryParse(node.Attributes["escort_main_party"].InnerText, out result))
                {
                    throw new ArgumentException("The value for escort_main_party must either be true or false!");
                }
                return result;
            }
            return true;
        }
        
        private float BaseSpeedOverride(XmlNode node)
        {
            if(node.Attributes["base_speed_override"] != null)
            {
                float result;
                if (!float.TryParse(node.Attributes["base_speed_override"].InnerText, out result))
                {
                    throw new ArgumentException("Please enter a valid float for the base speed override!");
                }
                return result;
            }
            return 1f;
        }
    }
}
