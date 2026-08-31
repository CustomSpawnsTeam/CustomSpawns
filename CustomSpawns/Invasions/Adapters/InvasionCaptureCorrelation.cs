using System.Collections.Generic;
using System.Linq;

namespace CustomSpawns.Invasions.Adapters
{
    // Transient native evidence, separate from saved rule state. Object tokens are compared by identity.
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
            object capturer, string previousOwnerClanId, string newOwnerClanId, string actualOwnerClanId)
        {
            if (nativeEvent == null || !isFinalizing || !_events.TryGetValue(nativeEvent, out var result) || !result.Verified ||
                !ReferenceEquals(result.Capturer, capturer) || result.PreviousOwnerClanId != previousOwnerClanId ||
                result.ExpectedOwnerClanId != newOwnerClanId || actualOwnerClanId != newOwnerClanId) return null;
            return PendingContext(result);
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
                context.PreviousOwnerClanId != previousOwnerClanId || context.ExpectedOwnerClanId != expectedOwnerClanId) return false;
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
