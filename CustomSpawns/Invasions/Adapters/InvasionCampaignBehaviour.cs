using System;
using System.Collections.Generic;
using System.Linq;
using CustomSpawns.AI;
using CustomSpawns.AI.Offensives;
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
    public sealed class InvasionCampaignBehaviour : CampaignBehaviorBase, IInvasionFootholdQuery,
        IInvasionLandingQuery
    {
        private readonly SpawnDao _spawns;
        private readonly DynamicSpawnData _parties;
        private readonly SpawnGroupService _spawnGroups;
        private readonly InvasionCoastalTownResolver _coastalTownResolver = new();
        private readonly Func<int, int> _randomIndex;
        private InvasionCaptureCorrelation _correlation;
        private InvasionReinforcementArrivals _arrivals;
        private InvasionOccupations _occupations;
        private InvasionKingdomRetirement _retirement;
        private InvasionSieges _sieges;
        private InvasionMembership _membership;
        private bool _initialized;
        public InvasionState State { get; private set; } = new();

        internal InvasionCampaignBehaviour(SpawnDao spawns, DynamicSpawnData parties, SpawnGroupService spawnGroups,
            SaveInitialiser initialiser, Func<int, int>? randomIndex = null)
        {
            _spawns = spawns;
            _parties = parties;
            _spawnGroups = spawnGroups;
            _randomIndex = randomIndex ?? MBRandom.RandomInt;
            _correlation = new InvasionCaptureCorrelation(State);
            _arrivals = NewArrivals();
            _occupations = NewOccupations();
            _retirement = new InvasionKingdomRetirement(State);
            _sieges = new InvasionSieges(State, _correlation, _spawns);
            _membership = new InvasionMembership(State, _spawns, _parties, _correlation);

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
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, OnHourlyTick);
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, ScheduleReinforcements);
        }

        public override void SyncData(IDataStore dataStore)
        {
            InvasionSaveData? saved = dataStore.IsSaving ? InvasionSaveData.FromState(State) : null;

            if (saved != null)
            {
                saved.OwnedCaptureEvidence = _correlation.ExportOwnedResults().Select(result => new InvasionCaptureEvidenceSaveData
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
            _correlation = new InvasionCaptureCorrelation(State);
            _arrivals = NewArrivals();
            _occupations = NewOccupations();
            _retirement = new InvasionKingdomRetirement(State);
            _sieges = new InvasionSieges(State, _correlation, _spawns);
            _membership = new InvasionMembership(State, _spawns, _parties, _correlation);

            foreach (var evidence in saved?.OwnedCaptureEvidence ?? new List<InvasionCaptureEvidenceSaveData>())
            {
                _correlation.RestoreOwnedResult(evidence.CaptureId, evidence.PreviousOwnerClanId,
                    evidence.ExpectedOwnerClanId, evidence.Capturer, (evidence.Participants ?? new List<MobileParty>()).Cast<object>());
            }

            _initialized = false;
            _coastalTownResolver.ClearCache();
        }

        private InvasionReinforcementArrivals NewArrivals() => new(State, _spawns, _parties, _spawnGroups,
            _coastalTownResolver, _randomIndex);

        private InvasionOccupations NewOccupations() => new(State, _spawns, _parties, _coastalTownResolver);

        private void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;
            _membership.Reconcile();
            _membership.RegisterLive();
            _sieges.RebindUnconfirmed();
        }

        private void RegisterParty(MobileParty party, CsPartyData data) => _membership.Register(party, data);

        private void RemoveParty(MobileParty party) => _membership.Remove(party);

        private void OnHourlyTick()
        {
            Initialize();

            _membership.Reconcile();
            _retirement.RetireSpent();
            _occupations.ApplyReady();
        }

        public bool HasPersistentInvasionGroup(CsPartyData partyData) => State.FindGroup(partyData.InvasionGroupId) != null;

        Func<Settlement, bool>? IInvasionLandingQuery.GetInitialCoastalTargetFilter(MobileParty party)
        {
            Initialize();
            return _arrivals.FindInitialCoastalTargetFilter(party);
        }

        IReadOnlyList<MobileParty> IInvasionLandingQuery.GetLandingParties(MobileParty party)
        {
            Initialize();
            return _membership.FindLiveMembers(party);
        }

        private void OnMapEventStarted(MapEvent mapEvent, PartyBase attacker, PartyBase defender)
        {
            Initialize();
            _sieges.ObserveStart(mapEvent);
        }

        private void OnMapEventEnded(MapEvent mapEvent) => _sieges.ObserveEnd(mapEvent);

        private void OnAftermath(MobileParty attacker, Settlement settlement, SiegeAftermathAction.SiegeAftermath aftermath,
            Clan previousOwner, Dictionary<MobileParty, float> contributions)
        {
            Initialize();

            long? capture = _sieges.ObserveAftermath(attacker, settlement, previousOwner, contributions);

            if (capture != null)
            {
                _arrivals.StartSchedule(capture.Value);
            }
        }

        private void OnOwnerChanged(Settlement settlement, bool openToClaim, Hero newOwner, Hero oldOwner,
            Hero capturer, ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail detail)
        {
            Initialize();

            long? capture = _sieges.ObserveOwnerChange(settlement, newOwner, oldOwner, capturer, detail);

            if (capture != null)
            {
                _arrivals.StartSchedule(capture.Value);
            }
        }

        bool IInvasionFootholdQuery.IsGroupWithoutTown(string? invasionGroupId)
        {
            Initialize();
            return State.IsGroupWithoutTown(invasionGroupId);
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

            _membership.Reconcile();
            _arrivals.LandDue();
        }

        // The food model asks per town, so it holds the behaviour and not the occupation pass,
        // which is replaced whenever a save rebinds the state.
        public float GetOngoingFoodSupport(Town town) => _occupations.GetOngoingFoodSupport(town);
    }
}
