using System;
using System.IO;
using System.Xml.Serialization;
using CustomSpawns.Utils;
using TaleWorlds.Library;

namespace CustomSpawns.Config
{
    class ConfigLoader
    {
        private static ConfigLoader? _instance;
        public Config Config { get; }

        public static ConfigLoader Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new ConfigLoader();
                }

                return _instance;
            }
        }

        private ConfigLoader()
        {
            string path = "";
            path = Path.Combine(BasePath.Name, "Modules", Main.ModuleName, "ModuleData", "config.xml");
            Config = getConfig(path);
        }

        private Config getConfig(String filePath)
        {
            try
            {
                XmlSerializer serializer = new(typeof(Config));
                using (var reader = new StreamReader(filePath))
                {
                    return (Config)serializer.Deserialize(reader);
                }
            }
            catch (System.Exception e)
            {
                ErrorHandler.HandleException(e);
                Config config = new();
                config.IsDebugMode = true;
                return config;
            }
        }
    }
}
