using NLog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors.AiBehaviors;

namespace CustomSpawns.Invasions.Adapters
{
    /**
     * The newest listener of a campaign event is called first, so the AI hourly tick listeners of this mod would
     * reweigh a lord's sieges before vanilla has weighed any. Vanilla's military AI is registered again once every
     * behaviour has registered, so it weighs first and ours reweigh what it weighed.
     */
    public sealed class VanillaMilitaryAiFirst
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        public void Reregister()
        {
            AiMilitaryBehavior? aiMilitary = Campaign.Current.GetCampaignBehavior<AiMilitaryBehavior>();

            if (aiMilitary == null)
            {
                Logger.Warn("Invasion AI: vanilla military AI not found; invasion sieges are weighed before it");
                return;
            }

            CampaignEventDispatcher.Instance.RemoveListeners(aiMilitary);
            aiMilitary.RegisterEvents();
            Logger.Info("Invasion AI: vanilla military AI now weighs sieges before the invasion reweighs them");
        }
    }
}
