using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using System.Xml.Serialization;
using CustomSpawns.ModIntegration;
using CustomSpawns.Utils;
using TaleWorlds.Localization;

namespace CustomSpawns.CampaignData.Config
{
    public class CampaignDataConfigLoader
    {
        private readonly MessageBoxService _messageBoxService;
        private readonly Dictionary<Type, object> _typeToConfig = new();

        public CampaignDataConfigLoader(SubModService subModService, MessageBoxService messageBoxService)
        {
            _messageBoxService = messageBoxService;
            string path = Path.Combine(subModService.GetCustomSpawnsModule(), "ModuleData", "custom_spawns_campaign_data_config.xml");
            ConstructConfigs(path);
        }

        private void ConstructConfigs(string xmlPath)
        {
            var type = typeof(ICampaignDataConfig);
            var types = Assembly.GetExecutingAssembly().GetTypes()
                    .Where(p => type.IsAssignableFrom(p) && p != type);

            try
            {

                XDocument xDocument = XDocument.Load(xmlPath);

                foreach(var t in types) //doing it this way to detec errors/missing for specific types.
                {

                    bool processed = false;

                    foreach(var ele in xDocument.Root.Elements())
                    {
                        if(ele.Name.LocalName.ToString() == t.Name)
                        {
                            var config = DeserializeNode(ele, t);
                            _typeToConfig.Add(t, config);
                            processed = true;
                        }
                    }

                    if (!processed)
                    {
                        _messageBoxService.ShowMessage(new TextObject("{=SpawnAPIWarn003}Could not find Campaign Data config file for type {NAME}").SetTextVariable("NANE", t.Name).ToString());
                    }
                }
            }
            catch (System.Exception e)
            {
                _messageBoxService.ShowCustomSpawnsErrorMessage(e, "CAMPAIGN DATA XML READING");
            }

        }

        private static object DeserializeNode(XElement data, Type t) 
        {
            if (data == null)
                return null;

            var ser = new XmlSerializer(t);
            return ser.Deserialize(data.CreateReader());
        }

        public T GetConfig<T>() where T: class, ICampaignDataConfig, new()
        {
            if (_typeToConfig.ContainsKey(typeof(T)))
            {
                return _typeToConfig[typeof(T)] as T;
            }

            return null;
        }
    }

}

