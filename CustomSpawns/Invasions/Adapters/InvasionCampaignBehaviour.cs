using System;
using System.Collections.Generic;
using System.Linq;
using CustomSpawns.AI;
using CustomSpawns.Data.Dao;
using CustomSpawns.Spawn;
using CustomSpawns.UtilityBehaviours;
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
        IInvasionLandingQuery
    {
        private readonly SpawnDao _spawnDao;
        private readonly DynamicSpawnData _dynamicSpawnData;
        private readonly SpawnGroupService _spawnGroupService;
        private readonly InvasionCoastalTownResolver _invasionCoastalTownResolver = new();
        private readonly Func<int, int> _randomIndex;
        private InvasionCaptureCorrelation _invasionCaptureCorrelation;
        private InvasionReinforcementArrivals _invasionReinforcementArrivals;
        private InvasionOccupations _invasionOccupations;
        private InvasionKingdomRetirement _invasionKingdomRetirement;
        private InvasionSieges _invasionSieges;
        private InvasionMembership _invasionMembership;
        private bool _initialized;
        public InvasionState State { get; private set; } = new();

        internal InvasionCampaignBehaviour(SpawnDao spawns, DynamicSpawnData parties, SpawnGroupService spawnGroups,
            SaveInitialiser initialiser, Func<int, int>? randomIndex = null)
        {
            _spawnDao = spawns;
            _dynamicSpawnData = parties;
            _spawnGroupService = spawnGroups;
            _randomIndex = randomIndex ?? MBRandom.RandomInt;
            _invasionCaptureCorrelation = new InvasionCaptureCorrelation(State);
            _invasionReinforcementArrivals = NewArrivals();
            _invasionOccupations = NewOccupations();
            _invasionKingdomRetirement = new InvasionKingdomRetirement(State);
            _invasionSieges = new InvasionSieges(State, _invasionCaptureCorrelation, _spawnDao);
            _invasionMembership = new InvasionMembership(State, _spawnDao, _dynamicSpawnData,
                _invasionCaptureCorrelation);

            // DynamicSpawnData registers its discovery callback first; scheduling/AI are composed later.
            initialiser.RunCallbackOnFirstCampaignTick(Initialize);
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
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, OnHourlyTick);
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, ScheduleReinforcements);
        }

        public override void SyncData(IDataStore dataStore)
        {
            InvasionSaveData? saved = dataStore.IsSaving ? InvasionSaveData.FromState(State) : null;

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

            if (!dataStore.IsLoading)
            {
                return;
            }

            State = saved?.ToState() ?? new InvasionState();
            _invasionCaptureCorrelation = new InvasionCaptureCorrelation(State);
            _invasionReinforcementArrivals = NewArrivals();
            _invasionOccupations = NewOccupations();
            _invasionKingdomRetirement = new InvasionKingdomRetirement(State);
            _invasionSieges = new InvasionSieges(State, _invasionCaptureCorrelation, _spawnDao);
            _invasionMembership = new InvasionMembership(State, _spawnDao, _dynamicSpawnData,
                _invasionCaptureCorrelation);

            foreach (var evidence in saved?.OwnedCaptureEvidence ?? new List<InvasionCaptureEvidenceSaveData>())
            {
                _invasionCaptureCorrelation.RestoreOwnedResult(evidence.CaptureId, evidence.PreviousOwnerClanId,
                    evidence.ExpectedOwnerClanId, evidence.Capturer, (evidence.Participants ?? new List<MobileParty>()).Cast<object>());
            }

            _initialized = false;
            _invasionCoastalTownResolver.ClearCache();
        }

        private InvasionReinforcementArrivals NewArrivals() =>
            new(State, _spawnDao, _dynamicSpawnData, _spawnGroupService, _invasionCoastalTownResolver,
                _randomIndex);

        private InvasionOccupations NewOccupations() =>
            new(State, _spawnDao, _dynamicSpawnData, _invasionCoastalTownResolver);

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

        private void RegisterParty(MobileParty party, CsPartyData data) => _invasionMembership.Register(party, data);

        private void RemoveParty(MobileParty party) => _invasionMembership.Remove(party);

        // Deliberately does not Initialize(): party creation fires for the whole map while a new game
        // is built, long before the first campaign tick, and adoption only reads state SyncData restores.
        private void OnMobilePartyCreated(MobileParty party) => _invasionMembership.Adopt(party);

        // Capture and flight are the two ways an invasion lord survives losing its party, and so the two
        // it would otherwise come back from.
        private void OnHeroPrisonerTaken(PartyBase capturer, Hero prisoner) => RemoveDefeatedLord(prisoner);

        private void OnCharacterBecameFugitive(Hero hero, bool showNotification) => RemoveDefeatedLord(hero);

        private void RemoveDefeatedLord(Hero hero)
        {
            Initialize();
            InvasionLordRemoval.RemoveDefeated(State, hero);
        }

        private void OnHourlyTick()
        {
            Initialize();

            _invasionMembership.Reconcile();
            _invasionKingdomRetirement.RetireSpent();
            _invasionOccupations.ApplyReady();
        }

        public bool HasPersistentInvasionGroup(CsPartyData partyData) => State.FindGroup(partyData.InvasionGroupId) != null;

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
        }

        bool IPartySteeringQuery.RequiresCustomSpawnsSteering(MobileParty party)
        {
            Initialize();
            return State.IsGroupWithoutTown(_dynamicSpawnData.GetDynamicSpawnData(party)?.InvasionGroupId);
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
            _invasionReinforcementArrivals.LandDue();
        }

        // The food model asks per town, so it holds the behaviour and not the occupation pass,
        // which is replaced whenever a save rebinds the state.
        public float GetOngoingFoodSupport(Town town) => _invasionOccupations.GetOngoingFoodSupport(town);
    }
}
