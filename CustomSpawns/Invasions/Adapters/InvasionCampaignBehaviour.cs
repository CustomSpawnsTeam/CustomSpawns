using System;
using System.Collections.Generic;
using System.Linq;
using CustomSpawns.AI;
using CustomSpawns.AI.Adapters;
using CustomSpawns.AI.Offensives;
using CustomSpawns.Data.Dao;
using CustomSpawns.Invasions.CapitalObjectives;
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
        private readonly InvaderHeroCreator _invaderHeroes;
        private readonly BannerlordInvasionConfigurations _bannerlordInvasionConfigurations;
        private readonly BannerlordInvasionWarbands _bannerlordInvasionWarbands = new();
        private readonly BannerlordInvasionGarrisons _bannerlordInvasionGarrisons = new();
        private readonly InvasionTroopBank _invasionTroopBank = new();
        private readonly InvasionCapitalObjectiveLogPolicy _invasionCapitalObjectiveLogPolicy = new();
        private readonly BannerlordInvasionResupplyOrders _bannerlordInvasionResupplyOrders;
        private InvasionCaptureCorrelation _invasionCaptureCorrelation;
        private InvasionReinforcementArrivals _invasionReinforcementArrivals;
        private InvasionOccupations _invasionOccupations;
        private InvasionKingdomRetirement _invasionKingdomRetirement;
        private InvasionSieges _invasionSieges;
        private InvasionMembership _invasionMembership;
        private InvasionLordWarbands _invasionLordWarbands;
        private InvasionFoodConvoys _invasionFoodConvoys;
        private IInvasionAssaultConfigurationProvider? _invasionAssaultConfigurationProvider;
        private BannerlordInvasionCapitalObjectiveWorld? _invasionCapitalObjectiveWorld;
        private InvasionCapitalObjectiveCoordinator? _invasionCapitalObjectiveCoordinator;
        private InvasionResupply _invasionResupply;
        private bool _initialized;
        private bool _reconcileCapitalObjectivesOnFirstCampaignTick;
        private Hero? _lordOfRemovedInvasionParty;
        public InvasionState State { get; private set; } = new();

        public InvasionCampaignBehaviour(SpawnDao spawns, DynamicSpawnData parties,
            SaveInitialiser initialiser, IInvasionCapitalRepository invasionCapitals,
            IInvasionClanRepository invasionClans)
        {
            _spawnDao = spawns;
            _dynamicSpawnData = parties;
            _invasionCapitals = invasionCapitals;
            _invasionClans = invasionClans;
            _invaderHeroes = new InvaderHeroCreator(invasionClans);
            _bannerlordInvasionConfigurations = new BannerlordInvasionConfigurations(spawns);
            _bannerlordInvasionResupplyOrders = new BannerlordInvasionResupplyOrders(
                _bannerlordInvasionWarbands);
            _invasionResupply = NewResupply();
            _invasionCaptureCorrelation = new InvasionCaptureCorrelation(State);
            _invasionReinforcementArrivals = NewArrivals();
            _invasionOccupations = NewOccupations();
            _invasionKingdomRetirement = new InvasionKingdomRetirement(State);
            _invasionSieges = new InvasionSieges(State, _invasionCaptureCorrelation, _spawnDao);
            _invasionMembership = new InvasionMembership(State, _spawnDao, _dynamicSpawnData,
                _invasionCaptureCorrelation);
            _invasionLordWarbands = NewLordWarbands();
            _invasionFoodConvoys = new InvasionFoodConvoys(State, _spawnDao);

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
            CampaignEvents.AiHourlyTickEvent.AddNonSerializedListener(this, AddResupplyScore);
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, ScheduleReinforcements);
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, UnloadFoodConvoys);
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, UpdateCapitalObjectivesDaily);
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

            if (!dataStore.IsLoading)
            {
                return;
            }

            State = saved?.ToState() ?? new InvasionState();
            _invasionResupply = NewResupply();
            _invasionResupply.Restore(returningParties ?? new List<string>());
            _invasionCaptureCorrelation = new InvasionCaptureCorrelation(State);
            _invasionReinforcementArrivals = NewArrivals();
            _invasionOccupations = NewOccupations();
            _invasionKingdomRetirement = new InvasionKingdomRetirement(State);
            _invasionSieges = new InvasionSieges(State, _invasionCaptureCorrelation, _spawnDao);
            _invasionMembership = new InvasionMembership(State, _spawnDao, _dynamicSpawnData,
                _invasionCaptureCorrelation);
            _invasionLordWarbands = NewLordWarbands();
            _invasionFoodConvoys = new InvasionFoodConvoys(State, _spawnDao);
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
            BannerlordInvasionLords bannerlordInvasionLords = new(_invaderHeroes);
            InvasionWaveBanking invasionWaveBanking = new(_bannerlordInvasionConfigurations,
                _bannerlordInvasionGarrisons, _bannerlordInvasionGarrisons,
                new InvasionLordLevy(bannerlordInvasionLords, _invasionTroopBank),
                new InvasionWaveFunding(new BannerlordInvasionTreasury(), new BannerlordInvasionCommissariat(),
                    new InvasionWarChest()));

            return new InvasionReinforcementArrivals(State, _spawnDao, _dynamicSpawnData, _invasionSupplyBases,
                new InvasionTroopBanking(invasionWaveBanking), bannerlordInvasionLords);
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

            if (!_reconcileCapitalObjectivesOnFirstCampaignTick)
            {
                return;
            }

            _reconcileCapitalObjectivesOnFirstCampaignTick = false;
            ReconcileCapitalObjectives("load");
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
            _lordOfRemovedInvasionParty = InvasionLordRemoval.TakeLordOfRemovedInvasionParty(State,
                leader?.Clan?.StringId, party.StringId, leader, () => _invasionMembership.Remove(party));
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

        // Capture and flight are the two ways an invasion lord outlives its party.
        private void OnHeroPrisonerTaken(PartyBase capturer, Hero prisoner) => RemoveDefeatedLord(prisoner);

        private void OnCharacterBecameFugitive(Hero hero, bool showNotification) => RemoveDefeatedLord(hero);

        // The kept lord authorises one removal decision and is spent by it: a party removal that
        // dispatches neither capture nor flight would otherwise leave it standing for the rest of the
        // session, ready to answer for a defeat years later that has nothing to do with an invasion.
        private void RemoveDefeatedLord(Hero hero)
        {
            Initialize();
            bool lostItsInvasionParty = hero == _lordOfRemovedInvasionParty;
            _lordOfRemovedInvasionParty = null;
            InvasionLordRemoval.RemoveDefeated(State, hero, lostItsInvasionParty);
        }

        private void OnHourlyTick()
        {
            Initialize();

            // A kept lord is consumed inside the very destroy that kept it, so one surviving into this
            // tick was kept by a destroy that announced neither capture nor flight -- a retreat-ended
            // battle, or a disband into a settlement -- and has no removal decision left to authorise.
            _lordOfRemovedInvasionParty = null;

            _invasionMembership.Reconcile();
            _invasionKingdomRetirement.RetireSpent();
            _invasionOccupations.ApplyReady();
            _invasionOccupations.KeepCrowns();
            UpdateCapitalObjectivesHourly();
        }

        private void AddResupplyScore(MobileParty party, PartyThinkParams thinkParams)
        {
            Initialize();
            _bannerlordInvasionResupplyOrders.AddBehaviorScore(party, thinkParams, _invasionResupply);
        }

        // The party itself no longer says which invasion it serves, so the spawn it came from is asked instead.
        public bool HasPersistentInvasion(CsPartyData partyData) => State.HasInvasionOf(partyData.OwningSpawnTemplateId);

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

            ReconcileCapitalObjectives("owner_change");
        }

        bool IPartySteeringQuery.RequiresCustomSpawnsSteering(MobileParty party)
        {
            Initialize();

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
                Kingdom? kingdom = Clan.FindFirst(clan => clan.StringId == invasion.ClanId)?.Kingdom;
                if (kingdom?.StringId != kingdomId || objective?.CommanderPartyId == null ||
                    objective.TargetSettlementId == null)
                {
                    continue;
                }

                return new InvasionCapitalObjectiveDirective(kingdomId,
                    objective.CommanderPartyId, objective.TargetSettlementId,
                    objective.Kind, objective.Phase);
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
                new BannerlordCapitalObjectiveArmy());
            var forceRatioRules = new ForceRatioRules();
            _invasionCapitalObjectiveCoordinator = new InvasionCapitalObjectiveCoordinator(
                _invasionAssaultConfigurationProvider, _invasionCapitalObjectiveWorld,
                new CapitalObjectiveStrengthRules(forceRatioRules),
                new CapitalObjectiveCommanderSelector(), new SiegeTargetSelector(forceRatioRules));
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
            Kingdom? kingdom = Clan.FindFirst(clan => clan.StringId == invasion.ClanId)?.Kingdom;
            InvasionAssaultRequirements? requirements =
                _invasionAssaultConfigurationProvider?.Find(invasion.SpawnTemplateId);
            InvasionCapitalObjectiveDiagnosticsSource diagnosticsSource =
                _invasionCapitalObjectiveLogPolicy.SelectDiagnostics(update.Evaluation != null,
                    current != null, previousCommanderPartyId, current?.CommanderPartyId);
            string diagnostics = FindDiagnostics(invasion, previousDiagnostics, update,
                diagnosticsSource);
            Logger.Info("Invasion capital objective: cadence=" + cadence +
                        " decision=" + update.Decision +
                        " spawn=" + invasion.SpawnTemplateId +
                        " kingdom=" + kingdom?.StringId +
                        " previous=" + previous +
                        " current=" + Describe(current) +
                        " target=" + update.TargetSettlementId +
                        " target_ratio=" + requirements?.MinimumTargetRatio +
                        " roaming_ratio=" + requirements?.MinimumRoamingRatio +
                        " evaluation=" + Describe(update.Evaluation) +
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

        private static string Describe(InvasionCapitalObjective? objective) => objective == null
            ? "none"
            : objective.Kind + "/" + objective.Phase + "/" + objective.DormantReason +
              "/commander=" + objective.CommanderPartyId +
              "/gather_deadline=" + objective.GatherDeadlineDays +
              "/attack_deadline=" + objective.AttackDeadlineDays;

        // Its own clock, so a change to how often the invasion is reinforced leaves its supplies alone.
        private void UnloadFoodConvoys()
        {
            Initialize();
            _invasionFoodConvoys.UnloadDue();
        }
    }
}
