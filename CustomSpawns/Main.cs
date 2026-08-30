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
using CustomSpawns.ModIntegration;
using CustomSpawns.RewardSystem;
using CustomSpawns.Spawn;
using CustomSpawns.Spawn.PartySize;
using CustomSpawns.UtilityBehaviours;
using CustomSpawns.Utils;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;

namespace CustomSpawns
{

    public class Main : MBSubModuleBase
    {
        public static string ModuleName = "CustomSpawns";

        #region Dependencies
        private IFactionDiplomacyProvider _constantWarFactionDiplomacyProvider;
        private CustomSpawnsClanDiplomacyProvider _customSpawnsClanDiplomacyProvider;
        private CustomSpawnsDiplomacyProvider _customSpawnsDiplomacyProvider;
        private CustomSpawnsDiplomacyModel _customSpawnsDiplomacyModel;
        private CustomSpawnsDiplomacyFactionsProvider _customSpawnsDiplomacyFactionsProvider;
        private CustomSpawnsDiplomacyBehaviour _customSpawnsDiplomacyBehaviour;
        private BanditPartySpawnFactory _banditPartySpawnFactory;
        private CustomPartySpawnFactory _customPartySpawnFactory;
        private Spawner _spawner;
        private SpawnGroupService _spawnGroupService;
        private SubModService _subModService;
        private DiplomacyDataReader _diplomacyDataReader;
        private DialogueDataReader _dialogueDataReader;
        private SpawnDataReader _spawnDataReader;
        private PartyBehaviourDataReader _partyBehaviourDataReader;
        private PartyBehaviourDtoAdapter _partyBehaviourDtoAdapter;
        private PartyBehaviourDao _partyBehaviourDao;
        private NameSignifierDataReader _nameSignifierDataReader;
        private DynamicSpawnData _dynamicSpawnData;
        private SpawnDtoAdapter _spawnDtoAdapter;
        private SpawnDao _spawnDao;
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
        private PartySizeCalculatedSubject _partySizeCalculatedSubject;
        private ISuzerainProvider _suzerainProvider;

        private SpawnCheats _spawnCheats;
        
        // Behaviours
        private CustomSpawnsDialogueBehaviour _customSpawnsDialogueBehaviour;
        private SpawnRewardBehaviour _spawnRewardBehaviour;
        private MobilePartyTrackingBehaviour _mobilePartyTrackingBehaviour;
        private DevestationMetricData _devestationMetricData;
        private DailyLogger _dailyLogger;
        private SpawnBehaviour _spawnBehaviour;
        private SaveInitialiser _saveInitialiser;
        private SafePassageBehaviour _safePassageBehaviour;
        private CustomSpawnsAiBehaviour _customSpawnsAiBehaviour;
        private AccompanyingPartySiegeBehaviour _accompanyingPartySiegeBehaviour;
        private AiDecisionLoggerBehaviour _aiDecisionLoggerBehaviour;


