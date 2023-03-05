using CustomSpawns.AI;
using CustomSpawns.AI.Barterables;
using CustomSpawns.CampaignData.Config;
using CustomSpawns.CampaignData.Implementations;
using CustomSpawns.Config;
using CustomSpawns.Data.Adapter;
using CustomSpawns.Data.Dao;
using CustomSpawns.Data.Reader.Impl;
using CustomSpawns.Dialogues;
using CustomSpawns.Diplomacy;
using CustomSpawns.HarmonyPatches;
using CustomSpawns.ModIntegration;
using CustomSpawns.PartySpeed;
using CustomSpawns.RewardSystem;
using CustomSpawns.Spawn;
using CustomSpawns.UtilityBehaviours;
using CustomSpawns.Utils;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

namespace CustomSpawns
{

    public class Main : MBSubModuleBase
    {
        public static string ModuleName = "CustomSpawns";

        #region Dependencies
        private IDiplomacyActionModel _diplomacyActionModel;
        private TrackClanKingdom _clanKingdomTrackable;
        private CustomSpawnsClanDiplomacyModel _customSpawnsClanDiplomacyModel;
        private BanditPartySpawnFactory _banditPartySpawnFactory;
        private CustomPartySpawnFactory _customPartySpawnFactory;
        private Spawner _spawner;
        private SubModService _subModService;
        private DiplomacyDataReader _diplomacyDataReader;
        private DialogueDataReader _dialogueDataReader;
        private SpawnDataReader _spawnDataReader;
        private NameSignifierDataReader _nameSignifierDataReader;
        private DynamicSpawnData _dynamicSpawnData;
        private SpawnDtoAdapter _spawnDtoAdapter;
        private SpawnDao _spawnDao;
        private PartySpeedContext _partySpeedContext;
        private CampaignDataConfigLoader _campaignDataConfigLoader;
        private ConfigLoader _configLoader;
        private MessageBoxService _messageBoxService;
        private DialogueConsequenceInterpretor _dialogueConsequenceInterpretor;
        private DialogueConditionInterpretor _dialogueConditionInterpretor;
        private ModDebug _modDebug;
        private RewardDataReader _rewardDataReader;
        private RewardDao _rewardDao;
        private DialogueDao _dialogueDao;
        private DialogueDtoAdapter _dialogueDtoAdapter;

        private PatchManager _patchManager;

        private SpawnCheats _spawnCheats;
        
        // Behaviours
        private HourlyPatrolAroundSpawnBehaviour _hourlyPatrolAroundSpawnBehaviour;
        private AttackClosestIfIdleForADayBehaviour _attackClosestIfIdleForADayBehaviour;
        private PatrolAroundClosestLestInterruptedAndSwitchBehaviour _patrolAroundClosestLestInterruptedAndSwitchBehaviour;
        private CustomSpawnsDialogueBehaviour _customSpawnsDialogueBehaviour;
        private SpawnRewardBehaviour _spawnRewardBehaviour;
        private MobilePartyTrackingBehaviour _mobilePartyTrackingBehaviour;
        private DevestationMetricData _devestationMetricData;
        private DailyLogger _dailyLogger;
        private ForcedWarPeaceBehaviour _forcedWarPeaceBehaviour;
        private ForceNoKingdomBehaviour _forceNoKingdomBehaviour;
        private SpawnBehaviour _spawnBehaviour;
        private SaveInitialiser _saveInitialiser;
        private SafePassageBehaviour _safePassageBehaviour;


