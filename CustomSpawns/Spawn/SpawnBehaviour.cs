using System;
using System.Collections.Generic;
using System.Linq;
using CustomSpawns.CampaignData.Implementations;
using CustomSpawns.Config;
using CustomSpawns.Data;
using CustomSpawns.Data.Dao;
using CustomSpawns.Data.Dto;
using CustomSpawns.Economics;
using CustomSpawns.Spawn.PartySize;
using CustomSpawns.UtilityBehaviours;
using CustomSpawns.Utils;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Map;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace CustomSpawns.Spawn
{
    class SpawnBehaviour : CampaignBehaviorBase
    {
        private readonly Dictionary<string, int> _numberOfSpawns;
        private readonly SpawnGroupService _spawnGroupService;
        private readonly SpawnDao _spawnDao;
        private readonly DynamicSpawnData _dynamicSpawnData;
        private readonly DevestationMetricData _devestationMetricData;
        private readonly ConfigLoader _configLoader;
        private readonly MessageBoxService _messageBoxService;
        private readonly DailyLogger _dailyLogger;
        private readonly ModDebug _modDebug;
        private readonly Func<CsPartyData, bool> _hasPersistentInvasionGroup;
        private readonly Func<string, bool> _isInvasionUnderway;
        
        #region Data Management

        private int _lastRedundantDataUpdate = 0;

        public SpawnBehaviour(
            SpawnGroupService spawnGroupService,
            SpawnDao spawnDao,
            DynamicSpawnData dynamicSpawnData,
            SaveInitialiser saveInitialiser,
            DevestationMetricData devestationMetricData,
            ConfigLoader configLoader,
            MessageBoxService messageBoxService,
            DailyLogger dailyLogger,
            ModDebug modDebug,
            PartySizeCalculatedSubject partySizeCalculatedSubject,
            Func<CsPartyData, bool> hasPersistentInvasion,
            Func<string, bool> isInvasionUnderway
        )
        {
            _spawnGroupService = spawnGroupService;
            _spawnDao = spawnDao;
            _dynamicSpawnData = dynamicSpawnData;
            _devestationMetricData = devestationMetricData;
            _messageBoxService = messageBoxService;
            _modDebug = modDebug;
            _lastRedundantDataUpdate = 0;
            _numberOfSpawns = new();
            _configLoader = configLoader;
            _dailyLogger = dailyLogger;
            _hasPersistentInvasionGroup = hasPersistentInvasion;
            _isInvasionUnderway = isInvasionUnderway;
            saveInitialiser.RunCallbackOnFirstCampaignTick(OnSaveStart);
            // Set the party size of a custom party to the size of the actual spawned party
            // This cancels the speed malus that is applied to non-lordly parties that are too big
            partySizeCalculatedSubject.Register(new PartySizeResizerOnFirstCalculation(spawnDao, dynamicSpawnData));
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
            CampaignEvents.MobilePartyDestroyed.AddNonSerializedListener(this, OnMobilePartyDestroyed);
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
                    CsPartyData? partyData = _dynamicSpawnData.GetDynamicSpawnData(mb);
                    SpawnDto? spawn = partyData?.OwningSpawnTemplateId == null
                        ? null
                        : _spawnDao.FindByPartyTemplateId(partyData.OwningSpawnTemplateId);
                    if(spawn != null)
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
            HandlePartyRemoval(partyBase?.MobileParty);
        }

        private void OnMobilePartyDestroyed(MobileParty? mobileParty, PartyBase destroyer)
        {
            HandlePartyRemoval(mobileParty);
        }

        private void HandlePartyRemoval(MobileParty? mobileParty)
        {
            CsPartyData? partyData = _dynamicSpawnData.TakeDynamicSpawnData(mobileParty);
            if (partyData == null || mobileParty == null)
                return;

            if (TryDecrementPrimarySpawnCount(_numberOfSpawns, partyData))
            {
                _modDebug.ShowMessage(mobileParty.StringId + " has died at " + partyData.LatestClosestSettlement + ", reducing the total number to: " + _numberOfSpawns[partyData.OwningSpawnTemplateId!], DebugMessageType.DeathTrack);
            }

            OnPartyDeath(mobileParty, partyData);
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
                TryIncrementPrimarySpawnCount(_numberOfSpawns, _dynamicSpawnData.GetDynamicSpawnData(mb));
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
                    // An invasion's reinforcements come through its own capital, not a second landing beside it.
                    // An invasion that follows another is launched by that one's conquest, never by chance.
                    if (spawn.Invasion != null && (_isInvasionUnderway(spawn.PartyTemplate.StringId) ||
                                                   spawn.Invasion.FollowsInvasion != null))
                    {
                        continue;
                    }

                    for (int i = 0; i < spawn.RepeatSpawnRolls; i++)
                    {
                        // MaximumConcurrentPrimaries is what this test keeps on the map.
                        if (_numberOfSpawns[spawn.PartyTemplate.StringId] < spawn.MaximumConcurrentPrimaries && (spawn.MinimumNumberOfDaysUntilSpawn < (int)Math.Ceiling(Campaign.Current.Models.CampaignTimeModel.CampaignStartTime.ElapsedDaysUntilNow)))
                        {
                            float currentChanceOfSpawn = ComputeChanceSpawn(spawn);
                            float spawnChanceFlatMultiplier = _configLoader.Config.SpawnChanceFlatMultiplier;
                            float chanceRoll = (float)rand.NextDouble();
                            if (!_configLoader.Config.IsAllSpawnMode)
                            {
                                if (chanceRoll >= currentChanceOfSpawn * spawnChanceFlatMultiplier)
                                    continue;
                            }

                            Clan configuredSpawnClan = GetConfiguredSpawnClan(spawn, rand);
                            var spawnSettlement = GetSpawnSettlement(spawn, configuredSpawnClan, (s => spawn.MinimumDevestationToSpawn > _devestationMetricData.GetDevestation(s)), rand);
                            //spawn nao!

                            if (spawnSettlement == null)
                            {
                                //no valid spawn settlement

                                break;
                            }

                            Clan spawnClan = GetSpawnClan(spawn, spawnSettlement, configuredSpawnClan);
                            MobileParty spawnedParty = _spawnGroupService.Spawn(spawn, spawnSettlement, spawnClan);
                            if (spawnedParty == null)
                                return;
                            _numberOfSpawns[spawn.PartyTemplate.StringId]++; //increment for can spawn and chance modifications
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
                            _dailyLogger.ReportSpawn(spawnedParty, spawn, spawnSettlement, spawnClan, currentChanceOfSpawn,
                                spawnChanceFlatMultiplier, chanceRoll, i + 1, _numberOfSpawns[spawn.PartyTemplate.StringId]);
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
            if (ShouldSuppressDeathMessage(partyData, _hasPersistentInvasionGroup))
                return;
            SpawnDto? spawn = partyData.OwningSpawnTemplateId == null
                ? null
                : _spawnDao.FindByPartyTemplateId(partyData.OwningSpawnTemplateId);
            if (spawn?.DeathMessage != null)
            {
                UX.ShowMessage(spawn.DeathMessage.Information, spawn.DeathMessage.Color,
                    CampaignUtils.GetClosestInhabitedSettlement(mobileParty).Name.ToString());
            }
        }

        public static bool ShouldSuppressDeathMessage(CsPartyData partyData,
            Func<CsPartyData, bool> hasPersistentInvasion)
        {
            return partyData.IsAdopted ||
                   (partyData.IsPrimarySpawn && hasPersistentInvasion(partyData));
        }

        #endregion
        
        private void HandleAIChecks(MobileParty mb, SpawnDto dto, Settlement spawnedSettlement) //TODO handle sub parties being reconstructed!
        {
            // mb.SetMovePatrolAroundPoint(mb.Position, mb.NavigationCapability);
        }

        private Settlement GetSpawnSettlement(SpawnDto dto, Clan spawnClan, Func<Settlement , bool> exceptionPredicate, Random rand = null)
        {
            if (rand == null)
                rand = new Random();

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

        public static bool TryDecrementPrimarySpawnCount(IDictionary<string, int> numberOfSpawns, CsPartyData partyData)
        {
            if (!partyData.IsPrimarySpawn || partyData.OwningSpawnTemplateId == null
                                          || !numberOfSpawns.ContainsKey(partyData.OwningSpawnTemplateId))
            {
                return false;
            }

            numberOfSpawns[partyData.OwningSpawnTemplateId]--;
            return true;
        }

        public static bool TryIncrementPrimarySpawnCount(IDictionary<string, int> numberOfSpawns,
            CsPartyData? partyData)
        {
            if (partyData == null || !partyData.IsPrimarySpawn || partyData.OwningSpawnTemplateId == null
                                  || !numberOfSpawns.ContainsKey(partyData.OwningSpawnTemplateId))
            {
                return false;
            }

            numberOfSpawns[partyData.OwningSpawnTemplateId]++;
            return true;
        }

        public static T ResolveSpawnClan<T>(bool inheritClanFromSettlement, T configuredSpawnClan, T? settlementOwnerClan)
            where T : class
        {
            return inheritClanFromSettlement && settlementOwnerClan != null ? settlementOwnerClan : configuredSpawnClan;
        }

        private static Clan GetSpawnClan(SpawnDto dto, Settlement spawnSettlement, Clan configuredSpawnClan)
        {
            return ResolveSpawnClan(dto.InheritClanFromSettlement, configuredSpawnClan, spawnSettlement.OwnerClan);
        }

        private static Clan GetConfiguredSpawnClan(SpawnDto dto, Random rand)
        {
            if (dto.OverridenSpawnClan.Count != 0)
            {
                return dto.OverridenSpawnClan[rand.Next(0, dto.OverridenSpawnClan.Count)];
            }

            return dto.SpawnClan;
        }
    }
}
