using System;
using System.IO;
using System.Xml.Serialization;
using CustomSpawns.ModIntegration;
using CustomSpawns.Utils;

namespace CustomSpawns.Config
{
    public class ConfigLoader
    {
        private readonly MessageBoxService _messageBoxService;
        
        public Config Config { get; }

        public ConfigLoader(SubModService subModService, MessageBoxService messageBoxService)
        {
            _messageBoxService = messageBoxService;
            Config = ReadConfig(subModService.GetCustomSpawnsModule());
        }

        private Config ReadConfig(String? filePath)
        {
            if (filePath == null)
            {
                return new();
            }
            try
            {
                XmlSerializer serializer = new(typeof(Config));
                using (var reader = new StreamReader(Path.Combine(filePath, "ModuleData", "config.xml")))
                {
                    return (Config)serializer.Deserialize(reader);
                }
            }
            catch (System.Exception e)
            {
                _messageBoxService.ShowCustomSpawnsErrorMessage(e);
                Config config = new();
                config.IsDebugMode = true;
                return config;
            }
        }
    }
}
