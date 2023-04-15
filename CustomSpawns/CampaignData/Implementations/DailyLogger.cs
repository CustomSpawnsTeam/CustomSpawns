using System;
using System.IO;
using CustomSpawns.CampaignData.Config;
using CustomSpawns.Data;
using CustomSpawns.Data.Dao;
using CustomSpawns.Data.Dto;
using CustomSpawns.ModIntegration;
using CustomSpawns.Spawn;
using CustomSpawns.Utils;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace CustomSpawns.CampaignData.Implementations { 
    class DailyLogger : CampaignBehaviorBase
    {
        private PlatformFilePath _sessionLogFile;
        private int _dayCount;
        private readonly DynamicSpawnData _dynamicSpawnData;
        private readonly DevestationMetricData _devestationMetricData;
        private readonly DailyLoggerConfig _config;
        private readonly MessageBoxService _messageBoxService;
        private readonly SubModService _subModService;
        private readonly SpawnDao _spawnDao;

        public DailyLogger(DevestationMetricData devestationMetricData, DynamicSpawnData dynamicSpawnData,
            CampaignDataConfigLoader campaignDataConfigLoader, MessageBoxService messageBoxService, SubModService subModService,
            SpawnDao spawnDao)
        {
            _devestationMetricData = devestationMetricData;
            _dynamicSpawnData = dynamicSpawnData;
            _config = campaignDataConfigLoader.GetConfig<DailyLoggerConfig>();
            _messageBoxService = messageBoxService;
            _subModService = subModService;
            _spawnDao = spawnDao;
            Init();
        }

        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnAfterDailyTick);
            CampaignEvents.WarDeclared.AddNonSerializedListener(this, OnWarDeclared);
            CampaignEvents.MakePeace.AddNonSerializedListener(this, OnPeaceMade);
            CampaignEvents.ClanChangedKingdom.AddNonSerializedListener(this, ClanChangedKingdom);
            CampaignEvents.OnSettlementOwnerChangedEvent.AddNonSerializedListener(this, OnSettlementChange);
        }

        public override void SyncData(IDataStore dataStore) { }

        private void Init()
        {
            string filename = "RudimentaryLastSessionLog_" + DateTime.Now.ToString("yyyy-MM-dd_h-mm_tt") + ".txt";
            PlatformDirectoryPath folderPath = new PlatformDirectoryPath(PlatformFileType.User, Path.Combine("Data", "CustomSpawns", "Logs"));
            _sessionLogFile = new PlatformFilePath(folderPath, filename);
            FileHelper.SaveFileString(_sessionLogFile, "");
        }

        private void OnAfterDailyTick()
        {
            _dayCount = (int)Campaign.Current.CampaignStartTime.ElapsedDaysUntilNow;
        }

        public void Info(String s)
        {
            WriteString(s);
        }
        
        private void WriteString(string s)
        {
            try
            {
                var date = DateTime.Now.ToString("yyyy-MM-dd h:mm tt");
                string line = String.Format("[{0} {1}][Campaign Day {2}] {3}", DateTime.Now.ToLongTimeString(), date,
                    _dayCount, s);
                FileHelper.AppendLineToFileString(_sessionLogFile, line);
            }
            catch (System.Exception ex)
            {
                _messageBoxService.ShowCustomSpawnsErrorMessage(ex, "Could not write into the log file");
            }
        }

        private void OnWarDeclared(IFaction fac1, IFaction fac2, DeclareWarAction.DeclareWarDetail reason)
        {
            WriteString(fac1.Name + " and " + fac2.Name + " have declared war!\n");
        }
        
        private void ClanChangedKingdom(Clan c, Kingdom k1, Kingdom k2, ChangeKingdomAction.ChangeKingdomActionDetail details, Boolean b)
        {
            if (k1 != null && k2 != null)
            {
                WriteString("Clan " + c.Name + " has left " + k1.Name + " to join " + k2.Name + ". (reason: " + details + ")");   
            } else if (k1 != null && k2 == null)
            {
                WriteString("Clan " + c.Name + " left " + k1.Name + ". (reason: " + details + ")");   
            } else if (k1 == null && k2 != null)
            {
                WriteString("Clan " + c.Name + " joined " + k2.Name + ". (reason: " + details + ")");
            }
        }

        private void OnPeaceMade(IFaction fac1, IFaction fac2, MakePeaceAction.MakePeaceDetail reason)
        {
            WriteString(fac1.Name + " and " + fac2.Name + " have made peace!\n");
        }

        private void OnSettlementChange(Settlement s, bool b, Hero newOwner, Hero oldOwner, Hero h3, ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail details)
        {


            if(details == ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail.BySiege)
            {
                if (s == null || oldOwner == null || newOwner == null || oldOwner.Clan == null || newOwner.Clan == null || oldOwner.Clan.Kingdom == null || newOwner.Clan.Kingdom == null) //absolutely disgusting.
                    WriteString("There has been a siege. \n");
                else
                    WriteString(s.Name + " has been captured successfully through siege, changing hands from " + oldOwner.Clan.Kingdom.Name + " to " + newOwner.Clan.Kingdom.Name + "\n");
            }
        }

        public void ReportSpawn(MobileParty spawned, float chanceOfSpawnBeforeSpawn)
        {
            if (spawned.Party.TotalStrength < _config.MinimumSpawnLogValue || chanceOfSpawnBeforeSpawn > _config.MinimumRarityToLog)
                return;

            string msg = "New Spawn: " + spawned.StringId +
                "\nTotal Strength:" + spawned.Party.TotalStrength +
                "\nChance of Spawn: " + chanceOfSpawnBeforeSpawn;

            string? partyTemplateId = _dynamicSpawnData.GetDynamicSpawnData(spawned)?.PartyTemplateId;
            if (partyTemplateId == null)
            {
                return;
            }
            SpawnDto? spawn = _spawnDao.FindByPartyTemplateId(partyTemplateId);
            if (spawn == null)
            {
                return;
            }
            
            if (spawn.DynamicSpawnChanceEffect > 0)
            {
                msg += "\nDynamic Spawn Chance Effect: " + spawn.DynamicSpawnChanceEffect;
                msg += "\nDynamic Spawn Chance Base Value During Spawn: " + DataUtils.GetCurrentDynamicSpawnCoeff(spawn.DynamicSpawnChancePeriod);
            }

            var spawnSettlement = _dynamicSpawnData.GetDynamicSpawnData(spawned).LatestClosestSettlement;

            if (spawnSettlement.IsVillage)
            {
                msg += "\nDevestation at spawn settlement: " +
                       _devestationMetricData.GetDevestation(spawnSettlement);
            }


            msg += "\n";

            WriteString(msg);
        }
    }
}
