using System;
using System.Collections.Generic;
using CustomSpawns.CampaignData.Implementations;
using CustomSpawns.Data.Reader;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using static TaleWorlds.CampaignSystem.Actions.ChangeKingdomAction.ChangeKingdomActionDetail;

namespace CustomSpawns.Diplomacy
{
    class ForceNoKingdomBehaviour : CampaignBehaviorBase
    {
        private readonly DailyLogger _dailyLogger;
        private readonly IDataReader<Dictionary<string,Data.Model.Diplomacy>> _dataReader;

        public ForceNoKingdomBehaviour(IDataReader<Dictionary<string,Data.Model.Diplomacy>> dataReader, DailyLogger dailyLogger)
        {
            _dataReader = dataReader;
            _dailyLogger = dailyLogger;
        }

        public override void RegisterEvents()
        {
            CampaignEvents.ClanChangedKingdom.AddNonSerializedListener(this, MakeSureNoClanJoinAKingdom);
        }

        public override void SyncData(IDataStore dataStore)
        {

        }

        private void MakeSureNoClanJoinAKingdom(Clan? clan, Kingdom? oldKingdom, Kingdom? newKingdom, ChangeKingdomAction.ChangeKingdomActionDetail details, Boolean b)
        {
            if (clan == null || details != JoinAsMercenary && details != JoinKingdom && details != CreateKingdom)
            {
                return;
            }
            
            if (_dataReader.Data.ContainsKey(clan.StringId))
            {
                if(_dataReader.Data[clan.StringId].ForceNoKingdom && clan.Kingdom != null)
                {
                    ChangeKingdomAction.ApplyByLeaveKingdom(clan);
                    _dailyLogger.Info(clan.StringId + " has forcefully been removed from parent kingdom " + oldKingdom?.Name ?? "");
                }
            }
        }
    }
}
