using System;
using System.Collections.Generic;
using System.Linq;

namespace CustomSpawns.Invasions.Adapters
{
    // Native evidence and the two-callback corroboration it feeds. Object tokens are compared by identity.
    internal sealed class InvasionCaptureCorrelation
    {
        private readonly InvasionState _state;
        private readonly Dictionary<object, NativeResult> _events = new();

        internal InvasionCaptureCorrelation(InvasionState state) => _state = state;
        internal bool IsBound(object nativeEvent) => _events.ContainsKey(nativeEvent);

        internal void Bind(object nativeEvent, long contextId, string previousOwnerClanId)
        {
            _events.Add(nativeEvent, new NativeResult(contextId, previousOwnerClanId));
        }

        internal void RecordResult(object nativeEvent, string settlementId, long battleStartTimeBits,
            bool isSuccessfulFinalization, object? nativeCapturer, string expectedOwnerClanId,
            IEnumerable<object> participatingParties)
        {
            if (!_events.TryGetValue(nativeEvent, out var result) || result.Verified || !isSuccessfulFinalization || nativeCapturer == null) return;
            var context = _state.FindCapture(result.ContextId);
            if (context == null || context.Cancelled || context.SettlementId != settlementId || context.BattleStartTimeBits != battleStartTimeBits) return;
            result.Capturer = nativeCapturer;
            result.ExpectedOwnerClanId = expectedOwnerClanId;
            result.Participants = participatingParties.ToArray();
            result.Verified = true;
        }

        internal InvasionCaptureContext? ResolveOwnership(object? nativeEvent, bool isFinalizing,
            string settlementId, object capturer, string previousOwnerClanId, string newOwnerClanId, string actualOwnerClanId)
        {
            if (nativeEvent == null || !isFinalizing || !_events.TryGetValue(nativeEvent, out var result) || !result.Verified ||
                !ReferenceEquals(result.Capturer, capturer) || result.PreviousOwnerClanId != previousOwnerClanId ||
                result.ExpectedOwnerClanId != newOwnerClanId || actualOwnerClanId != newOwnerClanId) return null;
            var context = PendingContext(result);
            return context?.SettlementId == settlementId ? context : null;
        }

        internal void DiscardUnsuccessfulEvent(object nativeEvent)
        {
            if (!_events.TryGetValue(nativeEvent, out var result) || result.Verified) return;
            var context = _state.FindCapture(result.ContextId);
            if (context != null) context.Cancelled = true;
            _events.Remove(nativeEvent);
        }

        internal InvasionCaptureContext? ResolveAftermath(object? nativeEvent, bool isFinalizing,
            string settlementId, string attackerId, string previousOwnerClanId, string actualOwnerClanId,
            IEnumerable<object> contributingParties)
        {
            object[] participants = contributingParties.ToArray();
            if (nativeEvent != null)
            {
                if (!isFinalizing || !_events.TryGetValue(nativeEvent, out var liveResult)) return null;
                return MatchesAftermath(liveResult, settlementId, attackerId, previousOwnerClanId, participants)
                    ? PendingContext(liveResult) : null;
            }
            // Player menu callbacks have no live map event. They must match the exact result-side snapshot
            // whose ownership was independently verified while that native event was finalizing.
            var matches = _events.Values.Where(result => MatchesAftermath(result, settlementId, attackerId,
                previousOwnerClanId, participants)).Where(result =>
                _state.FindCapture(result.ContextId)?.OwnershipObserved == true &&
                result.ExpectedOwnerClanId == actualOwnerClanId).ToList();
            return matches.Count == 1 ? PendingContext(matches[0]) : null;
        }

        private bool MatchesAftermath(NativeResult result, string settlementId, string attackerId,
            string previousOwnerClanId, object[] participants)
        {
            var context = PendingContext(result);
            return result.Verified && context != null && context.SettlementId == settlementId &&
                context.StartingAttackerId == attackerId && result.PreviousOwnerClanId == previousOwnerClanId &&
                result.Participants.Length == participants.Length &&
                result.Participants.All(expected => participants.Any(actual => ReferenceEquals(expected, actual)));
        }

