using System;
using System.Collections.Generic;
using System.Linq;
using CustomSpawns.Data.Dao;
using CustomSpawns.Data.Dto;
using NLog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace CustomSpawns.Invasions.Adapters
{
    /**
     * Reads a siege off the native map event that fights it. Bannerlord tells the same story twice,
     * through the aftermath and through the owner change, so each observation is handed to the
     * correlation and only the one that completes a capture reports it back.
     */
    internal sealed class InvasionSieges
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly InvasionState _state;
        private readonly InvasionCaptureCorrelation _correlation;
        private readonly SpawnDao _spawns;

        internal InvasionSieges(InvasionState state, InvasionCaptureCorrelation correlation, SpawnDao spawns)
        {
            _state = state;
            _correlation = correlation;
            _spawns = spawns;
        }

        internal void ObserveStart(MapEvent mapEvent)
        {
            Settlement? town = mapEvent.MapEventSettlement;
            BattleSideEnum conqueringSide = GetConqueringSide(mapEvent.IsSiegeAssault, mapEvent.IsSallyOut, mapEvent.IsBlockadeSallyOut);
            if (conqueringSide == BattleSideEnum.None || town?.IsTown != true) return;
            MobileParty? besieger = mapEvent.GetMapEventSide(conqueringSide).LeaderParty?.MobileParty;
            if (besieger == null) return;
            if (_correlation.IsBound(mapEvent)) return;
            var capture = _state.StartCapture(town.StringId, BitConverter.DoubleToInt64Bits(mapEvent.BattleStartTime.ToSeconds), besieger.StringId);
            _correlation.Bind(mapEvent, capture.Id, town.OwnerClan?.StringId ?? "");
        }

        internal void ObserveEnd(MapEvent mapEvent)
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

        internal long? ObserveAftermath(MobileParty attacker, Settlement settlement, Clan previousOwner,
            Dictionary<MobileParty, float> contributions)
        {
            MapEvent? mapEvent = settlement.Party.MapEvent ?? attacker.MapEvent;
            RecordNativeResult(mapEvent);
            var capture = _correlation.ResolveAftermath(mapEvent, IsSuccessfulFinalization(mapEvent),
                settlement.StringId, attacker.StringId, previousOwner?.StringId ?? "", settlement.OwnerClan?.StringId ?? "",
                contributions.Keys.Cast<object>());
            if (capture == null) return null;
            var participants = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (MobileParty party in contributions.Keys)
            {
                var group = _state.FindMemberGroup(party.StringId);
                if (group != null && group.HoldsClan(party.ActualClan?.StringId)) participants[party.StringId] = group.Id;
            }
            // Copy immediately: the native dictionary is cleared after the callback.
            _correlation.ObserveAftermath(capture.Id, attacker.StringId, previousOwner?.StringId ?? "", participants);
            return capture.Id;
        }

        internal long? ObserveOwnerChange(Settlement settlement, Hero newOwner, Hero oldOwner, Hero capturer,
            ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail detail)
        {
            InvasionFoothold? previousFoothold = _state.FindFoothold(settlement.StringId);
            _state.ObserveOwner(settlement.StringId, settlement.OwnerClan?.StringId);
            if (previousFoothold != null && _state.FindFoothold(settlement.StringId) == null)
                Logger.Info("Invasion coastal foothold lost: group=" + previousFoothold.GroupId + " town=" + settlement.StringId);
            if (!settlement.IsTown || detail != ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail.BySiege) return null;
            MobileParty? party = capturer?.PartyBelongedTo;
            if (party == null) return null;
            var group = _state.FindMemberGroup(party.StringId);
            MapEvent? mapEvent = settlement.Party.MapEvent ?? party.MapEvent;
            if (!ReferenceEquals(mapEvent?.MapEventSettlement, settlement)) return null;
            RecordNativeResult(mapEvent);
            var capture = _correlation.ResolveOwnership(mapEvent, IsSuccessfulFinalization(mapEvent), settlement.StringId, capturer,
                oldOwner?.Clan?.StringId ?? "", newOwner?.Clan?.StringId ?? "", settlement.OwnerClan?.StringId ?? "");
            if (group == null || capture == null || !group.HoldsClan(party.ActualClan?.StringId)) return null;
            InvasionData? configuration = _spawns.FindByPartyTemplateId(group.SpawnTemplateId)?.Invasion;
            if (configuration == null) return null;
            _correlation.ObserveOwnership(capture.Id, group.Id, party.StringId, oldOwner?.Clan?.StringId ?? "",
                newOwner?.Clan?.StringId ?? "", true);
            return capture.Id;
        }

        internal void RebindUnconfirmed()
        {
            // Only a unique live native anchor can restore an incomplete occurrence.
            var liveEvents = Settlement.All.Select(s => s.Party.MapEvent).Where(e => e != null).Distinct().ToList();
            var rebound = new HashSet<long>(_correlation.ExportOwnedResults().Select(result => result.CaptureId));
            foreach (var capture in _state.Captures.Where(c => !c.Cancelled && !c.IsConfirmed))
            {
                if (rebound.Contains(capture.Id)) continue;
                var matches = liveEvents.Where(e => e!.MapEventSettlement?.StringId == capture.SettlementId &&
                    BitConverter.DoubleToInt64Bits(e.BattleStartTime.ToSeconds) == capture.BattleStartTimeBits).ToList();
                int savedMatches = _state.Captures.Count(c => !c.Cancelled && !c.IsConfirmed &&
                    c.SettlementId == capture.SettlementId && c.BattleStartTimeBits == capture.BattleStartTimeBits);
                if (matches.Count != 1 || savedMatches != 1 || _correlation.IsBound(matches[0]!)) continue;
                _correlation.Bind(matches[0]!, capture.Id, capture.PreviousOwnerClanId ??
                    capture.AftermathPreviousOwnerClanId ?? matches[0]!.MapEventSettlement.OwnerClan?.StringId ?? "");
                rebound.Add(capture.Id);
            }
            _state.DiscardUnboundCaptures(rebound);
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
    }
}
