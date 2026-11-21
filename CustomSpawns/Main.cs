using System.Collections.Generic;
using CustomSpawns.AI;
using CustomSpawns.AI.Adapters;
using CustomSpawns.AI.Barterables;
using CustomSpawns.AI.Behaviours;
using CustomSpawns.AI.CampaignBehaviours;
using CustomSpawns.AI.Model;
using CustomSpawns.AI.Offensives;
using CustomSpawns.CampaignData.Config;
using CustomSpawns.CampaignData.Implementations;
using CustomSpawns.Config;
using CustomSpawns.Data.Adapter;
using CustomSpawns.Data.Dao;
using CustomSpawns.Data.Reader.Impl;
using CustomSpawns.Dialogues;
using CustomSpawns.Diplomacy;
using CustomSpawns.Invasions.Adapters;
using CustomSpawns.Invasions.Alliances.Adapters;
using CustomSpawns.Invasions.Bank;
using CustomSpawns.Invasions.Bank.Adapters;
using CustomSpawns.Invasions.CapitalObjectives.Adapters;
using CustomSpawns.Invasions.Conquest.Adapters;
using CustomSpawns.Invasions.GarrisonArmies.Adapters;
using CustomSpawns.Invasions.Lords.Adapters;
using CustomSpawns.Invasions.PlayerSparing.Adapters;
using CustomSpawns.Invasions.Reinforcements.Adapters;
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
        private InvasionCampaignBehaviour _invasionCampaignBehaviour;
        private InvasionCapitalRepository _invasionCapitalRepository;
        private InvasionClanRepository _invasionClanRepository;
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
        private StartupLogReport _startupLogReport;


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
            _partyBehaviourDtoAdapter = new PartyBehaviourDtoAdapter(PartyBehaviourRegistrations.ConfigAdapters);
            _partyBehaviourDao = new PartyBehaviourDao(_partyBehaviourDataReader, _partyBehaviourDtoAdapter, _messageBoxService);
            var besiegingPartyLordResolver = new BesiegingPartyLordResolver(_partyBehaviourDao, _messageBoxService, _modDebug);
            _spawner = new Spawner(_banditPartySpawnFactory, _customPartySpawnFactory, besiegingPartyLordResolver, _messageBoxService, _modDebug);
            _campaignDataConfigLoader = new CampaignDataConfigLoader(_subModService, _messageBoxService);
            _spawnDtoAdapter = new SpawnDtoAdapter(_nameSignifierDataReader, Campaign.Current.ObjectManager,
                Campaign.Current.CampaignObjectManager, _partyBehaviourDao);
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
            _invasionCapitalRepository = new InvasionCapitalRepository();
            _invasionClanRepository = new InvasionClanRepository(_spawnDao, _saveInitialiser);
            _invasionCampaignBehaviour = new InvasionCampaignBehaviour(_spawnDao, _dynamicSpawnData,
                _saveInitialiser, _invasionCapitalRepository, _invasionClanRepository, _spawnGroupService,
                _diplomacyDataReader.Data);
            _saveInitialiser.RunCallbackOnFirstCampaignTick(new VanillaMilitaryAiFirst().Reregister);
            _devestationMetricData = new DevestationMetricData(_mobilePartyTrackingBehaviour, _campaignDataConfigLoader, _saveInitialiser, _messageBoxService, _modDebug);
            _dailyLogger = new DailyLogger(_devestationMetricData, _messageBoxService);
            // The siege behaviour's own state, held here so that the targeting policy can read which
            // objective each party of an invasion has committed to. The behaviour still owns saving it.
            var siegeStates = new PartyStateStore<SettlementOffensiveBehaviourState>(
                BesiegeClosestSettlementBehaviour.SaveKey);
            var invasionCoastalSiegeTargetPolicy = new InvasionCoastalSiegeTargetPolicy(
                _invasionCampaignBehaviour, new BannerlordMapPathQuery(),
                party => siegeStates.Find(party)?.Target);
            // An invasion never lands on a settlement the player holds, whatever the coast allows.
            var invasionPlayerSparingSiegeTargetPolicy = new InvasionPlayerSparingSiegeTargetPolicy(
                invasionCoastalSiegeTargetPolicy, _invasionCampaignBehaviour.InvasionLandingSparing);
            IPriorityPartyBehaviour[] priorityPartyBehaviours =
                PartyBehaviourRegistrations.CreateBehaviours(_dynamicSpawnData,
                    invasionPlayerSparingSiegeTargetPolicy, siegeStates);
            var partyBehaviourLookup = new SpawnPartyBehaviourLookup(_spawnDao, _partyBehaviourDao,
                _messageBoxService);
            _invasionCampaignBehaviour.ConfigureCapitalObjectives(
                new BannerlordInvasionAssaultConfigurationProvider(_spawnDao, partyBehaviourLookup));
            var priorityPartyBehaviourRegistry = new PriorityPartyBehaviourRegistry(priorityPartyBehaviours);
            PartyBehaviourResolver.ValidateConfiguredBehaviorTypes(partyBehaviourLookup, priorityPartyBehaviourRegistry);
            var homeSettlementPatrolBehaviour = new HomeSettlementPatrolBehaviour();
            var partyBehaviourResolver = new PartyBehaviourResolver(
                partyBehaviourLookup, priorityPartyBehaviourRegistry, homeSettlementPatrolBehaviour,
                new FollowSupportedPartyBehaviour());
            var steeredParties = new SteeredPartyLookup(_dynamicSpawnData, _invasionCampaignBehaviour);
            IPartyOrder capitalOrders = new CompositePartyOrder(new IPartyOrder[]
            {
                new InvasionCapitalDefenceOrder(_invasionCampaignBehaviour),
                new InvasionCapitalAttackOrder(_invasionCampaignBehaviour),
                _invasionCampaignBehaviour.InvasionGarrisonArmyCampaign.InvasionGarrisonArmyOrder
            });
            _customSpawnsAiBehaviour = new CustomSpawnsAiBehaviour(steeredParties, partyBehaviourResolver,
                capitalOrders,
                new List<ISyncsCampaignData>(priorityPartyBehaviours) { homeSettlementPatrolBehaviour });
            _accompanyingPartySiegeBehaviour = new AccompanyingPartySiegeBehaviour(steeredParties);
            _aiDecisionLoggerBehaviour = new AiDecisionLoggerBehaviour(_dailyLogger, siegeStates,
                invasionCoastalSiegeTargetPolicy.GetAttackingForce,
                _dynamicSpawnData.FindAccompanyingParties);
            NonMercenaryCustomSpawnsFactionsProvider nonMercenaryCustomSpawnsFactionsProvider =
                new NonMercenaryCustomSpawnsFactionsProvider(_diplomacyDataReader);
            _customSpawnsDiplomacyFactionsProvider = new CustomSpawnsDiplomacyFactionsProvider(_diplomacyDataReader,
                new BannerlordClanFinder());
            _customSpawnsDiplomacyModel = new CustomSpawnsDiplomacyModel(campaignGameStarter.GetModel<DiplomacyModel>(), _customSpawnsDiplomacyProvider, _dailyLogger, nonMercenaryCustomSpawnsFactionsProvider,
                _invasionCampaignBehaviour.BannerlordInvasionConquestStances);
            _customSpawnsDiplomacyBehaviour = new CustomSpawnsDiplomacyBehaviour(_customSpawnsDiplomacyFactionsProvider,
                _customSpawnsClanDiplomacyProvider, nonMercenaryCustomSpawnsFactionsProvider, _dailyLogger,
                _invasionCampaignBehaviour.BannerlordInvasionConquestStances);
            _partySizeCalculatedSubject = new PartySizeCalculatedSubject();
            // Before the spawn behaviour's resizer, which would otherwise lift a lord's limit to whatever he
            // leads and let every refill from the bank raise it again.
            _partySizeCalculatedSubject.Register(_invasionCampaignBehaviour.InvasionLordPartySizeObserver);
            _spawnBehaviour = new SpawnBehaviour(_spawnGroupService, _spawnDao, _dynamicSpawnData, _saveInitialiser, _devestationMetricData, _configLoader, _messageBoxService, _dailyLogger, _modDebug, _partySizeCalculatedSubject,
                _invasionCampaignBehaviour.HasPersistentInvasion, _invasionCampaignBehaviour.IsInvasionUnderway);
            _safePassageBehaviour = new SafePassageBehaviour(_spawnDao);
            _spawnCheats = new SpawnCheats(_spawnGroupService, _spawnDao);
            _startupLogReport = new StartupLogReport(_subModService);
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
            _startupLogReport.Write();
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
                starter.AddBehavior(_invasionCapitalRepository);
                starter.AddBehavior(_invasionClanRepository);
                starter.AddBehavior(_dynamicSpawnData);
                starter.AddBehavior(_invasionCampaignBehaviour);
                starter.AddBehavior(_customSpawnsDiplomacyBehaviour);
                starter.AddBehavior(_customSpawnsAiBehaviour);
                starter.AddBehavior(_accompanyingPartySiegeBehaviour);
                starter.AddBehavior(_aiDecisionLoggerBehaviour);

                starter.AddModel(_customSpawnsDiplomacyModel);
                starter.AddModel(new InvasionAllianceModel(starter.GetModel<AllianceModel>(),
                    _invasionCampaignBehaviour.BannerlordInvasionPacts));
                starter.AddModel(new InvasionTradeAgreementModel(starter.GetModel<TradeAgreementModel>(),
                    _invasionCampaignBehaviour.BannerlordInvasionPacts));
                starter.AddModel(new CustomSpawnsPartySizeLimitModel(starter.GetModel<PartySizeLimitModel>(),
                    _partySizeCalculatedSubject));
                starter.AddModel(new InvasionArmyManagementModel(
                    starter.GetModel<ArmyManagementCalculationModel>(), _invasionCampaignBehaviour,
                    _invasionCampaignBehaviour, new ClanInfluenceLift(),
                    _invasionCampaignBehaviour.InvasionGarrisonArmyLeaders));
                starter.AddModel(new InvasionPartyDesertionModel(starter.GetModel<PartyDesertionModel>(),
                    _invasionCampaignBehaviour, new InvasionGarrisonDesertion(),
                    _invasionCampaignBehaviour.InvasionGarrisonArmyLeaders));
                starter.AddModel(new InvasionSettlementFoodModel(starter.GetModel<SettlementFoodModel>(),
                    _invasionCampaignBehaviour, new InvasionBankRations()));
                starter.AddModel(new InvasionMobilePartyAIModel(starter.GetModel<MobilePartyAIModel>(),
                    _invasionCampaignBehaviour.BannerlordInvasionAlliedBattles));
            }
            else
            {
                starter.AddBehavior(new RemoverBehaviour());
            }
        }

    }
}
