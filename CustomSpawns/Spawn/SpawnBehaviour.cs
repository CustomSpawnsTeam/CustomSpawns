using System;
using System.Collections.Generic;
using System.Linq;
using CustomSpawns.CampaignData.Implementations;
using CustomSpawns.Config;
using CustomSpawns.Data;
using CustomSpawns.Data.Dao;
using CustomSpawns.Data.Dto;
using CustomSpawns.Economics;
using CustomSpawns.UtilityBehaviours;
using CustomSpawns.Utils;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Map;
using TaleWorlds.Localization;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace CustomSpawns.Spawn
{
    class SpawnBehaviour : CampaignBehaviorBase
    {
        private readonly Dictionary<string, int> _numberOfSpawns;
        private readonly Spawner _spawner;
        private readonly SpawnDao _spawnDao;
        private readonly DynamicSpawnData _dynamicSpawnData;
        private readonly DevestationMetricData _devestationMetricData;
        private readonly ConfigLoader _configLoader;
        private readonly MessageBoxService _messageBoxService;
        private readonly DailyLogger _dailyLogger;
        private readonly ModDebug _modDebug;
        
        #region Data Management

        private int _lastRedundantDataUpdate = 0;

        public SpawnBehaviour(Spawner spawner, SpawnDao spawnDao, DynamicSpawnData dynamicSpawnData,
            SaveInitialiser saveInitialiser, DevestationMetricData devestationMetricData, ConfigLoader configLoader,
            MessageBoxService messageBoxService, DailyLogger dailyLogger, ModDebug modDebug)
        {
            _spawner = spawner;
            _spawnDao = spawnDao;
            _dynamicSpawnData = dynamicSpawnData;
            _devestationMetricData = devestationMetricData;
            _messageBoxService = messageBoxService;
            _modDebug = modDebug;
            _lastRedundantDataUpdate = 0;
            _numberOfSpawns = new();
            _configLoader = configLoader;
            _dailyLogger = dailyLogger;
            saveInitialiser.RunCallbackOnFirstCampaignTick(OnSaveStart);
        }

        private void HourlyCheckData()
        {
            if (_lastRedundantDataUpdate < _configLoader.Config.UpdatePartyRedundantDataPerHour + 1) // + 1 to give leeway and make sure every party gets updated. 
            {
                _lastRedundantDataUpdate++;
            }
            else
            {
                _lastRedundantDataUpdate = 0;
            }

            //Now for data checking?
        }

        #endregion


        #region MB API-Registered Behaviours

        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, DailyBehaviour);
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, HourlyBehaviour);
            CampaignEvents.DailyTickPartyEvent.AddNonSerializedListener(this, ReplenishPartyFood);
            CampaignEvents.MobilePartyCreated.AddNonSerializedListener(this, OnPartyCreated);
            CampaignEvents.OnPartyRemovedEvent.AddNonSerializedListener(this, OnPartyRemoved);
        }

        public override void SyncData(IDataStore dataStore)
        {

        }

        private bool _spawnedToday = false;

        private void OnSaveStart()
        {
            RestoreNumberOfSpawns();
            //restore lost AI behaviours!
            try
            {
                foreach (MobileParty mb in MobileParty.All)
                {
                    string id = CampaignUtils.IsolateMobilePartyStringID(mb);
                    var spawn = _spawnDao.FindByPartyTemplateId(id);
                    if(!string.IsNullOrEmpty(id) && spawn != null)
                    {
                        HandleAIChecks(mb, spawn, mb.HomeSettlement);
                    }
                }
            } catch(System.Exception e)
            {
                _messageBoxService.ShowCustomSpawnsErrorMessage(e, " reconstruction of save custom spawns mobile party data");
            }
        }

        private void HourlyBehaviour()
        {
            HourlyCheckData();
            if (!_spawnedToday && Campaign.Current.IsNight)
            {
                RegularSpawn();
                _spawnedToday = true;
            }
        }

        //deal with our parties being removed! Also this is more efficient ;)
        private void OnPartyRemoved(PartyBase? partyBase)
        {
            MobileParty? mb = partyBase?.MobileParty;
            if (mb == null)
                return;

            CsPartyData? partyData = _dynamicSpawnData.GetDynamicSpawnData(mb);
            if (partyData != null && _numberOfSpawns.ContainsKey(partyData.PartyTemplateId))
            {
                _numberOfSpawns[partyData.PartyTemplateId]--;
                //this is a custom spawns party!!
                OnPartyDeath(mb, partyData);
                _modDebug.ShowMessage(mb.StringId + " has died at " + partyData.LatestClosestSettlement + ", reducing the total number to: " + _numberOfSpawns[partyData.PartyTemplateId], DebugMessageType.DeathTrack);
            }
        }

        private void ReplenishPartyFood(MobileParty mb)
        {
            if (_dynamicSpawnData.GetDynamicSpawnData(mb) == null) //check if it is a custom spawns party
                return;
            PartyEconomicUtils.PartyReplenishFood(mb);
        }

        private void DailyBehaviour()
        {
            _spawnedToday = false;
        }

        #endregion

        // TODO check why this is not working
        private void RestoreNumberOfSpawns()
        {
            IList<SpawnDto> spawns = _spawnDao.FindAll();
            foreach (SpawnDto spawn in spawns)
            {
                _numberOfSpawns[spawn.PartyTemplate.StringId] = 0;
            }
            foreach (MobileParty mb in MobileParty.All)
            {
                if (mb == null)
                    continue;
                SpawnDto? spawn = _spawnDao.FindByPartyTemplateId(CampaignUtils.IsolateMobilePartyStringID(mb));
                if (spawn == null)
                {
                    continue;
                }
                _numberOfSpawns[spawn.PartyTemplate.StringId]++;
            }
        }
        
        private float ComputeChanceSpawn(SpawnDto spawn)
        {
            float devestationLerp = _devestationMetricData.GetDevestationLerp();
                
            float baseChance = 
                spawn.ChanceOfSpawn + spawn.ChanceInverseConstant * (spawn.MaximumOnMap - _numberOfSpawns[spawn.PartyTemplate.StringId]) / spawn.MaximumOnMap + spawn.DevestationLinearMultiplier * devestationLerp;

            float dynamicCoeff = 1;

            if(spawn.DynamicSpawnChanceEffect > 0)
            {
                dynamicCoeff = DataUtils.GetCurrentDynamicSpawnCoeff(spawn.DynamicSpawnChancePeriod);
            }

            return (1 - spawn.DynamicSpawnChanceEffect) * baseChance + spawn.DynamicSpawnChanceEffect * dynamicCoeff * baseChance;
        }
        
        private void RegularSpawn()
        {
            try
            {
                Random rand = new();
                IList<SpawnDto> spawns = _spawnDao.FindAll();
                foreach (SpawnDto spawn in spawns)
                {
                    for (int i = 0; i < spawn.RepeatSpawnRolls; i++)
                    {
                        if (_numberOfSpawns[spawn.PartyTemplate.StringId] <= spawn.MaximumOnMap && (spawn.MinimumNumberOfDaysUntilSpawn < (int)Math.Ceiling(Campaign.Current.CampaignStartTime.ElapsedDaysUntilNow)))
                        {
                            float currentChanceOfSpawn = ComputeChanceSpawn(spawn);
                            if (!_configLoader.Config.IsAllSpawnMode && 
                                (float)rand.NextDouble() >= currentChanceOfSpawn * _configLoader.Config.SpawnChanceFlatMultiplier)
                                continue;

                            var spawnSettlement = GetSpawnSettlement(spawn, (s => spawn.MinimumDevestationToSpawn > _devestationMetricData.GetDevestation(s)), rand);
                            //spawn nao!

                            if (spawnSettlement == null)
                            {
                                //no valid spawn settlement

                                break;
                            }

                            MobileParty spawnedParty = _spawner.SpawnParty(spawnSettlement, spawn.SpawnClan, spawn.PartyTemplate, spawn.BaseSpeedOverride, new TextObject(spawn.Name));
                            if (spawnedParty == null)
                                return;
                            _numberOfSpawns[spawn.PartyTemplate.StringId]++; //increment for can spawn and chance modifications
                                                           //dynamic data registration
                            //AI Checks!
                            HandleAIChecks(spawnedParty, spawn, spawnSettlement);
                            //accompanying spawns
                            foreach (var accomp in spawn.SpawnAlongWith)
                            {
                                MobileParty juniorParty = _spawner.SpawnParty(spawnSettlement, spawn.SpawnClan, accomp.templateObject, spawn.BaseSpeedOverride, new TextObject(accomp.name));
                                if (juniorParty == null)
                                    continue;
                                HandleAIChecks(juniorParty, spawn, spawnSettlement); //junior party has same AI behaviour as main party. TODO in future add some junior party AI and reconstruction.
                            }
                            //message if available
                            if (spawn.SpawnMessage != null)
                            {
                                UX.ShowMessage(spawn.SpawnMessage.Information, spawn.SpawnMessage.Color, spawnSettlement.Name.ToString());
                                //if (data.SoundEvent != -1 && !isSpawnSoundPlaying && ConfigLoader.Instance.Config.SpawnSoundEnabled)
                                //{
                                //    var sceneEmpty = Scene.CreateNewScene(false);
                                //    SoundEvent sound = SoundEvent.CreateEvent(data.SoundEvent, sceneEmpty);
                                //    sound.Play();
                                //    isSpawnSoundPlaying = true;
                                //}
                            }
                            _dailyLogger.ReportSpawn(spawnedParty, currentChanceOfSpawn);
                        }
                        else
                        {
                            break;
                        }
                    }
                }
            }
            catch (System.Exception e)
            {
                _messageBoxService.ShowCustomSpawnsErrorMessage(e);
            }
        }

        private void OnPartyDeath(MobileParty mb, CsPartyData dynamicData)
        {
            HandleDeathMessage(mb, dynamicData);
        }

        private void OnPartyCreated(MobileParty mobileParty)
        {
            ReplenishPartyFood(mobileParty);
        }

        #region Behaviour Handlers

        private void HandleDeathMessage(MobileParty mobileParty, CsPartyData partyData)
        {
            SpawnDto? spawn = _spawnDao.FindByPartyTemplateId(partyData.PartyTemplateId);
            if (spawn?.DeathMessage != null)
            {
                UX.ShowMessage(spawn.DeathMessage.Information, spawn.DeathMessage.Color,
                    CampaignUtils.GetClosestInhabitedSettlement(mobileParty).Name.ToString());
            }
        }

        #endregion
        
        private void HandleAIChecks(MobileParty mb, SpawnDto dto, Settlement spawnedSettlement) //TODO handle sub parties being reconstructed!
        {
            try
            {
                bool invalid = false;
                Dictionary<string, bool> aiRegistrations = new();
                if (dto.PatrolAroundSpawn)
                {
                    bool success = AI.AIManager.HourlyPatrolAroundSpawn.RegisterParty(mb, spawnedSettlement);
                    aiRegistrations.Add("Patrol around spawn behaviour: ", success);
                    invalid = invalid ? true : !success;
                }
                if (dto.AttackClosestIfIdleForADay)
                {
                    bool success = AI.AIManager.AttackClosestIfIdleForADayBehaviour.RegisterParty(mb);
                    aiRegistrations.Add("Attack Closest Settlement If Idle for A Day Behaviour: ", success);
                    invalid = invalid ? true : !success;
                }
                if (dto.PatrolAroundClosestLestInterruptedAndSwitch.isValidData)
                {
                    bool success = AI.AIManager.PatrolAroundClosestLestInterruptedAndSwitchBehaviour.RegisterParty(mb, 
                        new AI.PatrolAroundClosestLestInterruptedAndSwitchBehaviour.PatrolAroundClosestLestInterruptedAndSwitchBehaviourData(mb, dto.PatrolAroundClosestLestInterruptedAndSwitch));
                    aiRegistrations.Add("Patrol Around Closest Lest Interrupted And Switch Behaviour: ", success);
                    invalid = invalid ? true : !success;
                }
                if (invalid && _configLoader.Config.IsDebugMode)
                {
                    _messageBoxService.ShowMessage("Custom Spawns AI XML registration error has occured. The party being registered was: " + mb.StringId +
                                                       "\n Here is more info about the behaviours being registered: \n" + String.Join("\n", aiRegistrations.Keys));
                }
            }
            catch (System.Exception e)
            {
                _messageBoxService.ShowCustomSpawnsErrorMessage(e);
            }
        }
        private Settlement GetSpawnSettlement(SpawnDto dto, Func<Settlement , bool> exceptionPredicate, Random rand = null)
        {
            if (rand == null)
                rand = new Random();


            Clan spawnClan = dto.SpawnClan;
            //deal with override of spawn clan.
            if (dto.OverridenSpawnClan.Count != 0)
            {
                spawnClan = dto.OverridenSpawnClan[rand.Next(0, dto.OverridenSpawnClan.Count)];
            }
            //check for one hideout
            Settlement firstHideout = null;
            if (_configLoader.Config.SpawnAtOneHideout)
            {
                foreach (Settlement s in Settlement.All)
                {
                    if (s.IsHideout)
                    {
                        firstHideout = s;
                        break;
                    }
                }
            }

            //deal with town spawn
            Settlement spawnOverride = null;
            if (dto.OverridenSpawnSettlements.Count != 0)
            {
                spawnOverride = CampaignUtils.PickRandomSettlementAmong(new List<Settlement>(dto.OverridenSpawnSettlements.Where(s => !exceptionPredicate(s))),
                    dto.TrySpawnAtList, rand);
            }

            if (spawnOverride == null && dto.OverridenSpawnCultures.Count != 0)
            {
                //spawn at overriden spawn instead!
                spawnOverride = CampaignUtils.PickRandomSettlementOfCulture(dto.OverridenSpawnCultures, exceptionPredicate, dto.TrySpawnAtList);
            }

            if (spawnOverride != null)
                return spawnOverride;

            //get settlement
            List<IMapPoint> parties = spawnClan.WarPartyComponents.Select(warParty => warParty.MobileParty).ToList<IMapPoint>();
            List<Settlement> hideouts = Settlement.All.Where(settlement => settlement.IsHideout).ToList();
            Settlement spawnSettlement = _configLoader.Config.SpawnAtOneHideout ? firstHideout : (dto.TrySpawnAtList.Count == 0 ? CampaignUtils.GetNearestSettlement(hideouts, parties) : null);
            return spawnSettlement;
        }
    }
}
