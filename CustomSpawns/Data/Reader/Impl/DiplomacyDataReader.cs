using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using CustomSpawns.Exception;
using CustomSpawns.ModIntegration;
using CustomSpawns.Utils;
using MonoMod.Utils;

namespace CustomSpawns.Data.Reader.Impl
{
    public class DiplomacyDataReader : AbstractDataReader<DiplomacyDataReader, Dictionary<string,Model.Diplomacy>>
    {
        private readonly MessageBoxService _messageBoxService;
        private readonly Dictionary<string, Model.Diplomacy> _data;
        
        public DiplomacyDataReader(SubModService subModService, MessageBoxService messageBoxService)
        {
            _messageBoxService = messageBoxService;
            _data = LoadDiplomacyDataFromAllSubMods(subModService);
        }

        public override Dictionary<string, Model.Diplomacy> Data
        {
            get => _data;
        }

        private Dictionary<string, Model.Diplomacy> LoadDiplomacyDataFromAllSubMods(SubModService subModService)
        {
            var diplomacyData = new Dictionary<string,Model.Diplomacy>();
            foreach (var subMod in subModService.GetAllLoadedSubMods())
            {
                string path = Path.Combine(subMod.CustomSpawnsDirectoryPath, "Diplomacy.xml");
                if (File.Exists(path))
                {
                    try
                    {
                        diplomacyData.AddRange(ConstructListFromXML(path));       
                    }
                    catch (ArgumentException e)
                    {
                        _messageBoxService.ShowCustomSpawnsErrorMessage(e, "Diplomacy Data Parsing of " + path);
                    }
                }
            }
            return diplomacyData;
        }

        private Dictionary<string,Model.Diplomacy> ConstructListFromXML(string path)
        {
            Dictionary<string, Model.Diplomacy> data = new();
            XmlDocument doc = new();
            doc.Load(path);

            foreach (XmlNode node in doc.DocumentElement)
            {
                if (node.NodeType == XmlNodeType.Comment)
                    continue;
                Model.Diplomacy diplomacy = new();
                if(node.Attributes["target"] == null || node.Attributes["target"].InnerText == "")
                {
                    throw new ArgumentException("Each diplomacy data instance must have a target faction!");
                }
                diplomacy.clanString = node.Attributes["target"].InnerText;
                if (node["ForceWarPeaceBehaviour"] != null)
                {
                    //handle forced war peace data.
                    diplomacy.ForcedWarPeaceDataInstance = new Model.Diplomacy.ForcedWarPeaceData();
                    XmlElement forceNode = node["ForceWarPeaceBehaviour"];
                    HandleForcedWarPeaceBehaviourData(forceNode, diplomacy);
                }
                if(node["ForceNoKingdom"] != null)
                {
                    //handle forcing of no parent kingdoms.
                    bool result;
                    if(!bool.TryParse(node["ForceNoKingdom"].InnerText, out result))
                    {
                        throw new ArgumentException("ForceNoKingdom must be a boolean value!");
                    }
                    diplomacy.ForceNoKingdom = result;
                }

                data.Add(diplomacy.clanString, diplomacy);
            }

            return data;
        }

        private void HandleForcedWarPeaceBehaviourData(XmlElement forceNode, Model.Diplomacy diplomacy)
        {
            foreach (XmlNode forceNodeChild in forceNode)
            {
                if (forceNodeChild.NodeType == XmlNodeType.Comment)
                    continue;
                if (forceNodeChild.Name == "ForcedWarSpecial")
                {
                    //handle forced war special.
                    if (forceNodeChild.Attributes["flag"] == null)
                    {
                        throw new ArgumentException("Each forced war special data must have a flag.");
                    }
                    List<string> exceptionClans = new List<string>();
                    List<string> exceptionKingdoms = new List<string>();
                    string flag = forceNodeChild.Attributes["flag"].InnerText;
                    switch (flag)
                    {
                        case "all": //handle case where All clans except maybe some are designated as enemies.
                            int j = 0;
                            string st = "but";
                            while (true)
                            {
                                string s1 = st + "_" + j.ToString();
                                if (forceNodeChild.Attributes[s1] == null || forceNodeChild.Attributes[s1].InnerText == "")
                                {
                                    break;
                                }
                                else
                                {
                                    exceptionClans.Add(forceNodeChild.Attributes[s1].InnerText);
                                }
                                j++;
                            }
                            j = 0;
                            st = "but_kingdom";
                            while (true)
                            {
                                string s1 = st + "_" + j.ToString();
                                if (forceNodeChild.Attributes[s1] == null || forceNodeChild.Attributes[s1].InnerText == "")
                                {
                                    break;
                                }
                                else
                                {
                                    exceptionKingdoms.Add(forceNodeChild.Attributes[s1].InnerText);
                                }
                                j++;
                            }
                            break;
                        default:
                            throw new ArgumentException("Invalid forced war special data flag detected");
                    }

                    diplomacy.ForcedWarPeaceDataInstance.AtPeaceWithClans = exceptionClans;
                    diplomacy.ForcedWarPeaceDataInstance.ExceptionKingdoms = exceptionKingdoms;
                }
            }
        }
    }
}