        private InvasionCaptureContext? PendingContext(NativeResult result)
        {
            var context = _state.FindCapture(result.ContextId);
            return context != null && !context.Cancelled && !context.Joined ? context : null;
        }

        // Ownership and aftermath reach us as two unrelated native callbacks. Each records its half, and the
        // capture is credited only once both halves agree they describe the same siege.
        internal bool ObserveOwnership(long contextId, string groupId, string capturerId, string previousOwnerClanId,
            string newOwnerClanId, bool bySiege)
        {
            var context = _state.FindCapture(contextId);
            var group = _state.FindGroup(groupId);
            if (context == null || context.Cancelled || context.Joined || group == null || group.IsSuspended ||
                !bySiege || group.ClanId != newOwnerClanId || !group.Members.Contains(capturerId)) return false;
            if (context.OwnershipObserved) return false;
            context.OwnershipGroupId = groupId;
            context.CapturerId = capturerId;
            context.PreviousOwnerClanId = previousOwnerClanId;
            context.OwnershipObserved = true;
            TryJoin(context);
            return true;
        }

        internal bool ObserveAftermath(long contextId, string attackerId, string previousOwnerClanId,
            IReadOnlyDictionary<string, string> participantGroups)
        {
            var context = _state.FindCapture(contextId);
            if (context == null || context.Cancelled || context.Joined || context.StartingAttackerId != attackerId) return false;
            if (context.ParticipantSnapshotCaptured) return false;
            context.AftermathPreviousOwnerClanId = previousOwnerClanId;
            context.Participants = participantGroups.Where(p => _state.FindGroup(p.Value)?.Members.Contains(p.Key) == true)
                .ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
            context.ParticipantSnapshotCaptured = true;
            TryJoin(context);
            return true;
        }

        private void TryJoin(InvasionCaptureContext context)
        {
            if (!context.OwnershipObserved || !context.ParticipantSnapshotCaptured ||
                context.PreviousOwnerClanId != context.AftermathPreviousOwnerClanId ||
                !context.Participants.TryGetValue(context.CapturerId!, out string capturerGroupId) ||
                capturerGroupId != context.OwnershipGroupId) return;
            InvasionGroupState? group = _state.FindGroup(context.OwnershipGroupId);
            if (group == null || group.IsSuspended) return;
            context.Participants = context.Participants.Where(p => p.Value == group.Id)
                .ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
            context.Joined = true;
            if (_state.CreditIndex.TryGetValue(context.SettlementId, out var previous))
            {
                var previousCapture = _state.FindCapture(previous.ContextId);
                if (previousCapture != null) previousCapture.Cancelled = true;
            }
            _state.CreditIndex[context.SettlementId] = new InvasionTownCredit(context.SettlementId, group.Id, context.Id);
        }

        internal void Prune()
        {
            foreach (var entry in _events.Where(e => PendingContext(e.Value) == null).ToList()) _events.Remove(entry.Key);
        }

        internal IReadOnlyList<NativeResult> ExportOwnedResults() => _events.Values.Where(result => result.Verified &&
            PendingContext(result)?.OwnershipObserved == true).ToList();

        internal bool RestoreOwnedResult(long contextId, string previousOwnerClanId, string expectedOwnerClanId,
            object? capturer, IEnumerable<object> participants)
        {
            var context = _state.FindCapture(contextId);
            if (capturer == null || context == null || !context.OwnershipObserved || context.Joined || context.Cancelled ||
                context.PreviousOwnerClanId != previousOwnerClanId ||
                _state.FindGroup(context.OwnershipGroupId)?.ClanId != expectedOwnerClanId) return false;
            _events.Add(new object(), new NativeResult(contextId, previousOwnerClanId)
            {
                Verified = true, Capturer = capturer, ExpectedOwnerClanId = expectedOwnerClanId,
                Participants = participants.ToArray()
            });
            return true;
        }

        internal sealed class NativeResult
        {
            internal NativeResult(long contextId, string previousOwnerClanId)
            { ContextId = contextId; PreviousOwnerClanId = previousOwnerClanId; }
            internal long ContextId { get; }
            internal string PreviousOwnerClanId { get; }
            internal bool Verified;
            internal object? Capturer;
            internal string ExpectedOwnerClanId = "";
            internal object[] Participants = new object[0];
        }
    }
}