        private void InstantiateDependencies()
        {
            // TODO setup IoC
            _saveInitialiser = new SaveInitialiser();
            _messageBoxService = new MessageBoxService();
            _subModService = new SubModService(_messageBoxService);
            _configLoader = new ConfigLoader(_subModService, _messageBoxService);
            _modDebug = new ModDebug(_configLoader);
            _partySpeedContext = new PartySpeedContext(_configLoader);
            _diplomacyDataReader = new (_subModService, _messageBoxService);
            _nameSignifierDataReader = new (_subModService, _messageBoxService);
            _spawnDataReader = new (_subModService, _messageBoxService);
            _diplomacyActionModel = new ConstantWarDiplomacyActionModel();
            _clanKingdomTrackable = new TrackClanKingdom();
            _customSpawnsClanDiplomacyModel = new CustomSpawnsClanDiplomacyModel(_clanKingdomTrackable, _diplomacyActionModel, _diplomacyDataReader);
            _banditPartySpawnFactory = new BanditPartySpawnFactory();
            _customPartySpawnFactory = new CustomPartySpawnFactory();
            _spawner = new Spawner(_banditPartySpawnFactory, _customPartySpawnFactory, _messageBoxService, _modDebug);
            _campaignDataConfigLoader = new CampaignDataConfigLoader(_subModService, _messageBoxService);
            _spawnDtoAdapter = new SpawnDtoAdapter(_nameSignifierDataReader, Campaign.Current.ObjectManager, Campaign.Current.CampaignObjectManager, _partySpeedContext);
            _spawnDao = new SpawnDao(_spawnDataReader, _spawnDtoAdapter, _messageBoxService);
            _dialogueConsequenceInterpretor = new DialogueConsequenceInterpretor(_diplomacyActionModel, _messageBoxService);
            _dialogueConditionInterpretor = new DialogueConditionInterpretor(_spawnDao);
            _hourlyPatrolAroundSpawnBehaviour = new HourlyPatrolAroundSpawnBehaviour(_messageBoxService, _modDebug);
            _attackClosestIfIdleForADayBehaviour = new AttackClosestIfIdleForADayBehaviour(_modDebug);
            _patrolAroundClosestLestInterruptedAndSwitchBehaviour = new PatrolAroundClosestLestInterruptedAndSwitchBehaviour(_modDebug);
            _dialogueDataReader = new DialogueDataReader(_subModService, _messageBoxService);
            _dialogueDtoAdapter = new DialogueDtoAdapter(_dialogueConsequenceInterpretor, _dialogueConditionInterpretor);
            _dialogueDao = new DialogueDao(_dialogueDataReader, _dialogueDtoAdapter, _messageBoxService);
            _customSpawnsDialogueBehaviour = new CustomSpawnsDialogueBehaviour(_dialogueDao);
            _rewardDataReader = new RewardDataReader(_subModService, _messageBoxService);
            _rewardDao = new RewardDao(_rewardDataReader);
            _spawnRewardBehaviour = new SpawnRewardBehaviour(_rewardDao);
            _mobilePartyTrackingBehaviour = new MobilePartyTrackingBehaviour(_saveInitialiser, _modDebug);
            _dynamicSpawnData = new (_spawnDao, _saveInitialiser);
            _devestationMetricData = new DevestationMetricData(_mobilePartyTrackingBehaviour, _campaignDataConfigLoader, _saveInitialiser, _messageBoxService, _modDebug);
            _dailyLogger = new DailyLogger(_devestationMetricData, _dynamicSpawnData, _campaignDataConfigLoader, _messageBoxService, _subModService, _spawnDao);
            _forcedWarPeaceBehaviour = new ForcedWarPeaceBehaviour(_diplomacyActionModel, _clanKingdomTrackable, 
                _customSpawnsClanDiplomacyModel, _diplomacyDataReader, _dailyLogger);
            _forceNoKingdomBehaviour = new ForceNoKingdomBehaviour(_diplomacyDataReader, _dailyLogger);
            _spawnBehaviour = new SpawnBehaviour(_spawner, _spawnDao, _dynamicSpawnData, _saveInitialiser, _devestationMetricData, _configLoader, _messageBoxService, _dailyLogger, _modDebug);
            _patchManager = new PatchManager(_spawnDao, _partySpeedContext, _configLoader, _messageBoxService);
            _safePassageBehaviour = new SafePassageBehaviour(_spawnDao);
            _spawnCheats = new SpawnCheats(_spawner, _spawnDao);
        }
        #endregion

        private void DisplayLoadedModules()
        {
            UX.ShowMessage("Custom Spawns API loaded", Color.ConvertStringToColor("#001FFFFF"));
            AIManager.FlushRegisteredBehaviours(); //forget old behaviours to allocate space. 
            foreach (var subMod in _subModService.GetAllLoadedSubMods())
            {
                UX.ShowMessage(subMod.SubModuleName + " is now integrated into the Custom Spawns API.",
                    Color.ConvertStringToColor("#001FFFFF"));
            }
        }

        protected override void InitializeGameStarter(Game game, IGameStarter gameStarterObject)
        {
            if (!(gameStarterObject is CampaignGameStarter) || !(game.GameType is Campaign))
            {
                return;
            }
            InstantiateDependencies();
            AddBehaviours((CampaignGameStarter) gameStarterObject);
            DisplayLoadedModules();
        }

        private void AddBehaviours(CampaignGameStarter starter)
        {
            if (!_configLoader.Config.IsRemovalMode)
            {
                starter.AddBehavior(_hourlyPatrolAroundSpawnBehaviour);
                starter.AddBehavior(_attackClosestIfIdleForADayBehaviour);
                starter.AddBehavior(_patrolAroundClosestLestInterruptedAndSwitchBehaviour);
                starter.AddBehavior(_customSpawnsDialogueBehaviour);
                starter.AddBehavior(_spawnRewardBehaviour);
                starter.AddBehavior(_mobilePartyTrackingBehaviour);
                starter.AddBehavior(_devestationMetricData);
                starter.AddBehavior(_dailyLogger);
                starter.AddBehavior(_forcedWarPeaceBehaviour);
                starter.AddBehavior(_forceNoKingdomBehaviour);
                starter.AddBehavior(_spawnBehaviour);
                starter.AddBehavior(_saveInitialiser);
                starter.AddBehavior(_safePassageBehaviour);
                starter.AddBehavior(_dynamicSpawnData);
            }
            else
            {
                starter.AddBehavior(new RemoverBehaviour());
            }
        }

        public override void OnGameInitializationFinished(Game game)
        {
            base.OnGameInitializationFinished(game);
            if (!(game.GameType is Campaign))
            {
                return;
            }
            _patchManager.ApplyPatches();
        }
    }
}