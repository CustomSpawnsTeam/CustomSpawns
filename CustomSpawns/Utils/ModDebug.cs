using CustomSpawns.CampaignData.Config;
using CustomSpawns.Config;
using TaleWorlds.Library;


namespace CustomSpawns.Utils
{
    public class ModDebug
    {
        private readonly ConfigLoader _configLoader;
        
        public ModDebug(ConfigLoader configLoader)
        {
            _configLoader = configLoader;
        }
        
        public void ShowMessage(string message, DebugMessageType messageType)
        {
            if (!_configLoader.Config.IsDebugMode)
                return;
            if (messageType == DebugMessageType.AI && !_configLoader.Config.ShowAIDebug)
                return;
            if (messageType == DebugMessageType.DeathTrack && !_configLoader.Config.ShowDeathTrackDebug)
                return;
            InformationManager.DisplayMessage(new InformationMessage(message, Color.ConvertStringToColor("#FF8F00FF")));
        }

        public void ShowMessage(string message, ICampaignDataConfig config)
        {
            if (!config.ShowConfigDebug)
            {
                return;
            }

            InformationManager.DisplayMessage(new InformationMessage(message, Color.ConvertStringToColor("#FF8F00FF")));
        }

    }

    public enum DebugMessageType { Spawn, AI, Prisoner, Diplomacy, DeathTrack, Dialogue, Development, Reward }
}
