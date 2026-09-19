using System;
using System.Collections.Generic;
using System.Linq;

namespace CustomSpawns.Invasions.Adapters
{
    // Native evidence and the two-callback corroboration it feeds. Object tokens are compared by identity.
    internal sealed class InvasionCaptureCorrelation
    {
        private readonly InvasionState _invasionState;
        private readonly Dictionary<object, NativeResult> _events = new();

        internal InvasionCaptureCorrelation(InvasionState state) => _invasionState = state;
        internal bool IsBound(object nativeEvent) => _events.ContainsKey(nativeEvent);

        internal void Bind(object nativeEvent, long captureId, string previousOwnerClanId)
        {
            _events.Add(nativeEvent, new NativeResult(captureId, previousOwnerClanId));
        }

        internal void RecordResult(object nativeEvent, string settlementId, long battleStartTimeBits,
            bool isSuccessfulFinalization, object? nativeCapturer, string expectedOwnerClanId,
            IEnumerable<object> participatingParties)
        {
            if (!_events.TryGetValue(nativeEvent, out var result) || result.Verified || !isSuccessfulFinalization || nativeCapturer == null) return;
            var capture = _invasionState.FindCapture(result.CaptureId);
            if (capture == null || capture.Cancelled || capture.SettlementId != settlementId || capture.BattleStartTimeBits != battleStartTimeBits) return;
            result.Capturer = nativeCapturer;
            result.ExpectedOwnerClanId = expectedOwnerClanId;
            result.Participants = participatingParties.ToArray();
            result.Verified = true;
        }

        internal InvasionCapture? ResolveOwnership(object? nativeEvent, bool isFinalizing,
            string settlementId, object capturer, string previousOwnerClanId, string newOwnerClanId, string actualOwnerClanId)
        {
            if (nativeEvent == null || !isFinalizing || !_events.TryGetValue(nativeEvent, out var result) || !result.Verified ||
                !ReferenceEquals(result.Capturer, capturer) || result.PreviousOwnerClanId != previousOwnerClanId ||
                result.ExpectedOwnerClanId != newOwnerClanId || actualOwnerClanId != newOwnerClanId) return null;
            var capture = PendingCapture(result);
            return capture?.SettlementId == settlementId ? capture : null;
        }

        internal void DiscardUnsuccessfulEvent(object nativeEvent)
        {
            if (!_events.TryGetValue(nativeEvent, out var result) || result.Verified) return;
            var capture = _invasionState.FindCapture(result.CaptureId);
            if (capture != null) capture.Cancelled = true;
            _events.Remove(nativeEvent);
        }

        internal InvasionCapture? ResolveAftermath(object? nativeEvent, bool isFinalizing,
            string settlementId, string attackerId, string previousOwnerClanId, string actualOwnerClanId,
            IEnumerable<object> contributingParties)
        {
            object[] participants = contributingParties.ToArray();
            if (nativeEvent != null)
            {
                if (!isFinalizing || !_events.TryGetValue(nativeEvent, out var liveResult)) return null;
                return MatchesAftermath(liveResult, settlementId, attackerId, previousOwnerClanId, participants)
                    ? PendingCapture(liveResult) : null;
            }
            // Player menu callbacks have no live map event. They must match the exact result-side snapshot
            // whose ownership was independently verified while that native event was finalizing.
            var matches = _events.Values.Where(result => MatchesAftermath(result, settlementId, attackerId,
                previousOwnerClanId, participants)).Where(result =>
                _invasionState.FindCapture(result.CaptureId)?.OwnershipObserved == true &&
                result.ExpectedOwnerClanId == actualOwnerClanId).ToList();
            return matches.Count == 1 ? PendingCapture(matches[0]) : null;
        }

        private bool MatchesAftermath(NativeResult result, string settlementId, string attackerId,
            string previousOwnerClanId, object[] participants)
        {
            var capture = PendingCapture(result);
            return result.Verified && capture != null && capture.SettlementId == settlementId &&
                capture.StartingAttackerId == attackerId && result.PreviousOwnerClanId == previousOwnerClanId &&
                result.Participants.Length == participants.Length &&
                result.Participants.All(expected => participants.Any(actual => ReferenceEquals(expected, actual)));
        }

        private InvasionCapture? PendingCapture(NativeResult result)
        {
            var capture = _invasionState.FindCapture(result.CaptureId);
            return capture != null && !capture.Cancelled && !capture.IsConfirmed ? capture : null;
        }

