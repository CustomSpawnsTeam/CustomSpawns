using TaleWorlds.CampaignSystem;

namespace CustomSpawns.AI.Behaviours
{
    public interface ISyncsCampaignData
    {
        void SyncData(IDataStore dataStore);
    }
}
