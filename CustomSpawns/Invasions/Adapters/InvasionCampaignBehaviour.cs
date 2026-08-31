using System;
using System.Collections.Generic;
using System.Linq;
using CustomSpawns.Data.Dao;
using CustomSpawns.Data.Dto;
using CustomSpawns.Spawn;
using CustomSpawns.UtilityBehaviours;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace CustomSpawns.Invasions.Adapters
{
    public sealed class InvasionCampaignBehaviour : CampaignBehaviorBase
    {
        private readonly SpawnDao _spawns;
        private readonly DynamicSpawnData _parties;
        private InvasionCaptureCorrelation _correlation;
        private bool _initialized;
        public InvasionState State { get; private set; } = new();

        public InvasionCampaignBehaviour(SpawnDao spawns, DynamicSpawnData parties, SaveInitialiser initialiser)
        {
            _spawns = spawns;
            _parties = parties;
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
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, Reconcile);
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
                data.SetInvasionIdentity(saved.Id, data.InvasionRole ?? (data.IsPrimarySpawn ? "Original" : "Escort"));
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
            data.SetInvasionIdentity(group.Id, data.IsPrimarySpawn ? "Original" : "Escort");
        }

        private void RemoveParty(MobileParty party)
        {
            State.RemoveMember(party.StringId);
            Reconcile();
        }

        private void Reconcile()
        {
            State.Reconcile(MobileParty.All.Where(p => p.IsActive).Select(p => p.StringId),
                id => Settlement.Find(id)?.OwnerClan?.StringId,
                group => GetConfiguration(group) != null && Clan.FindFirst(c => c.StringId == group.ClanId)?.IsEliminated == false);
            _correlation.Prune();
        }

        public InvasionData? GetConfiguration(InvasionGroupState group) => _spawns.FindByPartyTemplateId(group.SpawnTemplateId)?.Invasion;

        public InvasionCoastalTargeting? GetCoastalTargeting(MobileParty party)
        {
            Initialize();
            InvasionGroupState? group = State.FindMemberGroup(party.StringId);
            if (group == null || group.IsSuspended || party.ActualClan?.StringId != group.ClanId) return null;
            InvasionData? configuration = GetConfiguration(group);
            return configuration == null ? null : new InvasionCoastalTargeting(group.HasFirstCoastalConquest,
                configuration.CoastalTownIds);
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
        }

        private void OnOwnerChanged(Settlement settlement, bool openToClaim, Hero newOwner, Hero oldOwner,
            Hero capturer, ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail detail)
        {
            Initialize();
            State.ObserveOwner(settlement.StringId, settlement.OwnerClan?.StringId);
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
            bool coastal = InvasionCoastalTargeting.IsCoastalTown(configuration.CoastalTownIds,
                settlement.StringId, settlement.IsTown, settlement.HasPort);
            State.ObserveOwnership(context.Id, group.Id, party.StringId, oldOwner?.Clan?.StringId ?? "",
                newOwner?.Clan?.StringId ?? "", true, coastal);
        }

        // A later occupation adapter consumes this readiness seam on an hourly tick, never in capture callbacks.
        public IReadOnlyList<InvasionCaptureContext> GetReadyOccupations()
        {
            Initialize();
            Reconcile();
            var ready = new List<InvasionCaptureContext>();
            foreach (var context in State.Captures)
            {
                Settlement? settlement = Settlement.Find(context.SettlementId);
                if (settlement?.Town?.GarrisonParty?.IsActive != true || settlement.Party.MapEvent != null ||
                    !State.CanApplyOccupation(context.Id, settlement.OwnerClan?.StringId ?? "")) continue;
                bool donorInBattle = MobileParty.All.Any(p => p.IsActive && context.ParticipantPartyIds.Contains(p.StringId) && p.MapEvent != null);
                if (!donorInBattle) ready.Add(context);
            }
            return ready;
        }
    }
}