        private void InstantiateDependencies(CampaignGameStarter campaignGameStarter)
        {
            // TODO setup IoC
            _saveInitialiser = new SaveInitialiser();
            _messageBoxService = new MessageBoxService();
            _subModService = new SubModService(_messageBoxService);
            _configLoader = new ConfigLoader(_subModService, _messageBoxService);
            _modDebug = new ModDebug(_configLoader);
            _diplomacyDataReader = new (_subModService, _messageBoxService);
            _nameSignifierDataReader = new (_subModService, _messageBoxService);
            _spawnDataReader = new (_subModService, _messageBoxService);
            _partyBehaviourDataReader = new (_subModService, _messageBoxService);
            _constantWarFactionDiplomacyProvider = new ConstantWarFactionDiplomacyProvider();
            _suzerainProvider = new SuzerainProvider();
            _customSpawnsClanDiplomacyProvider = new CustomSpawnsClanDiplomacyProvider(_suzerainProvider, _diplomacyDataReader);
            _customSpawnsDiplomacyProvider = new CustomSpawnsDiplomacyProvider(_customSpawnsClanDiplomacyProvider, _suzerainProvider);
            _banditPartySpawnFactory = new BanditPartySpawnFactory();
            _customPartySpawnFactory = new CustomPartySpawnFactory();
            _partyBehaviourDtoAdapter = new PartyBehaviourDtoAdapter();
            _partyBehaviourDao = new PartyBehaviourDao(_partyBehaviourDataReader, _partyBehaviourDtoAdapter, _messageBoxService);
            var besiegingPartyLordResolver = new BesiegingPartyLordResolver(_partyBehaviourDao, _messageBoxService, _modDebug);
            _spawner = new Spawner(_banditPartySpawnFactory, _customPartySpawnFactory, besiegingPartyLordResolver, _messageBoxService, _modDebug);
            _campaignDataConfigLoader = new CampaignDataConfigLoader(_subModService, _messageBoxService);
            _spawnDtoAdapter = new SpawnDtoAdapter(_nameSignifierDataReader, Campaign.Current.ObjectManager, Campaign.Current.CampaignObjectManager);
            _spawnDao = new SpawnDao(_spawnDataReader, _spawnDtoAdapter, _messageBoxService);
            _dialogueConsequenceInterpretor = new DialogueConsequenceInterpretor(_constantWarFactionDiplomacyProvider, _messageBoxService);
            _dialogueConditionInterpretor = new DialogueConditionInterpretor(_spawnDao);
            _dialogueDataReader = new DialogueDataReader(_subModService, _messageBoxService);
            _dialogueDtoAdapter = new DialogueDtoAdapter(_dialogueConsequenceInterpretor, _dialogueConditionInterpretor);
            _dialogueDao = new DialogueDao(_dialogueDataReader, _dialogueDtoAdapter, _messageBoxService);
            _customSpawnsDialogueBehaviour = new CustomSpawnsDialogueBehaviour(_dialogueDao);
            _rewardDataReader = new RewardDataReader(_subModService, _messageBoxService);
            _rewardDao = new RewardDao(_rewardDataReader);
            _spawnRewardBehaviour = new SpawnRewardBehaviour(_rewardDao, _modDebug);
            _mobilePartyTrackingBehaviour = new MobilePartyTrackingBehaviour(_saveInitialiser, _modDebug);
            _dynamicSpawnData = new (_spawnDao, _saveInitialiser);
            _spawnGroupService = new SpawnGroupService(_spawner, _dynamicSpawnData);
            _devestationMetricData = new DevestationMetricData(_mobilePartyTrackingBehaviour, _campaignDataConfigLoader, _saveInitialiser, _messageBoxService, _modDebug);
            _dailyLogger = new DailyLogger(_devestationMetricData, _messageBoxService);
            IPriorityPartyBehaviour[] priorityPartyBehaviours =
            {
                new PatrolAroundClosestLestInterruptedAndSwitchBehaviour(),
                new RaidClosestVillageBehaviour(
                    new BannerlordRaidClosestVillageAdapter(_dynamicSpawnData), MBRandom.RandomFloatRanged),
                new BesiegeClosestSettlementBehaviour(
                    new BannerlordBesiegeClosestSettlementAdapter(_dynamicSpawnData), MBRandom.RandomFloatRanged)
            };
            var partyBehaviourResolver = new PartyBehaviourResolver(_spawnDao, _partyBehaviourDao,
                priorityPartyBehaviours, new HomeSettlementPatrolBehaviour(), new FollowSupportedPartyBehaviour(),
                _messageBoxService);
            _customSpawnsAiBehaviour = new CustomSpawnsAiBehaviour(_dynamicSpawnData, partyBehaviourResolver);
            _accompanyingPartySiegeBehaviour = new AccompanyingPartySiegeBehaviour(_dynamicSpawnData);
            _aiDecisionLoggerBehaviour = new AiDecisionLoggerBehaviour(_dailyLogger);
            NonMercenaryCustomSpawnsFactionsProvider nonMercenaryCustomSpawnsFactionsProvider =
                new NonMercenaryCustomSpawnsFactionsProvider(_diplomacyDataReader);
            _customSpawnsDiplomacyFactionsProvider = new CustomSpawnsDiplomacyFactionsProvider(_diplomacyDataReader);
            _customSpawnsDiplomacyModel = new CustomSpawnsDiplomacyModel(campaignGameStarter.GetModel<DiplomacyModel>(), _customSpawnsDiplomacyProvider, _dailyLogger, nonMercenaryCustomSpawnsFactionsProvider);
            _customSpawnsDiplomacyBehaviour = new CustomSpawnsDiplomacyBehaviour(_customSpawnsDiplomacyFactionsProvider,
                _customSpawnsClanDiplomacyProvider, nonMercenaryCustomSpawnsFactionsProvider, _dailyLogger);
            _partySizeCalculatedSubject = new PartySizeCalculatedSubject();
            _spawnBehaviour = new SpawnBehaviour(_spawnGroupService, _spawnDao, _dynamicSpawnData, _saveInitialiser, _devestationMetricData, _configLoader, _messageBoxService, _dailyLogger, _modDebug, _partySizeCalculatedSubject);
            _safePassageBehaviour = new SafePassageBehaviour(_spawnDao);
            _spawnCheats = new SpawnCheats(_spawnGroupService, _spawnDao);
        }
        #endregion