        // Ownership and aftermath reach us as two unrelated native callbacks. Each records its half, and the
        // capture is confirmed only once both halves agree they describe the same siege.
        internal bool ObserveOwnership(long captureId, string groupId, string capturerId, string previousOwnerClanId,
            string newOwnerClanId, bool bySiege)
        {
            var capture = _invasionState.FindCapture(captureId);
            var group = _invasionState.FindGroup(groupId);
            if (capture == null || capture.Cancelled || capture.IsConfirmed || group == null || group.IsSuspended ||
                !bySiege || group.ClanId != newOwnerClanId || !group.Members.Contains(capturerId)) return false;
            if (capture.OwnershipObserved) return false;
            capture.CapturingGroupId = groupId;
            capture.CapturerId = capturerId;
            capture.PreviousOwnerClanId = previousOwnerClanId;
            capture.OwnershipObserved = true;
            TryConfirm(capture);
            return true;
        }

        internal bool ObserveAftermath(long captureId, string attackerId, string previousOwnerClanId,
            IReadOnlyDictionary<string, string> participantGroups)
        {
            var capture = _invasionState.FindCapture(captureId);
            if (capture == null || capture.Cancelled || capture.IsConfirmed || capture.StartingAttackerId != attackerId) return false;
            if (capture.HasParticipants) return false;
            capture.AftermathPreviousOwnerClanId = previousOwnerClanId;
            capture.Participants = participantGroups.Where(p => _invasionState.FindGroup(p.Value)?.Members.Contains(p.Key) == true)
                .ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
            capture.HasParticipants = true;
            TryConfirm(capture);
            return true;
        }

        private void TryConfirm(InvasionCapture capture)
        {
            if (!capture.OwnershipObserved || !capture.HasParticipants ||
                capture.PreviousOwnerClanId != capture.AftermathPreviousOwnerClanId ||
                !capture.Participants.TryGetValue(capture.CapturerId!, out string capturerGroupId) ||
                capturerGroupId != capture.CapturingGroupId) return;
            InvasionGroupState? group = _invasionState.FindGroup(capture.CapturingGroupId);
            if (group == null || group.IsSuspended) return;
            capture.Participants = capture.Participants.Where(p => p.Value == group.Id)
                .ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
            capture.IsConfirmed = true;
            if (_invasionState.FootholdIndex.TryGetValue(capture.SettlementId, out var previous))
            {
                var previousCapture = _invasionState.FindCapture(previous.CaptureId);
                if (previousCapture != null) previousCapture.Cancelled = true;
            }
            _invasionState.FootholdIndex[capture.SettlementId] = new InvasionFoothold(capture.SettlementId, group.Id, capture.Id);
        }

        internal void Prune()
        {
            foreach (var entry in _events.Where(e => PendingCapture(e.Value) == null).ToList()) _events.Remove(entry.Key);
        }

        internal IReadOnlyList<NativeResult> ExportOwnedResults() => _events.Values.Where(result => result.Verified &&
            PendingCapture(result)?.OwnershipObserved == true).ToList();

        internal bool RestoreOwnedResult(long captureId, string previousOwnerClanId, string expectedOwnerClanId,
            object? capturer, IEnumerable<object> participants)
        {
            var capture = _invasionState.FindCapture(captureId);
            if (capturer == null || capture == null || !capture.OwnershipObserved || capture.IsConfirmed || capture.Cancelled ||
                capture.PreviousOwnerClanId != previousOwnerClanId ||
                _invasionState.FindGroup(capture.CapturingGroupId)?.ClanId != expectedOwnerClanId) return false;
            _events.Add(new object(), new NativeResult(captureId, previousOwnerClanId)
            {
                Verified = true, Capturer = capturer, ExpectedOwnerClanId = expectedOwnerClanId,
                Participants = participants.ToArray()
            });
            return true;
        }

        internal sealed class NativeResult
        {
            internal NativeResult(long captureId, string previousOwnerClanId)
            { CaptureId = captureId; PreviousOwnerClanId = previousOwnerClanId; }
            internal long CaptureId { get; }
            internal string PreviousOwnerClanId { get; }
            internal bool Verified;
            internal object? Capturer;
            internal string ExpectedOwnerClanId = "";
            internal object[] Participants = new object[0];
        }
    }
}
