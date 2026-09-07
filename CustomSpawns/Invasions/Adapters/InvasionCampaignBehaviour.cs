using System;
using System.Collections.Generic;
using System.Linq;
using CustomSpawns.Data.Dao;
using CustomSpawns.Data.Dto;
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
    public sealed class InvasionCampaignBehaviour : CampaignBehaviorBase
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly SpawnDao _spawns;
        private readonly DynamicSpawnData _parties;
        private readonly SpawnGroupService _spawnGroups;
        private readonly InvasionCoastalTownResolver _coastalTownResolver = new();
        private readonly Func<int, int> _randomIndex;
        private InvasionCaptureCorrelation _correlation;
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
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, OnHourlyTick);
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, ScheduleReinforcements);
        }

        public override void SyncData(IDataStore dataStore)
        {
            InvasionSaveData? saved = dataStore.IsSaving ? InvasionSaveData.FromState(State) : null;
            if (saved != null)
                saved.OwnedCaptureEvidence = _correlation.ExportOwnedResults().Select(result => new InvasionCaptureEvidenceSaveData
                {
                    ContextId = result.ContextId, PreviousOwnerClanId = result.PreviousOwnerClanId,
                    ExpectedOwnerClanId = result.ExpectedOwnerClanId, Capturer = result.Capturer as Hero,
                    Participants = result.Participants.Cast<MobileParty>().ToList()
                }).ToList();
            dataStore.SyncData("_invasionState", ref saved);
            if (!dataStore.IsLoading) return;
            State = saved?.ToState() ?? new InvasionState();
            _correlation = new InvasionCaptureCorrelation(State);
            foreach (var evidence in saved?.OwnedCaptureEvidence ?? new List<InvasionCaptureEvidenceSaveData>())
                _correlation.RestoreOwnedResult(evidence.ContextId, evidence.PreviousOwnerClanId,
                    evidence.ExpectedOwnerClanId, evidence.Capturer, (evidence.Participants ?? new List<MobileParty>()).Cast<object>());
            _initialized = false;
            _coastalTownResolver.ClearCache();
        }

        private void Initialize()
        {
            if (_initialized) return;
            _initialized = true;
            Reconcile();
            foreach (MobileParty party in MobileParty.All.Where(p => p.IsActive))
            {
                CsPartyData? data = _parties.GetDynamicSpawnData(party);
                if (data != null) RegisterParty(party, data);
            }
            // Only a unique live native anchor can restore an incomplete occurrence.
            var liveEvents = Settlement.All.Select(s => s.Party.MapEvent).Where(e => e != null).Distinct().ToList();
            var rebound = new HashSet<long>(_correlation.ExportOwnedResults().Select(result => result.ContextId));
            foreach (var context in State.Captures.Where(c => !c.Cancelled && !c.Joined))
            {
                if (rebound.Contains(context.Id)) continue;
                var matches = liveEvents.Where(e => e!.MapEventSettlement?.StringId == context.SettlementId &&
                    BitConverter.DoubleToInt64Bits(e.BattleStartTime.ToSeconds) == context.BattleStartTimeBits).ToList();
                int savedMatches = State.Captures.Count(c => !c.Cancelled && !c.Joined &&
                    c.SettlementId == context.SettlementId && c.BattleStartTimeBits == context.BattleStartTimeBits);
                if (matches.Count != 1 || savedMatches != 1 || _correlation.IsBound(matches[0]!)) continue;
                _correlation.Bind(matches[0]!, context.Id, context.PreviousOwnerClanId ??
                    context.AftermathPreviousOwnerClanId ?? matches[0]!.MapEventSettlement.OwnerClan?.StringId ?? "");
                rebound.Add(context.Id);
            }
            State.DiscardUnboundCaptures(rebound);
        }

        private void RegisterParty(MobileParty party, CsPartyData data)
        {
            // Saved membership survives removed configurations so cleanup remains possible.
            InvasionGroupState? saved = State.FindGroup(data.InvasionGroupId);
            if (saved != null && saved.ClanId == party.ActualClan?.StringId && saved.SpawnTemplateId == data.OwningSpawnTemplateId)
            {
                saved.Members.Add(party.StringId);
                data.SetInvasionIdentity(saved.Id, data.InvasionRole ?? InvasionRoleFor(data));
                return;
            }
            string? templateId = data.OwningSpawnTemplateId;
            Clan? clan = party.ActualClan;
            if (templateId == null || clan == null || clan.IsEliminated || clan.IsBanditFaction ||
                _spawns.FindByPartyTemplateId(templateId)?.Invasion == null)
            {
                data.SetInvasionIdentity(null, null);
                return;
            }
            var group = State.RegisterMember(templateId, clan.StringId, party.StringId);
            data.SetInvasionIdentity(group.Id, InvasionRoleFor(data));
        }

        private void RemoveParty(MobileParty party)
        {
            State.RemoveMember(party.StringId);
            Reconcile();
        }

        private static string InvasionRoleFor(CsPartyData data) => data.IsIndependentReinforcement ? "Reinforcement" :
            data.IsPrimarySpawn ? "Original" : "Escort";

        private void Reconcile()
        {
            State.Reconcile(MobileParty.All.Where(p => p.IsActive).Select(p => p.StringId),
                id => Settlement.Find(id)?.OwnerClan?.StringId,
                group => GetConfiguration(group) != null && Clan.FindFirst(c => c.StringId == group.ClanId)?.IsEliminated == false);
            _correlation.Prune();
        }

        private void OnHourlyTick()
        {
            Reconcile();
            ApplyReadyOccupations();
        }

        public InvasionData? GetConfiguration(InvasionGroupState group) => _spawns.FindByPartyTemplateId(group.SpawnTemplateId)?.Invasion;

        public bool HasPersistentInvasionGroup(CsPartyData partyData) => State.FindGroup(partyData.InvasionGroupId) != null;

        public Func<Settlement, bool>? GetInitialCoastalTargetFilter(MobileParty party)
        {
            Initialize();
            InvasionGroupState? group = State.FindMemberGroup(party.StringId);
            if (group == null || group.IsSuspended || group.HasFirstCoastalConquest ||
                party.ActualClan?.StringId != group.ClanId) return null;
            InvasionData? configuration = GetConfiguration(group);
            return configuration == null ? null : _coastalTownResolver.IsCoastalTown;
        }

        private void OnMapEventStarted(MapEvent mapEvent, PartyBase attacker, PartyBase defender)
        {
            Initialize();
            Settlement? town = mapEvent.MapEventSettlement;
            BattleSideEnum conqueringSide = GetConqueringSide(mapEvent.IsSiegeAssault, mapEvent.IsSallyOut, mapEvent.IsBlockadeSallyOut);
            if (conqueringSide == BattleSideEnum.None || town?.IsTown != true) return;
            MobileParty? besieger = mapEvent.GetMapEventSide(conqueringSide).LeaderParty?.MobileParty;
            if (besieger == null) return;
            if (_correlation.IsBound(mapEvent)) return;
            var context = State.StartCapture(town.StringId, BitConverter.DoubleToInt64Bits(mapEvent.BattleStartTime.ToSeconds), besieger.StringId);
            _correlation.Bind(mapEvent, context.Id, town.OwnerClan?.StringId ?? "");
        }

        private void OnMapEventEnded(MapEvent mapEvent)
        {
            if (!IsSuccessfulFinalization(mapEvent))
            {
                _correlation.DiscardUnsuccessfulEvent(mapEvent);
                return;
            }
            // Native ownership follows this event synchronously; player aftermath may follow much later.
            // Preserve immutable result evidence while native participant sides still exist.
            RecordNativeResult(mapEvent);
        }

        private void RecordNativeResult(MapEvent? mapEvent)
        {
            if (mapEvent?.MapEventSettlement == null || !IsSuccessfulFinalization(mapEvent)) return;
            BattleSideEnum conqueringSide = GetConqueringSide(mapEvent.IsSiegeAssault, mapEvent.IsSallyOut, mapEvent.IsBlockadeSallyOut);
            PartyBase? capturerParty = mapEvent.GetMapEventSide(conqueringSide).LeaderParty;
            _correlation.RecordResult(mapEvent, mapEvent.MapEventSettlement.StringId,
                BitConverter.DoubleToInt64Bits(mapEvent.BattleStartTime.ToSeconds), true,
                capturerParty?.Owner, capturerParty?.MobileParty?.MapFaction?.Leader?.Clan?.StringId ?? "",
                mapEvent.PartiesOnSide(conqueringSide).Where(p => p.Party.IsMobile)
                    .Select(p => (object)p.Party.MobileParty));
        }

        private static bool IsSuccessfulFinalization(MapEvent? mapEvent) => mapEvent != null &&
            GetConqueringSide(mapEvent.IsSiegeAssault, mapEvent.IsSallyOut, mapEvent.IsBlockadeSallyOut) != BattleSideEnum.None &&
            mapEvent.WinningSide == GetConqueringSide(mapEvent.IsSiegeAssault, mapEvent.IsSallyOut, mapEvent.IsBlockadeSallyOut) && mapEvent.IsFinalized;

        // Native sally-out attackers are the garrison; the besiegers are the defenders.
        internal static BattleSideEnum GetConqueringSide(bool isSiegeAssault, bool isSallyOut, bool isBlockadeSallyOut) =>
            isSiegeAssault ? BattleSideEnum.Attacker : isSallyOut || isBlockadeSallyOut ? BattleSideEnum.Defender : BattleSideEnum.None;

        private void OnAftermath(MobileParty attacker, Settlement settlement, SiegeAftermathAction.SiegeAftermath aftermath,
            Clan previousOwner, Dictionary<MobileParty, float> contributions)
        {
            Initialize();
            MapEvent? mapEvent = settlement.Party.MapEvent ?? attacker.MapEvent;
            RecordNativeResult(mapEvent);
            var context = _correlation.ResolveAftermath(mapEvent, IsSuccessfulFinalization(mapEvent),
                settlement.StringId, attacker.StringId, previousOwner?.StringId ?? "", settlement.OwnerClan?.StringId ?? "",
                contributions.Keys.Cast<object>());
            if (context == null) return;
            var participants = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (MobileParty party in contributions.Keys)
            {
                var group = State.FindMemberGroup(party.StringId);
                if (group != null && group.ClanId == party.ActualClan?.StringId) participants[party.StringId] = group.Id;
            }
            // Copy immediately: the native dictionary is cleared after the callback.
            State.ObserveAftermath(context.Id, attacker.StringId, previousOwner?.StringId ?? "", participants);
            StartReinforcementSchedule(context.Id);
        }

        private void OnOwnerChanged(Settlement settlement, bool openToClaim, Hero newOwner, Hero oldOwner,
            Hero capturer, ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail detail)
        {
            Initialize();
            InvasionTownCredit? previousCredit = State.FindCredit(settlement.StringId);
            State.ObserveOwner(settlement.StringId, settlement.OwnerClan?.StringId);
            if (previousCredit != null && State.FindCredit(settlement.StringId) == null)
                Logger.Info("Invasion coastal credit lost: group=" + previousCredit.GroupId + " town=" + settlement.StringId);
            if (!settlement.IsTown || detail != ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail.BySiege) return;
            MobileParty? party = capturer?.PartyBelongedTo;
            if (party == null) return;
            var group = State.FindMemberGroup(party.StringId);
            MapEvent? mapEvent = settlement.Party.MapEvent ?? party.MapEvent;
            if (!ReferenceEquals(mapEvent?.MapEventSettlement, settlement)) return;
            RecordNativeResult(mapEvent);
            var context = _correlation.ResolveOwnership(mapEvent, IsSuccessfulFinalization(mapEvent), settlement.StringId, capturer,
                oldOwner?.Clan?.StringId ?? "", newOwner?.Clan?.StringId ?? "", settlement.OwnerClan?.StringId ?? "");
            if (group == null || context == null || party.ActualClan?.StringId != group.ClanId) return;
            InvasionData? configuration = GetConfiguration(group);
            if (configuration == null) return;
            bool coastal = _coastalTownResolver.IsCoastalTown(settlement);
            State.ObserveOwnership(context.Id, group.Id, party.StringId, oldOwner?.Clan?.StringId ?? "",
                newOwner?.Clan?.StringId ?? "", true, coastal);
            StartReinforcementSchedule(context.Id);
        }

        private void StartReinforcementSchedule(long contextId)
        {
            InvasionCaptureContext? context = State.FindCapture(contextId);
            InvasionData? configuration = context == null ? null : State.FindGroup(context.OwnershipGroupId) is { } group
                ? GetConfiguration(group)
                : null;
            if (configuration != null && State.StartReinforcementSchedule(contextId, CampaignTime.Now.ToDays,
                    configuration.ReinforcementIntervalInDays))
            {
                Logger.Info("Invasion coastal credit joined: group=" + context!.OwnershipGroupId + " town=" +
                            context.SettlementId + " next_arrival_day=" +
                            State.FindGroup(context.OwnershipGroupId)!.NextReinforcementDeadlineDays);
            }
        }

        private void ScheduleReinforcements()
        {
            Initialize();
            Reconcile();
            double nowDays = CampaignTime.Now.ToDays;
            foreach (InvasionGroupState group in State.Groups.ToList())
            {
                if (group.NextReinforcementDeadlineDays == 0 || group.NextReinforcementDeadlineDays > nowDays)
                    continue;

                // Claim the slot first. A failed spawn and repeated callback both wait for the next interval.
                if (!State.TryConsumeReinforcementSlot(group.Id, nowDays))
                    continue;

                InvasionData? configuration = GetConfiguration(group);
                Clan? clan = Clan.FindFirst(candidate => candidate.StringId == group.ClanId);
                SpawnDto? spawn = _spawns.FindByPartyTemplateId(group.SpawnTemplateId);
                if (configuration == null || spawn == null || clan == null || clan.IsEliminated || group.IsSuspended)
                {
                    Logger.Info("Invasion reinforcement slot skipped: group=" + group.Id + " reason=configuration_or_clan");
                    continue;
                }
                if (ActiveReinforcementCount(group.Id) >= configuration.ReinforcementActivePartyCap)
                {
                    Logger.Info("Invasion reinforcement slot skipped: group=" + group.Id + " reason=active_cap");
                    continue;
                }

                List<Settlement> eligible = Settlement.All.Where(settlement => IsEligibleLandingSettlement(settlement,
                    group, configuration)).ToList();
                if (eligible.Count == 0)
                {
                    Logger.Info("Invasion reinforcement slot skipped: group=" + group.Id + " reason=no_credited_port");
                    continue;
                }

                Settlement landing = eligible[_randomIndex(eligible.Count)];
                PartyTemplateObject? template = Campaign.Current.ObjectManager.GetObject<PartyTemplateObject>(
                    configuration.ReinforcementPartyTemplateId);
                if (template == null || !IsEligibleLandingSettlement(landing, group, configuration))
                {
                    Logger.Info("Invasion reinforcement slot skipped: group=" + group.Id + " reason=template_or_port_changed");
                    continue;
                }

                MobileParty? reinforcement = _spawnGroups.SpawnInvasionReinforcement(spawn, template, landing, clan);
                Logger.Info("Invasion reinforcement " + (reinforcement == null ? "failed" : "arrived") + ": group=" +
                            group.Id + " town=" + landing.StringId + " next_arrival_day=" +
                            group.NextReinforcementDeadlineDays);
            }
        }

        private int ActiveReinforcementCount(string groupId)
        {
            return MobileParty.All.Count(party => party.IsActive && _parties.GetDynamicSpawnData(party) is { } data &&
                data.InvasionGroupId == groupId && data.IsIndependentReinforcement);
        }

        private bool IsEligibleLandingSettlement(Settlement settlement, InvasionGroupState group,
            InvasionData configuration)
        {
            InvasionTownCredit? credit = State.FindCredit(settlement.StringId);
            return credit?.GroupId == group.Id && credit.SupplyPortEligible && settlement.IsTown &&
                   settlement.OwnerClan?.StringId == group.ClanId && settlement.SiegeEvent == null &&
                   _coastalTownResolver.IsCoastalTown(settlement);
        }

        public float GetOngoingFoodSupport(Town town)
        {
            InvasionTownCredit? credit = State.FindCredit(town.Settlement.StringId);
            InvasionGroupState? group = State.FindGroup(credit?.GroupId);
            InvasionData? configuration = group == null ? null : GetConfiguration(group);
            return IsEligibleForFood(town, group, configuration) ? configuration!.DailyFoodBonus : 0f;
        }

        private void ApplyReadyOccupations()
        {
            foreach (InvasionCaptureContext capture in GetReadyOccupations())
            {
                Town? town = Settlement.Find(capture.SettlementId)?.Town;
                InvasionGroupState? group = State.FindGroup(capture.OwnershipGroupId);
                InvasionData? configuration = group == null ? null : GetConfiguration(group);
                if (town?.GarrisonParty == null || group == null || configuration == null)
                    continue;

                foreach (MobileParty party in MobileParty.All.Where(p => p.IsActive &&
                             capture.ParticipantPartyIds.Contains(p.StringId) &&
                             _parties.GetDynamicSpawnData(p)?.IsEscort == true).ToList())
                {
                    town.GarrisonParty.MemberRoster.Add(party.MemberRoster);
                    DestroyPartyAction.Apply(null, party);
                }

                if (IsEligibleForFood(town, group, configuration))
                    InvasionOccupationFood.Apply(town, configuration.FoodStockFloor);
                if (State.CompleteOccupation(capture.Id, town.Settlement.OwnerClan?.StringId ?? ""))
                    Logger.Info("Invasion occupation applied: group=" + group.Id + " town=" + town.Settlement.StringId);
            }
        }

        private bool IsEligibleForFood(Town town, InvasionGroupState? group, InvasionData? configuration)
        {
            Clan? clan = group == null ? null : Clan.FindFirst(candidate => candidate.StringId == group.ClanId);
            InvasionTownCredit? credit = State.FindCredit(town.Settlement.StringId);
            return InvasionFoodSupportRules.IsEligibleForOngoingSupport(credit?.GroupId == group?.Id,
                credit?.SupplyPortEligible == true, configuration != null &&
                _coastalTownResolver.IsCoastalTown(town.Settlement),
                group?.ClanId == town.Settlement.OwnerClan?.StringId, configuration != null && !group!.IsSuspended &&
                clan?.IsEliminated == false, town.Settlement.SiegeEvent != null);
        }

        // Occupation is applied on the hourly tick after Bannerlord has finished the capture callbacks.
        public IReadOnlyList<InvasionCaptureContext> GetReadyOccupations()
        {
            Initialize();
            Reconcile();
            var ready = new List<InvasionCaptureContext>();
            foreach (var context in State.Captures)
            {
                Settlement? settlement = Settlement.Find(context.SettlementId);
                if (settlement?.Town?.GarrisonParty?.IsActive != true || settlement.Party.MapEvent != null ||
                    settlement.SiegeEvent != null ||
                    !State.CanApplyOccupation(context.Id, settlement.OwnerClan?.StringId ?? "")) continue;
                bool donorInBattle = MobileParty.All.Any(p => p.IsActive && context.ParticipantPartyIds.Contains(p.StringId) && p.MapEvent != null);
                if (!donorInBattle) ready.Add(context);
            }
            return ready;
        }
    }
}
