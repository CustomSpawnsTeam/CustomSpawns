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

namespace CustomSpawns.Invasions.Adapters
{
    public sealed class InvasionCampaignBehaviour : CampaignBehaviorBase
    {
        private readonly SpawnDao _spawns;
        private readonly DynamicSpawnData _parties;
        private readonly Dictionary<MapEvent, long> _liveContexts = new();
        private bool _initialized;
        public InvasionState State { get; private set; } = new();

        public InvasionCampaignBehaviour(SpawnDao spawns, DynamicSpawnData parties, SaveInitialiser initialiser)
        {
            _spawns = spawns;
            _parties = parties;
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
            dataStore.SyncData("_invasionState", ref saved);
            if (!dataStore.IsLoading) return;
            State = saved?.ToState() ?? new InvasionState();
            _liveContexts.Clear();
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
            var rebound = new HashSet<long>();
            foreach (var context in State.Captures.Where(c => !c.Cancelled && !c.Joined))
            {
                var matches = liveEvents.Where(e => e!.MapEventSettlement?.StringId == context.SettlementId &&
                    BitConverter.DoubleToInt64Bits(e.BattleStartTime.ToSeconds) == context.BattleStartTimeBits).ToList();
                int savedMatches = State.Captures.Count(c => !c.Cancelled && !c.Joined &&
                    c.SettlementId == context.SettlementId && c.BattleStartTimeBits == context.BattleStartTimeBits);
                if (matches.Count != 1 || savedMatches != 1 || _liveContexts.ContainsKey(matches[0]!)) continue;
                _liveContexts.Add(matches[0]!, context.Id);
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
        }

        public InvasionData? GetConfiguration(InvasionGroupState group) => _spawns.FindByPartyTemplateId(group.SpawnTemplateId)?.Invasion;

        private void OnMapEventStarted(MapEvent mapEvent, PartyBase attacker, PartyBase defender)
        {
            Initialize();
            Settlement? town = mapEvent.MapEventSettlement;
            if (!mapEvent.IsSiegeAssault || town?.IsTown != true || attacker.MobileParty == null) return;
            if (_liveContexts.ContainsKey(mapEvent)) return;
            var context = State.StartCapture(town.StringId, BitConverter.DoubleToInt64Bits(mapEvent.BattleStartTime.ToSeconds), attacker.MobileParty.StringId);
            _liveContexts.Add(mapEvent, context.Id);
        }

        private void OnMapEventEnded(MapEvent mapEvent)
        {
            // Native aftermath may be dispatched later. The primitive occurrence survives map-event teardown.
            _liveContexts.Remove(mapEvent);
        }

        private InvasionCaptureContext? ResolveContext(Settlement settlement, string? attackerId = null)
        {
            var matches = State.Captures.Where(c => c.SettlementId == settlement.StringId && !c.Cancelled && !c.Joined &&
                (attackerId == null || c.StartingAttackerId == attackerId)).ToList();
            return matches.Count == 1 ? matches[0] : null;
        }

        private void OnAftermath(MobileParty attacker, Settlement settlement, SiegeAftermathAction.SiegeAftermath aftermath,
            Clan previousOwner, Dictionary<MobileParty, float> contributions)
        {
            Initialize();
            var context = ResolveContext(settlement, attacker.StringId);
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
            State.ObserveOwner(settlement.StringId, newOwner?.Clan?.StringId);
            if (!settlement.IsTown || detail != ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail.BySiege) return;
            MobileParty? party = capturer?.PartyBelongedTo;
            if (party == null) return;
            var group = State.FindMemberGroup(party.StringId);
            var context = ResolveContext(settlement);
            if (group == null || context == null || party.ActualClan?.StringId != group.ClanId) return;
            InvasionData? configuration = GetConfiguration(group);
            if (configuration == null) return;
            bool coastal = configuration.CoastalTownIds.Count > 0
                ? configuration.CoastalTownIds.Contains(settlement.StringId) : settlement.HasPort;
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
