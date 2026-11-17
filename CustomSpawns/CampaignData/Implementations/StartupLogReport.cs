using System.Linq;
using CustomSpawns.Data.Dao;
using CustomSpawns.ModIntegration;
using NLog;
using TaleWorlds.Library;
using TaleWorlds.ModuleManager;

namespace CustomSpawns.CampaignData.Implementations
{
    // The first lines of a campaign's log: what a bug report needs to reproduce the setup it ran on.
    public class StartupLogReport
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly SubModService _subModService;
        private readonly ISpawnDao _spawnDao;

        public StartupLogReport(SubModService subModService, ISpawnDao spawnDao)
        {
            _subModService = subModService;
            _spawnDao = spawnDao;
        }

        public void Write()
        {
            Logger.Info("Custom Spawns started: version=" + FindModuleVersion(Main.ModuleName) +
                        " game=" + ApplicationVersion.FromParametersFile() +
                        " log_level=" + FindLogLevel());
            Logger.Info("Loaded modules: " + string.Join(", ", TaleWorlds.Engine.Utilities.GetModulesNames()
                .Select(moduleId => moduleId + "@" + FindModuleVersion(moduleId))));
            Logger.Info("Custom Spawns submods: " + string.Join(", ", _subModService.GetAllLoadedSubMods()
                .Select(subMod => subMod.SubModuleName)));
            Logger.Info("Spawns loaded: count=" + _spawnDao.FindAll().Count + " invasions=" + string.Join(",",
                _spawnDao.FindAll().Where(spawn => spawn.Invasion != null)
                    .Select(spawn => spawn.PartyTemplate.StringId)));
        }

        private static string FindModuleVersion(string moduleId)
        {
            return ModuleHelper.GetModuleInfo(moduleId)?.Version.ToString() ?? "unknown";
        }

        private static string FindLogLevel()
        {
            return Logger.IsTraceEnabled ? "Trace" : Logger.IsDebugEnabled ? "Debug" : "Info";
        }
    }
}
