using System.Collections.Generic;
using System.IO;
using System.Xml;
using CustomSpawns.Utils;
using TaleWorlds.ModuleManager;

namespace CustomSpawns.ModIntegration
{
    public class SubModService
    {
        private readonly MessageBoxService _messageBoxService;
        private static List<SubMod>? _cachedSubMods;

        public SubModService(MessageBoxService messageBoxService)
        {
            _messageBoxService = messageBoxService;
        }
        
        public List<SubMod> GetAllLoadedSubMods()
        {
            if (_cachedSubMods != null)
            {
                return _cachedSubMods;
            }
            List<SubMod> subMods = new();
            foreach (string path in TaleWorlds.Engine.Utilities.GetModulesNames())
            {
                string loadedModule = ModuleHelper.GetModuleFullPath(path);
                string subModDefinitionPath = Path.Combine(loadedModule, "CustomSpawns", "CustomSpawnsSubMod.xml");
                if (!File.Exists(Path.Combine(loadedModule, "Submodule.xml")) || !File.Exists(subModDefinitionPath))
                {
                    continue;
                }

                XmlDocument doc = new();
                doc.Load(subModDefinitionPath);
                string? subModuleName = doc.DocumentElement?["SubModuleName"]?.InnerText;
                if (string.IsNullOrWhiteSpace(subModuleName))
                {
                    _messageBoxService.ShowMessage("The submodule in path " + loadedModule + " in the CustomSpawnsSubMod.xml file is not valid. " +
                                                       "Either the SubModuleName element is missing in the CustomSpawnsSubMod.xml or its value is empty");
                    continue;
                }
                SubMod mod = new(subModuleName!, Path.Combine(loadedModule, "CustomSpawns"));
                subMods.Add(mod);
            }
            _cachedSubMods = subMods;
            return subMods;
        }

        public string GetCustomSpawnsModule()
        {
            return ModuleHelper.GetModuleFullPath(Main.ModuleName);
        }
    }
}