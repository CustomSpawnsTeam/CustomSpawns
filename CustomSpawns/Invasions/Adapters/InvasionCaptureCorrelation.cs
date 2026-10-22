using System;
using System.Collections.Generic;
using System.Linq;

namespace CustomSpawns.Invasions.Adapters
{
    // Native evidence for capture observations. Object tokens are compared by identity.
    public sealed class InvasionCaptureCorrelation
    {
        private readonly InvasionState _invasionState;
        private readonly Dictionary<object, NativeResult> _events = new();

        public InvasionCaptureCorrelation(InvasionState state) => _invasionState = state;
        public bool IsBound(object nativeEvent) => _events.ContainsKey(nativeEvent);

        public void Bind(object nativeEvent, long captureId, string previousOwnerClanId)
        {
            _events.Add(nativeEvent, new NativeResult(captureId, previousOwnerClanId));
        }

        public void RecordResult(object nativeEvent, string settlementId, long battleStartTimeBits,
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

        public InvasionCapture? ResolveOwnership(object? nativeEvent, bool isFinalizing,
            string settlementId, object capturer, string previousOwnerClanId, string newOwnerClanId, string actualOwnerClanId)
        {
            if (nativeEvent == null || !isFinalizing || !_events.TryGetValue(nativeEvent, out var result) || !result.Verified ||
                !ReferenceEquals(result.Capturer, capturer) || result.PreviousOwnerClanId != previousOwnerClanId ||
                result.ExpectedOwnerClanId != newOwnerClanId || actualOwnerClanId != newOwnerClanId) return null;
            var capture = PendingCapture(result);
            return capture?.SettlementId == settlementId ? capture : null;
        }

        public void DiscardUnsuccessfulEvent(object nativeEvent)
        {
            if (!_events.TryGetValue(nativeEvent, out var result) || result.Verified) return;
            var capture = _invasionState.FindCapture(result.CaptureId);
            if (capture != null) capture.Cancelled = true;
            _events.Remove(nativeEvent);
        }

        public InvasionCapture? ResolveAftermath(object? nativeEvent, bool isFinalizing,
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

        public void Prune()
        {
            foreach (var entry in _events.Where(e => PendingCapture(e.Value) == null).ToList()) _events.Remove(entry.Key);
        }

        public IReadOnlyList<NativeResult> ExportOwnedResults() => _events.Values.Where(result => result.Verified &&
            PendingCapture(result)?.OwnershipObserved == true).ToList();

        public bool RestoreOwnedResult(long captureId, string previousOwnerClanId, string expectedOwnerClanId,
            object? capturer, IEnumerable<object> participants)
        {
            var capture = _invasionState.FindCapture(captureId);
            if (capturer == null || capture == null || !capture.OwnershipObserved || capture.IsConfirmed || capture.Cancelled ||
                capture.PreviousOwnerClanId != previousOwnerClanId ||
                _invasionState.FindInvasion(capture.CapturingInvasionClanId)?.ClanId != expectedOwnerClanId) return false;
            _events.Add(new object(), new NativeResult(captureId, previousOwnerClanId)
            {
                Verified = true, Capturer = capturer, ExpectedOwnerClanId = expectedOwnerClanId,
                Participants = participants.ToArray()
            });
            return true;
        }

        public sealed class NativeResult
        {
            public NativeResult(long captureId, string previousOwnerClanId)
            { CaptureId = captureId; PreviousOwnerClanId = previousOwnerClanId; }
            public long CaptureId { get; }
            public string PreviousOwnerClanId { get; }
            public bool Verified;
            public object? Capturer;
            public string ExpectedOwnerClanId = "";
            public object[] Participants = new object[0];
        }
    }
}