        private void DisplayLoadedModules()
        {
            UX.ShowMessage(new TextObject("{=SpawnAPIMsg000}Custom Spawns API loaded").ToString(), Color.ConvertStringToColor("#001FFFFF"));
            foreach (var subMod in _subModService.GetAllLoadedSubMods())
            {
                UX.ShowMessage(new TextObject("{=SpawnAPIMsg010}{NAME} is now integrated into the Custom Spawns API.").SetTextVariable("NAME", subMod.SubModuleName).ToString(),
                    Color.ConvertStringToColor("#001FFFFF"));
            }
        }

        protected override void InitializeGameStarter(Game game, IGameStarter gameStarterObject)
        {
            if (!(gameStarterObject is CampaignGameStarter campaignGameStarter) || !(game.GameType is Campaign))
            {
                return;
            }
            InstantiateDependencies(campaignGameStarter);
            AddBehaviours(campaignGameStarter);
            DisplayLoadedModules();
        }

        private void AddBehaviours(CampaignGameStarter starter)
        {
            if (!_configLoader.Config.IsRemovalMode)
            {
                starter.AddBehavior(_customSpawnsDialogueBehaviour);
                starter.AddBehavior(_spawnRewardBehaviour);
                starter.AddBehavior(_mobilePartyTrackingBehaviour);
                starter.AddBehavior(_devestationMetricData);
                starter.AddBehavior(_dailyLogger);
                starter.AddBehavior(_spawnBehaviour);
                starter.AddBehavior(_saveInitialiser);
                starter.AddBehavior(_safePassageBehaviour);
                starter.AddBehavior(_dynamicSpawnData);
                starter.AddBehavior(_customSpawnsDiplomacyBehaviour);
                starter.AddBehavior(_customSpawnsAiBehaviour);
                starter.AddBehavior(_accompanyingPartySiegeBehaviour);
                starter.AddBehavior(_aiDecisionLoggerBehaviour);
                
                starter.AddModel(_customSpawnsDiplomacyModel);
                starter.AddModel(new CustomSpawnsPartySizeLimitModel(starter.GetModel<PartySizeLimitModel>(),
                    _partySizeCalculatedSubject));
            }
            else
            {
                starter.AddBehavior(new RemoverBehaviour());
            }
        }

    }
}
