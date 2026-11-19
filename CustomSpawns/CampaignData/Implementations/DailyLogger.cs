using System;
using System.Globalization;
using System.IO;
using CustomSpawns.Data.Dto;
using CustomSpawns.Utils;
using NLog;
using NLog.Config;
using NLog.Targets;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace CustomSpawns.CampaignData.Implementations
{
    public class DailyLogger : CampaignBehaviorBase
    {
        static DailyLogger()
        {
            var assemblyFolder = Path.GetDirectoryName(typeof(DailyLogger).Assembly.Location);
            var modRootFolder = Path.GetFullPath(Path.Combine(assemblyFolder, "..", ".."));

            // NLog.config sets the levels and targets; it finds the mod's folder through this item.
            GlobalDiagnosticsContext.Set("modRoot", modRootFolder);
            var configPath = Path.Combine(modRootFolder, "ModuleData", "NLog.config");
            var config = File.Exists(configPath)
                ? new XmlLoggingConfiguration(configPath)
                : DefaultConfiguration(modRootFolder);
#if DEBUG
            // A debug build logs at Debug at least, whatever NLog.config says.
            foreach (var rule in config.LoggingRules)
            {
                if (!rule.IsLoggingEnabledForLevel(LogLevel.Debug))
                {
                    rule.EnableLoggingForLevels(LogLevel.Debug, LogLevel.Fatal);
                }
            }
#endif
            LogManager.Configuration = config;
        }

        private static LoggingConfiguration DefaultConfiguration(string modRootFolder)
        {
            var config = new LoggingConfiguration();
            var logFilePath = Path.Combine(modRootFolder, "Logs", "${shortdate}.log");

            var logfile = new FileTarget("logfile")
            {
                FileName = logFilePath,
                Layout = "${longdate}|${level:uppercase=true}|${logger}|${message}${onexception:inner=${newline}${exception:format=tostring}}",
                CreateDirs = true,
                AutoFlush = true
            };

            var logconsole = new ConsoleTarget("logconsole")
            {
                Layout = "${longdate}|${level:uppercase=true}|${logger}|${message}${onexception:inner=${newline}${exception:format=tostring}}"
            };

            config.AddRule(LogLevel.Info, LogLevel.Fatal, logfile);
            config.AddRule(LogLevel.Info, LogLevel.Fatal, logconsole);

            return config;
        }

        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private int _dayCount;
        private readonly DevestationMetricData _devestationMetricData;
        private readonly MessageBoxService _messageBoxService;

        public DailyLogger(DevestationMetricData devestationMetricData, MessageBoxService messageBoxService)
        {
            _devestationMetricData = devestationMetricData;
            _messageBoxService = messageBoxService;
        }

        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnAfterDailyTick);
            CampaignEvents.WarDeclared.AddNonSerializedListener(this, OnWarDeclared);
            CampaignEvents.MakePeace.AddNonSerializedListener(this, OnPeaceMade);
            CampaignEvents.OnClanChangedKingdomEvent.AddNonSerializedListener(this, ClanChangedKingdom);
            CampaignEvents.OnSettlementOwnerChangedEvent.AddNonSerializedListener(this, OnSettlementChange);
        }

        public override void SyncData(IDataStore dataStore) { }

        private void OnAfterDailyTick()
        {
            _dayCount = (int)Campaign.Current.Models.CampaignTimeModel.CampaignStartTime.ElapsedDaysUntilNow;
        }

        public void Info(String s)
        {
            WriteString(s);
        }

        public void Trace(string s)
        {
            WriteString(s, LogLevel.Trace);
        }

        private void WriteString(string s)
        {
            WriteString(s, LogLevel.Info);
        }

        private void WriteString(string s, LogLevel level)
        {
            try
            {
                var line = string.Format("Campaign Day {0}| {1}", _dayCount, s);
                Logger.Log(level, line);
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

        public void ReportSpawn(MobileParty spawnedParty, SpawnDto spawn, Settlement spawnSettlement, Clan spawnClan,
            float calculatedChance, float flatMultiplier, float? chanceRoll, int rollNumber, int numberOnMap)
        {
            float effectiveChance = calculatedChance * flatMultiplier;
            string chanceRollText = chanceRoll.HasValue ? FormatPercentage(chanceRoll.Value) : "forced";
            string msg = "Party spawned" +
                " | party_id=" + spawnedParty.StringId +
                " | template_id=" + spawn.PartyTemplate.StringId +
                " | spawn_name=" + spawn.Name +
                " | clan_id=" + spawnClan.StringId +
                " | clan_name=" + spawnClan.Name +
                " | settlement_id=" + spawnSettlement.StringId +
                " | settlement_name=" + spawnSettlement.Name +
                " | roll=" + rollNumber + "/" + spawn.RepeatSpawnRolls +
                " | random_roll=" + chanceRollText +
                " | configured_chance=" + FormatPercentage(spawn.ChanceOfSpawn) +
                " | calculated_chance=" + FormatPercentage(calculatedChance) +
                " | flat_multiplier=" + FormatNumber(flatMultiplier) +
                " | effective_chance=" + FormatPercentage(effectiveChance) +
                " | on_map=" + numberOnMap + "/" + spawn.MaximumOnMap +
                " | strength=" + FormatNumber(spawnedParty.Party.CalculateCurrentStrength()) +
                " | devastation=" + FormatNumber(_devestationMetricData.GetDevestation(spawnSettlement));

            WriteString(msg);
        }

        private static string FormatPercentage(float probability)
        {
            return FormatNumber(probability * 100) + "%";
        }

        private static string FormatNumber(float value)
        {
            return value.ToString("0.0000", CultureInfo.InvariantCulture);
        }
    }
}
