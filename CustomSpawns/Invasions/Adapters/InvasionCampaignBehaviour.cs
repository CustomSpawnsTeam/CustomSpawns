using System;
using System.Collections.Generic;
using System.Linq;
using CustomSpawns.AI;
using CustomSpawns.AI.Adapters;
using CustomSpawns.AI.Offensives;
using CustomSpawns.Data.Dao;
using CustomSpawns.Invasions.Alliances;
using CustomSpawns.Invasions.Alliances.Adapters;
using CustomSpawns.Invasions.Bank;
using CustomSpawns.Invasions.Bank.Adapters;
using CustomSpawns.Invasions.CapitalObjectives;
using CustomSpawns.Invasions.CapitalObjectives.Adapters;
using CustomSpawns.Invasions.Coalitions;
using CustomSpawns.Invasions.Coalitions.Adapters;
using CustomSpawns.Invasions.Conquest;
using CustomSpawns.Invasions.Conquest.Adapters;
using CustomSpawns.Invasions.GarrisonArmies;
using CustomSpawns.Invasions.GarrisonArmies.Adapters;
using CustomSpawns.Invasions.Lines;
using CustomSpawns.Invasions.Lines.Adapters;
using CustomSpawns.Invasions.Lords;
using CustomSpawns.Invasions.Lords.Adapters;
using CustomSpawns.Invasions.PlayerSparing;
using CustomSpawns.Invasions.PlayerSparing.Adapters;
using CustomSpawns.Invasions.Reinforcements;
using CustomSpawns.Invasions.Reinforcements.Adapters;
using CustomSpawns.Spawn;
using CustomSpawns.UtilityBehaviours;
using NLog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace CustomSpawns.Invasions.Adapters
{
    public sealed class InvasionCampaignBehaviour : CampaignBehaviorBase, IPartySteeringQuery,
        IInvasionLandingQuery, IInvaderClanQuery, IInvasionCapitalObjectiveQuery
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly SpawnDao _spawnDao;
        private readonly DynamicSpawnData _dynamicSpawnData;
        private readonly InvasionSupplyBases _invasionSupplyBases = new();
        private readonly IInvasionCapitalRepository _invasionCapitals;
        private readonly IInvasionClanRepository _invasionClans;
        private readonly InvasionGarrisonArmyLords _invasionGarrisonArmyLords = new();
        private readonly InvaderHeroCreator _invaderHeroes;
        private readonly BannerlordInvasionConfigurations _bannerlordInvasionConfigurations;
        private readonly BannerlordInvasionWarbands _bannerlordInvasionWarbands;
        private readonly BannerlordInvasionGarrisons _bannerlordInvasionGarrisons;
        private readonly InvasionTroopBank _invasionTroopBank = new();
        private readonly InvasionLossDiagnostics _invasionLossDiagnostics;
        private readonly InvasionCapitalObjectiveLogPolicy _invasionCapitalObjectiveLogPolicy = new();
        private readonly InvasionCapitalObjectiveSteeringPolicy _invasionCapitalObjectiveSteeringPolicy =
            new();
        private readonly BannerlordInvasionResupplyOrders _bannerlordInvasionResupplyOrders;
        private readonly BannerlordInvasionFiefGarrisons _bannerlordInvasionFiefGarrisons;
        private InvasionCaptureCorrelation _invasionCaptureCorrelation;
        private InvasionReinforcementArrivals _invasionReinforcementArrivals;
        private InvasionOccupations _invasionOccupations;
        private InvasionKingdomRetirement _invasionKingdomRetirement;
        private InvasionSieges _invasionSieges;
        private InvasionMembership _invasionMembership;
        private InvasionLordWarbands _invasionLordWarbands;
        private InvasionLordRemoval _invasionLordRemoval;
        private IInvasionAssaultConfigurationProvider? _invasionAssaultConfigurationProvider;
        private BannerlordInvasionCapitalObjectiveWorld? _invasionCapitalObjectiveWorld;
        private InvasionCapitalObjectiveCoordinator? _invasionCapitalObjectiveCoordinator;
        private InvasionResupply _invasionResupply;
        private bool _initialized;
        private bool _reconcileCapitalObjectivesOnFirstCampaignTick;
        private Hero? _lordOfRemovedInvasionParty;
        public InvasionState State { get; private set; } = new();

        private readonly BannerlordInvasionConquestWars _bannerlordInvasionConquestWars;
        private readonly BannerlordInvasionLineRefills _bannerlordInvasionLineRefills;
        private readonly BannerlordInvasionTargetSieges _bannerlordInvasionTargetSieges;
        private readonly BannerlordInvasionAlliedSieges _bannerlordInvasionAlliedSieges;
        private readonly BannerlordInvasionCoalitionSieges _bannerlordInvasionCoalitionSieges;
        private readonly BannerlordInvasionCoalitionHunts _bannerlordInvasionCoalitionHunts;
        private readonly BannerlordInvasionCallsToWar _bannerlordInvasionCallsToWar;
        private readonly BannerlordInvasionLinePeace _bannerlordInvasionLinePeace;
        private readonly InvasionCoalitionAlliances _invasionCoalitionAlliances;
        private readonly InvasionCoalitionWars _invasionCoalitionWars;
        private readonly InvasionCoalitionPlayerChoice _invasionCoalitionPlayerChoice = new();
        private readonly InvasionStandingCoalitions _invasionStandingCoalitions = new();
        private readonly BannerlordInvasionCoalitions _bannerlordInvasionCoalitions;
        private readonly InvasionConquestSaveStore _invasionConquestSaveStore;
        private readonly InvasionLineClans _invasionLineClans = new();
        private readonly InvasionBeatenKingdoms _invasionBeatenKingdoms = new();
        private readonly InvasionCloneDiplomacy _invasionCloneDiplomacy;

        // Whose wars each invasion fights and which kingdom it conquers; the diplomacy rules ask it too.
        public BannerlordInvasionConquestStances BannerlordInvasionConquestStances { get; }

        // Lets the invasion's lords lead more men than their clan's tier allows them.
        public InvasionLordPartySizeObserver InvasionLordPartySizeObserver { get; }

        // Leads part of each held capital's garrison out against the conquered kingdom now and then.
        public InvasionGarrisonArmyCampaign InvasionGarrisonArmyCampaign { get; }

        // Which lords lead a garrison army, so the models spare their parties and armies.
        public IInvasionGarrisonArmyLeaders InvasionGarrisonArmyLeaders => _invasionGarrisonArmyLords;

        // Which kingdoms may ally or trade with an invasion's realm; the alliance and trade models ask it.
        public BannerlordInvasionPacts BannerlordInvasionPacts { get; }

        // Which battles nearby a lord of a coalition, or of a line, joins beside its allies; the party AI model asks it.
        public BannerlordInvasionAlliedBattles BannerlordInvasionAlliedBattles { get; }

        // Which settlements an invasion spares on its first landing; the siege target policy asks it.
        public IInvasionLandingSparing InvasionLandingSparing { get; }

        public InvasionCampaignBehaviour(SpawnDao spawns, DynamicSpawnData parties,
            SaveInitialiser initialiser, IInvasionCapitalRepository invasionCapitals,
            IInvasionClanRepository invasionClans, SpawnGroupService spawnGroupService,
            IDictionary<string, Data.Model.Diplomacy> diplomacy)
        {
            _spawnDao = spawns;
            _invasionLossDiagnostics = new InvasionLossDiagnostics(() => State);
            _dynamicSpawnData = parties;
            _invasionCapitals = invasionCapitals;
            _invasionClans = invasionClans;
            _invaderHeroes = new InvaderHeroCreator(invasionClans, _invasionGarrisonArmyLords);
            _bannerlordInvasionConfigurations = new BannerlordInvasionConfigurations(spawns);
            _bannerlordInvasionGarrisons = new BannerlordInvasionGarrisons(_invasionTroopBank);
            _bannerlordInvasionWarbands = new BannerlordInvasionWarbands(_invasionTroopBank);
            _bannerlordInvasionResupplyOrders = new BannerlordInvasionResupplyOrders(
                _bannerlordInvasionWarbands);
            _bannerlordInvasionFiefGarrisons = new BannerlordInvasionFiefGarrisons(() => State,
                _bannerlordInvasionConfigurations, new InvasionConsolidation(), _invasionTroopBank);
            InvasionConquests invasionConquests = new();
            InvasionConquestFronts invasionConquestFronts = new();
            BannerlordInvasionRealms bannerlordInvasionRealms = new(() => State);
            BannerlordInvasionLaunches bannerlordInvasionLaunches = new(spawnGroupService,
                settlement => _invasionSupplyBases.GetFacts(settlement).IsCoastalTown);
            BannerlordInvasionLines bannerlordInvasionLines = new(bannerlordInvasionRealms, invasionConquests,
                invasionConquestFronts);
            BannerlordInvasionCoalitions bannerlordInvasionCoalitions = new(() => State,
                _bannerlordInvasionConfigurations, bannerlordInvasionRealms, invasionConquests,
                invasionConquestFronts, bannerlordInvasionLines, _invasionCoalitionPlayerChoice,
                _invasionStandingCoalitions);
            _bannerlordInvasionCoalitions = bannerlordInvasionCoalitions;
            BannerlordInvasionConquestStances = new BannerlordInvasionConquestStances(() => State,
                _bannerlordInvasionConfigurations, bannerlordInvasionRealms, invasionConquests,
                invasionConquestFronts, bannerlordInvasionLines, bannerlordInvasionCoalitions);
            BannerlordInvasionPacts = new BannerlordInvasionPacts(bannerlordInvasionRealms, invasionConquests);
            BannerlordInvasionAlliances bannerlordInvasionAlliances = new(bannerlordInvasionRealms);
            _invasionCoalitionAlliances = new InvasionCoalitionAlliances(bannerlordInvasionCoalitions,
                bannerlordInvasionAlliances,
                new BannerlordInvasionCoalitionInvitations(bannerlordInvasionCoalitions, bannerlordInvasionRealms),
                _invasionCoalitionPlayerChoice);
            _invasionCoalitionWars = new InvasionCoalitionWars(bannerlordInvasionCoalitions,
                new BannerlordInvasionKingdomWars());
            BannerlordInvasionCoalitionFronts bannerlordInvasionCoalitionFronts = new(() => State,
                bannerlordInvasionCoalitions, bannerlordInvasionRealms, new InvasionCapitalIsolation());
            _bannerlordInvasionAlliedSieges = new BannerlordInvasionAlliedSieges(bannerlordInvasionRealms,
                invasionConquests, bannerlordInvasionCoalitionFronts);
            InvasionCoalitionWarScores invasionCoalitionWarScores = new();
            BannerlordInvasionSiegeOffers bannerlordInvasionSiegeOffers = new();
            _bannerlordInvasionCoalitionSieges = new BannerlordInvasionCoalitionSieges(
                bannerlordInvasionCoalitionFronts, _bannerlordInvasionAlliedSieges,
                new BannerlordInvasionCoalitionCapitalSieges(_bannerlordInvasionAlliedSieges,
                    new BannerlordInvasionCoalitionForces(bannerlordInvasionCoalitionFronts),
                    bannerlordInvasionSiegeOffers, invasionCoalitionWarScores),
                bannerlordInvasionSiegeOffers, invasionCoalitionWarScores);
            _bannerlordInvasionCoalitionHunts = new BannerlordInvasionCoalitionHunts(
                bannerlordInvasionCoalitionFronts, invasionCoalitionWarScores);
            BannerlordInvasionAlliedBattles = new BannerlordInvasionAlliedBattles(bannerlordInvasionRealms,
                new InvasionAlliedBattles());
            _bannerlordInvasionCallsToWar = new BannerlordInvasionCallsToWar(bannerlordInvasionRealms,
                invasionConquests, new InvasionRebellions(invasionConquests));
            _bannerlordInvasionLinePeace = new BannerlordInvasionLinePeace(bannerlordInvasionRealms,
                invasionConquests, BannerlordInvasionConquestStances);
            _bannerlordInvasionTargetSieges = new BannerlordInvasionTargetSieges(() => State,
                _bannerlordInvasionConfigurations, BannerlordInvasionConquestStances,
                new InvasionTargetSiegeScores(), _bannerlordInvasionAlliedSieges);
            _invasionConquestSaveStore = new InvasionConquestSaveStore(invasionConquests);
            BannerlordInvasionNeighbours bannerlordInvasionNeighbours = new(bannerlordInvasionLaunches,
                bannerlordInvasionRealms, invasionConquests);
            _invasionCloneDiplomacy = new InvasionCloneDiplomacy(diplomacy);
            BannerlordInvasionFollowerLaunches bannerlordInvasionFollowerLaunches = new(() => State,
                _bannerlordInvasionConfigurations, bannerlordInvasionLaunches,
                new BannerlordInvasionClones(bannerlordInvasionLaunches, new BannerlordInvasionClanFounding(),
                    invasionClans, _invasionLineClans, _invasionCloneDiplomacy),
                bannerlordInvasionRealms, invasionConquests, new InvasionLandingSiteChoice(),
                new InvasionFollowerChoice());
            InvasionPlayerSparing invasionPlayerSparing = new(new BannerlordInvasionPlayerRealm(),
                _invasionBeatenKingdoms);
            InvasionLandingSparing = new BannerlordInvasionLandingSparing(() => State,
                _bannerlordInvasionConfigurations, invasionPlayerSparing);
            _bannerlordInvasionConquestWars = new BannerlordInvasionConquestWars(() => State,
                _bannerlordInvasionConfigurations, bannerlordInvasionRealms,
                bannerlordInvasionAlliances,
                new BannerlordInvasionRetargets(bannerlordInvasionNeighbours, invasionConquests,
                    new InvasionTargetChoice(), _bannerlordInvasionConfigurations, invasionPlayerSparing),
                new BannerlordInvasionFrontOpenings(_bannerlordInvasionFiefGarrisons, bannerlordInvasionNeighbours,
                    bannerlordInvasionRealms, invasionConquestFronts),
                BannerlordInvasionConquestStances, invasionConquests, _invasionLineClans, _invasionBeatenKingdoms,
                invasionPlayerSparing);
            _bannerlordInvasionLineRefills = new BannerlordInvasionLineRefills(() => State,
                _bannerlordInvasionConfigurations, bannerlordInvasionFollowerLaunches, invasionConquests,
                invasionClans, invasionCapitals, _invasionBeatenKingdoms);
            InvasionLordPartySizeObserver = new InvasionLordPartySizeObserver(
                new InvasionLordPartySize(() => State, _bannerlordInvasionConfigurations), _invasionGarrisonArmyLords);
            InvasionGarrisonArmyCampaign = new InvasionGarrisonArmyCampaign(() => State, spawns, _invasionTroopBank,
                BannerlordInvasionConquestStances, _invaderHeroes, _invasionGarrisonArmyLords);
            _invasionResupply = NewResupply();
            _invasionCaptureCorrelation = new InvasionCaptureCorrelation(State);
            _invasionReinforcementArrivals = NewArrivals();
            _invasionOccupations = NewOccupations();
            _invasionKingdomRetirement = new InvasionKingdomRetirement(State, new InvasionRealmCollapse(), new InvasionMercenaryClans());
            _invasionSieges = new InvasionSieges(State, _invasionCaptureCorrelation, _spawnDao);
            _invasionMembership = new InvasionMembership(State, _spawnDao, _dynamicSpawnData,
                _invasionCaptureCorrelation);
            _invasionLordWarbands = NewLordWarbands();
            _invasionLordRemoval = new InvasionLordRemoval(State, _invaderHeroes);

            // DynamicSpawnData registers its discovery callback first; scheduling/AI are composed later.
            initialiser.RunCallbackOnFirstCampaignTick(InitializeOnFirstCampaignTick);
            parties.PartyRegistered += RegisterParty;
            parties.PartyRemoved += RemoveParty;
        }

        public override void RegisterEvents()
        {
            CampaignEvents.MapEventStarted.AddNonSerializedListener(this, OnMapEventStarted);
            CampaignEvents.MapEventEnded.AddNonSerializedListener(this, OnMapEventEnded);
            CampaignEvents.OnSiegeAftermathAppliedEvent.AddNonSerializedListener(this, OnAftermath);
            CampaignEvents.OnSettlementOwnerChangedEvent.AddNonSerializedListener(this, OnOwnerChanged);
            CampaignEvents.CanKingdomBeDiscontinuedEvent.AddNonSerializedListener(this, CanKingdomBeDiscontinued);
            CampaignEvents.MobilePartyCreated.AddNonSerializedListener(this, OnMobilePartyCreated);
            CampaignEvents.HeroPrisonerTaken.AddNonSerializedListener(this, OnHeroPrisonerTaken);
            CampaignEvents.CharacterBecameFugitiveEvent.AddNonSerializedListener(this, OnCharacterBecameFugitive);
            CampaignEvents.SettlementEntered.AddNonSerializedListener(this, OnSettlementEntered);
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, OnHourlyTick);
            CampaignEvents.TickEvent.AddNonSerializedListener(this, RemoveDefeatedLords);
            CampaignEvents.AiHourlyTickEvent.AddNonSerializedListener(this, AddResupplyScore);
            CampaignEvents.AiHourlyTickEvent.AddNonSerializedListener(this, HoldSieges);
            CampaignEvents.AiHourlyTickEvent.AddNonSerializedListener(this, PreferTargetSieges);
            CampaignEvents.AiHourlyTickEvent.AddNonSerializedListener(this, PreferLineSieges);
            CampaignEvents.AiHourlyTickEvent.AddNonSerializedListener(this, HuntLineParties);
            CampaignEvents.HourlyTickPartyEvent.AddNonSerializedListener(this, JoinAlliedSieges);
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, ScheduleReinforcements);
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, UpdateCapitalObjectivesDaily);
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, ConsolidateDaily);
            CampaignEvents.WarDeclared.AddNonSerializedListener(this, OnWarDeclared);
            CampaignEvents.MakePeace.AddNonSerializedListener(this, OnMakePeace);
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, RaiseGarrisonArmies);
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, EndForbiddenPacts);
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, KeepCoalitionAlliances);
            CampaignEvents.ArmyDispersed.AddNonSerializedListener(this, OnArmyDispersed);
            _invasionLossDiagnostics.RegisterEvents();
        }

        public void ConfigureCapitalObjectives(
            IInvasionAssaultConfigurationProvider invasionAssaultConfigurationProvider)
        {
            _invasionAssaultConfigurationProvider = invasionAssaultConfigurationProvider ??
                                                     throw new ArgumentNullException(
                                                         nameof(invasionAssaultConfigurationProvider));
            RebuildCapitalObjectiveCoordinator();
        }

        public override void SyncData(IDataStore dataStore)
        {
            InvasionStateSaveData? saved = dataStore.IsSaving ? InvasionStateSaveData.FromState(State) : null;

            if (saved != null)
            {
                saved.OwnedCaptureEvidence = _invasionCaptureCorrelation.ExportOwnedResults().Select(result => new InvasionCaptureEvidenceSaveData
                {
                    CaptureId = result.CaptureId, PreviousOwnerClanId = result.PreviousOwnerClanId,
                    ExpectedOwnerClanId = result.ExpectedOwnerClanId, Capturer = result.Capturer as Hero,
                    Participants = result.Participants.Cast<MobileParty>().ToList()
                }).ToList();
            }

            dataStore.SyncData("_invasionState", ref saved);
            List<string>? returningParties = dataStore.IsSaving
                ? _invasionResupply.FindReturningParties().ToList()
                : null;
            dataStore.SyncData("_invasionResupplyParties", ref returningParties);
            Dictionary<string, string>? conquests = dataStore.IsSaving
                ? _invasionConquestSaveStore.Export()
                : null;
            dataStore.SyncData("_invasionConquests", ref conquests);
            Dictionary<string, string>? garrisonArmies = dataStore.IsSaving
                ? InvasionGarrisonArmyCampaign.Export()
                : null;
            dataStore.SyncData("_invasionGarrisonArmies", ref garrisonArmies);
            Dictionary<string, string>? garrisonArmyLords = dataStore.IsSaving
                ? InvasionGarrisonArmyCampaign.ExportLords()
                : null;
            dataStore.SyncData("_invasionGarrisonArmyLords", ref garrisonArmyLords);
            Dictionary<string, string>? lineClans = dataStore.IsSaving ? _invasionLineClans.Export() : null;
            dataStore.SyncData("_invasionLineClans", ref lineClans);
            Dictionary<string, string>? beatenKingdoms = dataStore.IsSaving
                ? _invasionBeatenKingdoms.Export()
                : null;
            dataStore.SyncData("_invasionBeatenKingdoms", ref beatenKingdoms);
            string? coalitionAnswer = dataStore.IsSaving ? _invasionCoalitionPlayerChoice.Export() : null;
            dataStore.SyncData("_invasionCoalitionAnswer", ref coalitionAnswer);
            List<string>? coalitionLines = dataStore.IsSaving ? _invasionStandingCoalitions.Export() : null;
            dataStore.SyncData("_invasionCoalitionLines", ref coalitionLines);

            if (!dataStore.IsLoading)
            {
                return;
            }

            State = saved?.ToState() ?? new InvasionState();
            _invasionConquestSaveStore.Restore(conquests);
            _invasionLineClans.Restore(lineClans);
            _invasionBeatenKingdoms.Restore(beatenKingdoms);
            _invasionCoalitionPlayerChoice.Restore(coalitionAnswer);
            _invasionStandingCoalitions.Restore(coalitionLines);
            _invasionCloneDiplomacy.InheritAll(_invasionLineClans);
            InvasionGarrisonArmyCampaign.Restore(garrisonArmies);
            InvasionGarrisonArmyCampaign.RestoreLords(garrisonArmyLords);
            _invasionResupply = NewResupply();
            _invasionResupply.Restore(returningParties ?? new List<string>());
            _invasionCaptureCorrelation = new InvasionCaptureCorrelation(State);
            _invasionReinforcementArrivals = NewArrivals();
            _invasionOccupations = NewOccupations();
            _invasionKingdomRetirement = new InvasionKingdomRetirement(State, new InvasionRealmCollapse(), new InvasionMercenaryClans());
            _invasionSieges = new InvasionSieges(State, _invasionCaptureCorrelation, _spawnDao);
            _invasionMembership = new InvasionMembership(State, _spawnDao, _dynamicSpawnData,
                _invasionCaptureCorrelation);
            _invasionLordWarbands = NewLordWarbands();
            _invasionLordRemoval = new InvasionLordRemoval(State, _invaderHeroes);
            RebuildCapitalObjectiveCoordinator();

            foreach (var evidence in saved?.OwnedCaptureEvidence ?? new List<InvasionCaptureEvidenceSaveData>())
            {
                _invasionCaptureCorrelation.RestoreOwnedResult(evidence.CaptureId, evidence.PreviousOwnerClanId,
                    evidence.ExpectedOwnerClanId, evidence.Capturer, (evidence.Participants ?? new List<MobileParty>()).Cast<object>());
            }

            _initialized = false;
            _reconcileCapitalObjectivesOnFirstCampaignTick = true;
            _invasionSupplyBases.ClearCache();
        }

        private InvasionReinforcementArrivals NewArrivals()
        {
            BannerlordInvasionLords bannerlordInvasionLords = new(_invaderHeroes, _invasionGarrisonArmyLords);
            InvasionWaveBanking invasionWaveBanking = new(_bannerlordInvasionConfigurations,
                _bannerlordInvasionGarrisons, _bannerlordInvasionGarrisons, bannerlordInvasionLords,
                new InvasionLordLevy(bannerlordInvasionLords, _invasionTroopBank),
                new InvasionWaveFunding(new BannerlordInvasionTreasury(), new BannerlordInvasionCommissariat(),
                    new InvasionWarChest()));

            return new InvasionReinforcementArrivals(State, _spawnDao, _invasionSupplyBases,
                new InvasionTroopBanking(invasionWaveBanking), bannerlordInvasionLords, BannerlordInvasionConquestStances);
        }

        private InvasionLordWarbands NewLordWarbands() =>
            new(new InvasionWarbandBank(State, _bannerlordInvasionConfigurations, _bannerlordInvasionWarbands,
                _bannerlordInvasionGarrisons, _invasionTroopBank), _bannerlordInvasionWarbands);

        private InvasionResupply NewResupply() =>
            new(State, _bannerlordInvasionConfigurations, _bannerlordInvasionGarrisons,
                _bannerlordInvasionResupplyOrders);

        private InvasionOccupations NewOccupations() =>
            new(State, _spawnDao, _dynamicSpawnData, _invasionCapitals, _invasionClans);

        private void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;
            _invasionMembership.Reconcile();
            _invasionMembership.RegisterLive();
            _invasionSieges.RebindUnconfirmed();
        }

        private void InitializeOnFirstCampaignTick()
        {
            Initialize();
            _invasionLossDiagnostics.ObserveParties("load");

            if (!_reconcileCapitalObjectivesOnFirstCampaignTick)
            {
                return;
            }

            _reconcileCapitalObjectivesOnFirstCampaignTick = false;
            ReconcileCapitalObjectives("load");
            _invasionReinforcementArrivals.LogStrength("load");
        }

        private void RegisterParty(MobileParty party, CsPartyData data) => _invasionMembership.Register(party, data);

        /**
         * Removing a party tells the ledger first and the party's lord only afterwards: vanilla destroys
         * a fleeing lord's party before it announces the flight, and that destroy can leave the invasion it
         * emptied spent or reconciled away. Whether the lord was invading is therefore answered here,
         * while its party is still a member of a invasion that is still fighting, and kept for the capture
         * or flight the same destroy is about to dispatch. The next removal decision spends it, and the
         * next hourly tick discards it if no such decision came.
         */
        private void RemoveParty(MobileParty party)
        {
            _invasionResupply.Remove(party.StringId);
            Hero? leader = party.LeaderHero;
            string? removedLordId = State.TakeLordOfRemovedInvasionParty(leader?.Clan?.StringId,
                party.StringId, leader?.StringId, () => _invasionMembership.Remove(party));
            _lordOfRemovedInvasionParty = removedLordId == null ? null : leader;
        }

        /**
         * Deliberately does not Initialize(): party creation fires for the whole map while a new game
         * is built, long before the first campaign tick, and adoption only reads state SyncData restores.
         *
         * This is also where vanilla has just handed a newly fielded lord his free warband, so it is where
         * an invasion holding its capital takes that warband back and makes him draw from the capital
         * instead. On a new game the state is still empty, so nothing is drawn from anything.
         */
        private void OnMobilePartyCreated(MobileParty party)
        {
            _invasionMembership.Adopt(party);
            _invasionLordWarbands.DrawFromGarrison(party);
        }

        /*
         * Capture and flight are the two ways an invasion lord outlives its party. A garrison army's lord is
         * ended first, so his army's end is logged as his capture and his men go home before he is removed.
         */
        private void OnHeroPrisonerTaken(PartyBase capturer, Hero prisoner)
        {
            InvasionGarrisonArmyCampaign.Review();
            RemoveDefeatedLord(prisoner);
        }

        private void OnCharacterBecameFugitive(Hero hero, bool showNotification)
        {
            InvasionGarrisonArmyCampaign.Review();
            RemoveDefeatedLord(hero);
        }

        // The kept lord authorises one removal decision and is spent by it: a party removal that
        // dispatches neither capture nor flight would otherwise leave it standing for the rest of the
        // session, ready to answer for a defeat years later that has nothing to do with an invasion.
        private void RemoveDefeatedLord(Hero hero)
        {
            Initialize();
            bool lostItsInvasionParty = hero == _lordOfRemovedInvasionParty;
            _lordOfRemovedInvasionParty = null;
            _invasionLordRemoval.Defer(hero, lostItsInvasionParty);
        }

        private void RemoveDefeatedLords(float dt)
        {
            _invasionLordRemoval?.RemoveDeferred();
        }

        private void OnHourlyTick()
        {
            Initialize();

            // A kept lord is consumed inside the very destroy that kept it, so one surviving into this
            // tick was kept by a destroy that announced neither capture nor flight -- a retreat-ended
            // battle, or a disband into a settlement -- and has no removal decision left to authorise.
            _lordOfRemovedInvasionParty = null;

            _invasionMembership.Reconcile();
            _invasionKingdomRetirement.RetireFallen();
            _invasionOccupations.ApplyReady();
            _invasionOccupations.KeepCrowns();
            UpdateCapitalObjectivesHourly();
            InvasionGarrisonArmyCampaign.Review();
        }

        private void RaiseGarrisonArmies()
        {
            Initialize();
            InvasionGarrisonArmyCampaign.UpdateDaily();
        }

        private void EndForbiddenPacts()
        {
            Initialize();
            BannerlordInvasionPacts.EndForbiddenPacts();
        }

        private void KeepCoalitionAlliances()
        {
            Initialize();
            _bannerlordInvasionCoalitions.DisbandBeatenLines();
            _invasionCoalitionAlliances.KeepAlliances();
            _invasionCoalitionWars.KeepWars();
        }

        private void OnArmyDispersed(Army army, Army.ArmyDispersionReason reason, bool isPlayersArmy) =>
            InvasionGarrisonArmyCampaign.ObserveDispersal(army, reason);

        private void AddResupplyScore(MobileParty party, PartyThinkParams thinkParams)
        {
            Initialize();
            _bannerlordInvasionResupplyOrders.AddBehaviorScore(party, thinkParams, _invasionResupply);
        }

        private void HoldSieges(MobileParty party, PartyThinkParams thinkParams)
        {
            Initialize();
            _bannerlordInvasionFiefGarrisons.HoldSieges(party, thinkParams,
                BannerlordInvasionConquestStances.FindConquestTargetKingdomId);
        }

        private void PreferTargetSieges(MobileParty party, PartyThinkParams thinkParams)
        {
            Initialize();
            _bannerlordInvasionTargetSieges.PreferTargetSieges(party, thinkParams);
        }

        private void PreferLineSieges(MobileParty party, PartyThinkParams thinkParams)
        {
            Initialize();
            _bannerlordInvasionCoalitionSieges.PreferLineSieges(party, thinkParams);
        }

        private void HuntLineParties(MobileParty party, PartyThinkParams thinkParams)
        {
            Initialize();
            _bannerlordInvasionCoalitionHunts.HuntLineParties(party, thinkParams);
        }

        private void JoinAlliedSieges(MobileParty party)
        {
            Initialize();
            _bannerlordInvasionAlliedSieges.JoinAlliedSiege(party);
        }

        private void ConsolidateDaily()
        {
            Initialize();
            _bannerlordInvasionConquestWars.UpdateDaily();
            _bannerlordInvasionLineRefills.CheckDaily();
            _bannerlordInvasionFiefGarrisons.TopUpDaily();
        }

        private void OnWarDeclared(IFaction first, IFaction second, DeclareWarAction.DeclareWarDetail detail)
        {
            Initialize();
            BannerlordInvasionConquestStances.ObserveWar(first, second);
            _bannerlordInvasionCallsToWar.CallAlliesToWar(first, second, detail);
        }

        private void OnMakePeace(IFaction first, IFaction second, MakePeaceAction.MakePeaceDetail detail)
        {
            Initialize();
            _bannerlordInvasionLinePeace.ShareLinePeace(first, second);
        }

        // The party itself no longer says which invasion it serves, so the spawn it came from is asked instead.
        public bool HasPersistentInvasion(CsPartyData partyData) => State.HasInvasionOf(partyData.OwningSpawnTemplateId);

        // A spawn whose invasion is still being fought lands no second force beside it.
        public bool IsInvasionUnderway(string spawnTemplateId) => State.HasInvasionOf(spawnTemplateId);

        Func<Settlement, bool>? IInvasionLandingQuery.GetInitialCoastalTargetFilter(MobileParty party)
        {
            Initialize();
            return _invasionReinforcementArrivals.FindInitialCoastalTargetFilter(party);
        }

        IReadOnlyList<MobileParty> IInvasionLandingQuery.GetLandingParties(MobileParty party)
        {
            Initialize();
            return _invasionMembership.FindLiveMembers(party);
        }

        bool IInvasionLandingQuery.ServesLiveInvasion(MobileParty party)
        {
            Initialize();
            Invasion? invasion = State.FindInvasionOfParty(party.StringId);
            return invasion != null && !invasion.IsSuspended &&
                   invasion.HoldsClan(party.ActualClan?.StringId);
        }

        private void OnMapEventStarted(MapEvent mapEvent, PartyBase attacker, PartyBase defender)
        {
            Initialize();
            _invasionSieges.ObserveStart(mapEvent);
        }

        private void OnMapEventEnded(MapEvent mapEvent) => _invasionSieges.ObserveEnd(mapEvent);

        private void OnAftermath(MobileParty attacker, Settlement settlement, SiegeAftermathAction.SiegeAftermath aftermath,
            Clan previousOwner, Dictionary<MobileParty, float> contributions)
        {
            Initialize();

            long? capture = _invasionSieges.ObserveAftermath(attacker, settlement, previousOwner, contributions);

            if (capture != null)
            {
                _invasionReinforcementArrivals.StartSchedule(capture.Value);
            }
        }

        private void OnOwnerChanged(Settlement settlement, bool openToClaim, Hero newOwner, Hero oldOwner,
            Hero capturer, ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail detail)
        {
            Initialize();

            long? capture = _invasionSieges.ObserveOwnerChange(settlement, newOwner, oldOwner, capturer, detail);

            if (capture != null)
            {
                _invasionReinforcementArrivals.StartSchedule(capture.Value);
            }

            _bannerlordInvasionConquestWars.ObserveConquest(newOwner?.Clan, oldOwner?.Clan);
            ReconcileCapitalObjectives("owner_change");
        }

        bool IPartySteeringQuery.RequiresCustomSpawnsSteering(MobileParty party)
        {
            Initialize();

            Invasion? invasion = State.FindFightingInvasion(party.ActualClan?.StringId);
            string? nativeArmyCommanderPartyId = party.Army?.LeaderParty?.StringId;
            if (_invasionCapitalObjectiveSteeringPolicy.IsReleasedToNative(
                    invasion?.CapitalObjective, party.StringId, nativeArmyCommanderPartyId))
            {
                return false;
            }

            // Vanilla's military AI only steers a party with a hero at its head, so a leaderless invasion
            // party handed to it would hold where it stands for the rest of the campaign.
            return party.LeaderHero == null || State.RequiresConfiguredPartySteering(party.ActualClan?.StringId);
        }

        private void CanKingdomBeDiscontinued(Kingdom kingdom, ref bool result)
        {
            Initialize();

            if (State.ProtectsKingdomOf(kingdom.Clans.Select(clan => clan.StringId)))
            {
                result = false;
            }
        }

        private void ScheduleReinforcements()
        {
            Initialize();

            _invasionMembership.Reconcile();
            _invasionReinforcementArrivals.SendDue();
            _invasionLordWarbands.RefillAtCapitals();
            _invasionReinforcementArrivals.LogStrength("daily");
            _invasionLossDiagnostics.ObserveParties("daily");
        }

        private void OnSettlementEntered(MobileParty? party, Settlement settlement, Hero? hero)
        {
            if (party == null)
            {
                return;
            }

            Initialize();
            _invasionLordWarbands.Refill(party);
        }

        bool IInvaderClanQuery.IsInvaderClan(string? clanId) => State.FindFightingInvasion(clanId) != null;

        InvasionCapitalObjectiveDirective? IInvasionCapitalObjectiveQuery.FindForKingdom(
            string kingdomId)
        {
            Initialize();

            foreach (Invasion invasion in State.Invasions)
            {
                InvasionCapitalObjective? objective = invasion.CapitalObjective;
                Kingdom? kingdom = invasion.FindLeadingClan()?.Kingdom;
                if (invasion.IsSuspended || kingdom?.StringId != kingdomId || objective?.CommanderPartyId == null ||
                    objective.TargetSettlementId == null)
                {
                    continue;
                }

                return new InvasionCapitalObjectiveDirective(kingdomId,
                    objective.CommanderPartyId, objective.TargetSettlementId,
                    objective.Kind, objective.Phase, objective.MobilizedPartyIds,
                    objective.StagingSettlementId);
            }

            return null;
        }

        private void RebuildCapitalObjectiveCoordinator()
        {
            if (_invasionAssaultConfigurationProvider == null)
            {
                _invasionCapitalObjectiveWorld = null;
                _invasionCapitalObjectiveCoordinator = null;
                return;
            }

            _invasionCapitalObjectiveWorld = new BannerlordInvasionCapitalObjectiveWorld(State,
                _invasionCapitals, new BannerlordMapPathQuery(), new InvasionCoastalTownResolver(),
                new BannerlordCapitalObjectiveArmy(new CapitalArmyStagingRules()), BannerlordInvasionConquestStances);
            var forceRatioRules = new ForceRatioRules();
            _invasionCapitalObjectiveCoordinator = new InvasionCapitalObjectiveCoordinator(
                _invasionAssaultConfigurationProvider, _invasionCapitalObjectiveWorld,
                new CapitalObjectiveStrengthRules(forceRatioRules),
                new CapitalArmyMobilizationPlanner(), new SiegeTargetSelector(forceRatioRules),
                new InvasionCapitalSuccession());
        }

        private void UpdateCapitalObjectivesHourly()
        {
            ReconcileCapitalObjectives("hourly");
        }

        private void ReconcileCapitalObjectives(string cadence)
        {
            if (_invasionCapitalObjectiveCoordinator == null)
            {
                return;
            }

            foreach (Invasion invasion in State.Invasions.ToList())
            {
                InvasionCapitalObjective? previousObjective = invasion.CapitalObjective;
                string previous = Describe(previousObjective);
                string? previousCommanderPartyId = previousObjective?.CommanderPartyId;
                string previousDiagnostics = _invasionCapitalObjectiveWorld?.Describe(invasion) ??
                                             "unavailable";
                CapitalObjectiveUpdate update = _invasionCapitalObjectiveCoordinator.UpdateHourly(invasion);
                LogCapitalObjectiveUpdate(invasion, previous, previousDiagnostics,
                    previousCommanderPartyId, update, cadence);
            }
        }

        private void UpdateCapitalObjectivesDaily()
        {
            Initialize();
            if (_invasionCapitalObjectiveCoordinator == null)
            {
                return;
            }

            foreach (Invasion invasion in State.Invasions.ToList())
            {
                InvasionCapitalObjective? previousObjective = invasion.CapitalObjective;
                string previous = Describe(previousObjective);
                string? previousCommanderPartyId = previousObjective?.CommanderPartyId;
                InvasionCapitalObjectivePhase? previousPhase = previousObjective?.Phase;
                InvasionCapitalObjectiveDormantReason? previousDormantReason =
                    previousObjective?.DormantReason;
                string previousDiagnostics = _invasionCapitalObjectiveWorld?.Describe(invasion) ??
                                             "unavailable";
                CapitalObjectiveUpdate update = _invasionCapitalObjectiveCoordinator.UpdateDaily(invasion);
                bool includeNoChange = _invasionCapitalObjectiveLogPolicy.IncludesDailyNoChange(
                    previousPhase, previousDormantReason);
                if (includeNoChange || update.Decision != CapitalObjectiveDecision.None)
                {
                    LogCapitalObjectiveUpdate(invasion, previous, previousDiagnostics,
                        previousCommanderPartyId, update, "daily", includeNoChange);
                }
            }
        }

        private void LogCapitalObjectiveUpdate(Invasion invasion,
            string previous, string previousDiagnostics, string? previousCommanderPartyId,
            CapitalObjectiveUpdate update, string cadence, bool includeNoChange = false)
        {
            if (!includeNoChange && update.Decision == CapitalObjectiveDecision.None)
            {
                return;
            }

            InvasionCapitalObjective? current = invasion.CapitalObjective;
            Kingdom? kingdom = invasion.FindLeadingClan()?.Kingdom;
            InvasionAssaultRequirements? requirements =
                _invasionAssaultConfigurationProvider?.Find(invasion.SpawnTemplateId);
            InvasionCapitalObjectiveDiagnosticsSource diagnosticsSource =
                _invasionCapitalObjectiveLogPolicy.SelectDiagnostics(update.Evaluation != null,
                    current != null, previousCommanderPartyId, current?.CommanderPartyId);
            string diagnostics = FindDiagnostics(invasion, previousDiagnostics, update,
                diagnosticsSource);
            Logger.Trace("Invasion capital objective: cadence=" + cadence +
                        " decision=" + update.Decision +
                        " spawn=" + invasion.SpawnTemplateId +
                        " kingdom=" + kingdom?.StringId +
                        " previous=" + previous +
                        " current=" + Describe(current) +
                        " target=" + update.TargetSettlementId +
                        " target_ratio=" + requirements?.MinimumTargetRatio +
                        " roaming_ratio=" + requirements?.MinimumRoamingRatio +
                        " evaluation=" + Describe(update.Evaluation) +
                        " mobilization=" + Describe(update.MobilizationResult) +
                        " diagnostics_snapshot=" + diagnosticsSource.ToString().ToLowerInvariant() +
                        " diagnostics=" + diagnostics);
        }

        private string FindDiagnostics(Invasion invasion, string previousDiagnostics,
            CapitalObjectiveUpdate update,
            InvasionCapitalObjectiveDiagnosticsSource diagnosticsSource)
        {
            if (diagnosticsSource == InvasionCapitalObjectiveDiagnosticsSource.Previous)
            {
                return previousDiagnostics;
            }

            if (diagnosticsSource == InvasionCapitalObjectiveDiagnosticsSource.Evaluation)
            {
                CapitalObjectiveEvaluation evaluation = update.Evaluation!;
                string? targetSettlementId = evaluation.TargetFacts
                    .OrderBy(facts => facts.Distance)
                    .Select(facts => facts.SettlementId)
                    .FirstOrDefault();
                return _invasionCapitalObjectiveWorld?.Describe(invasion,
                           evaluation.CommanderPartyId, targetSettlementId) ?? "unavailable";
            }

            return _invasionCapitalObjectiveWorld?.Describe(invasion) ?? "unavailable";
        }

        private static string Describe(CapitalObjectiveEvaluation? evaluation)
        {
            if (evaluation == null)
            {
                return "none";
            }

            return "commander=" + evaluation.CommanderPartyId +
                   "/assembled_strength=" + evaluation.AttackForces.AssembledFieldStrength +
                   "/targets=[" + string.Join(";", evaluation.TargetFacts.Select(facts =>
                       facts.SettlementId + ":active=" + facts.IsActive +
                       ",hostile=" + facts.IsHostile +
                       ",compatible=" + facts.HasCompatibleOrJoinableSiege +
                       ",strategic=" + facts.MatchesStrategicRequirement +
                       ",reachable=" + facts.IsReachable +
                       ",distance=" + facts.Distance +
                       ",defenders=" + facts.DefenderStrength +
                       ",nearby=" + facts.NearbyHostileStrength)) + "]";
        }

        private static string Describe(CapitalArmyMobilizationResult? result)
        {
            if (result == null)
            {
                return "none";
            }

            return "success=" + result.Succeeded +
                   "/failure=" + result.FailureReason +
                   "/staging=" + result.StagingSettlementId +
                   "/parties=[" + string.Join(";", result.PartyIds) + "]" +
                   "/deferred=[" + string.Join(";", result.DeferredParties.Select(item =>
                       item.PartyId + ":" + item.Reason)) + "]";
        }

        private static string Describe(InvasionCapitalObjective? objective) => objective == null
            ? "none"
            : objective.Kind + "/" + objective.Phase + "/" + objective.DormantReason +
              "/commander=" + objective.CommanderPartyId +
              "/gather_deadline=" + objective.GatherDeadlineDays +
              "/attack_deadline=" + objective.AttackDeadlineDays;
    }
}
