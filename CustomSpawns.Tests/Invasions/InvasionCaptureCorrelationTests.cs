using System.Collections.Generic;
using System.Linq;
using CustomSpawns.Invasions;
using CustomSpawns.Invasions.Adapters;
using NUnit.Framework;
using TaleWorlds.Core;

namespace CustomSpawns.Tests.Invasions
{
    public class InvasionCaptureCorrelationTests
    {
        private InvasionState _state = null!;
        private InvasionCaptureCorrelation _correlation = null!;
        private InvasionGroupState _group = null!;
        private readonly object _hero = new();
        private readonly object _leaderParty = new();
        private readonly object _oldDonor = new();
        private readonly object _newDonor = new();

        [SetUp]
        public void SetUp()
        {
            _state = new InvasionState();
            _group = _state.RegisterMember("invasion", "clan", "leader");
            _state.RegisterMember("invasion", "clan", "old-donor");
            _state.RegisterMember("invasion", "clan", "new-donor");
            _correlation = new InvasionCaptureCorrelation(_state);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void LateOldAftermathCannotTransplantDonorsToNewSiegeEvenWhenNewSiegeHasReachedResults(bool newResultReady)
        {
            object oldEvent = new();
            var oldCapture = Bind(oldEvent, 100);
            Record(oldEvent, 100, new[] { _leaderParty, _oldDonor });
            object newEvent = new();
            var newCapture = Bind(newEvent, 200);
            if (newResultReady) Record(newEvent, 200, new[] { _leaderParty, _newDonor });

            var resolved = _correlation.ResolveAftermath(newEvent, newResultReady, "town", "leader", "enemy", "clan",
                new[] { _leaderParty, _oldDonor });
            Assert.IsNull(resolved);
            Assert.False(newCapture.ParticipantSnapshotCaptured);
            Assert.False(newCapture.Joined);
            Assert.True(oldCapture.Cancelled);
            Assert.That(_state.Credits, Is.Empty);

            if (!newResultReady) return;
            Own(newEvent, newCapture);
            Assert.IsNull(_correlation.ResolveAftermath(null, false, "town", "leader", "enemy", "clan",
                new[] { _leaderParty, _oldDonor }));
            var correct = _correlation.ResolveAftermath(newEvent, true, "town", "leader", "enemy", "clan",
                new[] { _leaderParty, _newDonor });
            Assert.AreSame(newCapture, correct);
            ApplyAftermath(correct!, "new-donor");
            Assert.That(newCapture.ParticipantPartyIds, Is.EquivalentTo(new[] { "leader", "new-donor" }));
            Assert.AreEqual(newCapture.Id, _state.Credits.Single().ContextId);
        }

        [TestCase(true, false, false, BattleSideEnum.Attacker)]
        [TestCase(false, true, false, BattleSideEnum.Defender)]
        [TestCase(false, false, true, BattleSideEnum.Defender)]
        [TestCase(false, false, false, BattleSideEnum.None)]
        public void NativeConquestSideIncludesSallyOutBesiegersAndExcludesSiegeOutside(bool assault, bool sallyOut,
            bool blockadeSallyOut, BattleSideEnum expected)
        {
            Assert.AreEqual(expected, InvasionCampaignBehaviour.GetConqueringSide(assault, sallyOut, blockadeSallyOut));
        }

        [Test]
        public void LateOldOwnershipCannotUseTheNewSiegesInProgressHandle()
        {
            object oldEvent = new();
            Bind(oldEvent, 100);
            Record(oldEvent, 100, new[] { _leaderParty, _oldDonor });
            object newEvent = new();
            var capture = Bind(newEvent, 200);
            Assert.IsNull(_correlation.ResolveOwnership(newEvent, false, "town", _hero, "enemy", "clan", "clan"));
            Assert.False(capture.OwnershipObserved);
            Assert.IsNull(_correlation.ResolveOwnership(oldEvent, true, "town", _hero, "enemy", "clan", "clan"));
        }

        [Test]
        public void StaleCapturerFromAnotherGroupIsRejectedEvenWhenTheyParticipatedInTheCurrentWinningSide()
        {
            object oldEvent = new();
            Bind(oldEvent, 100);
            Record(oldEvent, 100, new[] { _leaderParty, _oldDonor });
            object newEvent = new();
            var capture = Bind(newEvent, 200);
            object newNativeCapturer = new();
            _correlation.RecordResult(newEvent, "town", 200, true, newNativeCapturer, "clan",
                new[] { _leaderParty, _newDonor });
            Assert.IsNull(_correlation.ResolveOwnership(newEvent, true, "town", _hero, "enemy", "clan", "clan"));
            Assert.AreSame(capture, _correlation.ResolveOwnership(newEvent, true, "town", newNativeCapturer, "enemy", "clan", "clan"));
        }

        [Test]
        public void MatchingMetadataWithoutTheBoundNativeEventCannotAuthorizeOwnership()
        {
            object nativeEvent = new();
            Bind(nativeEvent, 100);
            Record(nativeEvent, 100, new[] { _leaderParty });
            Assert.IsNull(_correlation.ResolveOwnership(new object(), true, "town", _hero, "enemy", "clan", "clan"));
            Assert.IsNull(_correlation.ResolveOwnership(null, true, "town", _hero, "enemy", "clan", "clan"));
            Assert.IsNull(_correlation.ResolveOwnership(nativeEvent, true, "town", _hero, "enemy", "clan", "different-actual-owner"));
        }

        [Test]
        public void LateOwnershipForAnotherTownCannotResolveOntoTheCapturersCurrentBattle()
        {
            object oldEvent = new();
            var oldCoastalCapture = _state.StartCapture("coastal-a", 100, "leader");
            _correlation.Bind(oldEvent, oldCoastalCapture.Id, "enemy");
            object inlandEvent = new();
            var inlandCapture = _state.StartCapture("inland-b", 200, "leader");
            _correlation.Bind(inlandEvent, inlandCapture.Id, "enemy");
            _correlation.RecordResult(inlandEvent, "inland-b", 200, true, _hero, "clan", new[] { _leaderParty });

            var stale = _correlation.ResolveOwnership(inlandEvent, true, "coastal-a", _hero, "enemy", "clan", "clan");
            Assert.IsNull(stale);
            Assert.False(inlandCapture.OwnershipObserved);
            Assert.That(_state.Credits, Is.Empty);

            var actual = _correlation.ResolveOwnership(inlandEvent, true, "inland-b", _hero, "enemy", "clan", "clan");
            Assert.AreSame(inlandCapture, actual);
            _correlation.ObserveOwnership(actual!.Id, _group.Id, "leader", "enemy", "clan", true);
            _correlation.ObserveAftermath(actual.Id, "leader", "enemy", new Dictionary<string, string> { ["leader"] = _group.Id });
            Assert.AreEqual("inland-b", _state.Credits.Single().SettlementId);
        }

        [Test]
        public void FailedOrAbandonedSiegeEndReleasesNativeReferenceAndCancelsOccurrence()
        {
            object nativeEvent = new();
            var capture = Bind(nativeEvent, 100);
            _correlation.DiscardUnsuccessfulEvent(nativeEvent);
            Assert.False(_correlation.IsBound(nativeEvent));
            Assert.True(capture.Cancelled);
            Assert.IsNull(_correlation.ResolveOwnership(nativeEvent, true, "town", _hero, "enemy", "clan", "clan"));
            _state.Reconcile(new[] { "leader" }, _ => "enemy", _ => true, _ => false);
            Assert.IsNull(_state.FindCapture(capture.Id));
        }

        [Test]
        public void WrongResultAnchorCannotBeRecordedUnderAKnownNativeHandle()
        {
            object nativeEvent = new();
            Bind(nativeEvent, 100);
            Record(nativeEvent, 200, new[] { _leaderParty });
            Assert.IsNull(_correlation.ResolveOwnership(nativeEvent, true, "town", _hero, "enemy", "clan", "clan"));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void DirectNativeResultAcceptsBothCallbackOrdersAndCompletesOnlyOnce(bool aftermathFirst)
        {
            object nativeEvent = new();
            var capture = Bind(nativeEvent, 100);
            Record(nativeEvent, 100, new[] { _leaderParty, _newDonor });
            if (!aftermathFirst) Own(nativeEvent, capture);
            var resolved = _correlation.ResolveAftermath(nativeEvent, true, "town", "leader", "enemy",
                aftermathFirst ? "enemy" : "clan", new[] { _leaderParty, _newDonor });
            Assert.AreSame(capture, resolved);
            ApplyAftermath(resolved!, "new-donor");
            if (aftermathFirst) Own(nativeEvent, capture);
            Assert.True(capture.Joined);
            Assert.True(_state.CompleteOccupation(capture.Id, "clan"));
            Assert.False(_state.CompleteOccupation(capture.Id, "clan"));
        }

        [Test]
        public void DelayedPlayerAftermathNeedsOwnershipAndExactRetainedNativeParticipants()
        {
            object nativeEvent = new();
            var capture = Bind(nativeEvent, 100);
            Record(nativeEvent, 100, new[] { _leaderParty, _newDonor });
            Assert.IsNull(_correlation.ResolveAftermath(null, false, "town", "leader", "enemy", "clan", new[] { _leaderParty, _newDonor }));
            Own(nativeEvent, capture);
            Assert.IsNull(_correlation.ResolveAftermath(null, false, "town", "leader", "enemy", "enemy", new[] { _leaderParty, _newDonor }));
            Assert.IsNull(_correlation.ResolveAftermath(null, false, "town", "leader", "enemy", "clan", new[] { _leaderParty, _oldDonor }));
            var delayed = _correlation.ResolveAftermath(null, false, "town", "leader", "enemy", "clan", new[] { _leaderParty, _newDonor });
            Assert.AreSame(capture, delayed);
            ApplyAftermath(delayed!, "new-donor");
            Assert.True(capture.Joined);
        }

        [Test]
        public void OwnedNativeResultSurvivesSaveBeforeDelayedAftermathWithoutAnyLiveEvent()
        {
            object nativeEvent = new();
            var capture = Bind(nativeEvent, 100);
            Record(nativeEvent, 100, new[] { _leaderParty, _newDonor });
            Own(nativeEvent, capture);
            var saved = _correlation.ExportOwnedResults().Single();
            _state = InvasionSaveData.FromState(_state).ToState();
            _correlation = new InvasionCaptureCorrelation(_state);
            Assert.True(_correlation.RestoreOwnedResult(saved.ContextId, saved.PreviousOwnerClanId,
                saved.ExpectedOwnerClanId, saved.Capturer, saved.Participants));
            _state.DiscardUnboundCaptures(new HashSet<long>(_correlation.ExportOwnedResults().Select(r => r.ContextId)));
            var restored = _correlation.ResolveAftermath(null, false, "town", "leader", "enemy", "clan",
                new[] { _leaderParty, _newDonor });
            Assert.IsNotNull(restored);
            ApplyAftermath(restored!, "new-donor");
            Assert.True(restored!.Joined);
            Assert.True(_state.CompleteOccupation(restored.Id, "clan"));
            Assert.False(_state.CompleteOccupation(restored.Id, "clan"));
        }

        [Test]
        public void EvidenceWithoutMatchingSavedOwnershipCannotReviveAnUnboundCapture()
        {
            object nativeEvent = new();
            var capture = Bind(nativeEvent, 100);
            Assert.False(_correlation.RestoreOwnedResult(capture.Id, "enemy", "clan", _hero, new[] { _leaderParty }));
            _correlation.ObserveOwnership(capture.Id, _group.Id, "leader", "enemy", "clan", true);
            Assert.False(_correlation.RestoreOwnedResult(capture.Id, "wrong-owner", "clan", _hero, new[] { _leaderParty }));
            Assert.False(_correlation.RestoreOwnedResult(capture.Id, "enemy", "wrong-owner", _hero, new[] { _leaderParty }));
            Assert.False(_correlation.RestoreOwnedResult(capture.Id, "enemy", "clan", null, new[] { _leaderParty }));
        }

        [Test]
        public void ReusedPartyIdentifierCannotReplaceTheNativeParticipantObject()
        {
            object nativeEvent = new();
            Bind(nativeEvent, 100);
            Record(nativeEvent, 100, new[] { _leaderParty, _newDonor });
            Assert.IsNull(_correlation.ResolveAftermath(nativeEvent, true, "town", "leader", "enemy", "clan",
                new[] { _leaderParty, new object() }));
        }

        private InvasionCaptureContext Bind(object nativeEvent, long time)
        {
            var capture = _state.StartCapture("town", time, "leader");
            _correlation.Bind(nativeEvent, capture.Id, "enemy");
            return capture;
        }
        private void Record(object nativeEvent, long time, object[] participants) =>
            _correlation.RecordResult(nativeEvent, "town", time, true, _hero, "clan", participants);
        private void Own(object nativeEvent, InvasionCaptureContext capture)
        {
            Assert.AreSame(capture, _correlation.ResolveOwnership(nativeEvent, true, "town", _hero, "enemy", "clan", "clan"));
            _correlation.ObserveOwnership(capture.Id, _group.Id, "leader", "enemy", "clan", true);
        }
        private void ApplyAftermath(InvasionCaptureContext capture, string donor) =>
            _correlation.ObserveAftermath(capture.Id, "leader", "enemy", new Dictionary<string, string>
            { ["leader"] = _group.Id, [donor] = _group.Id });
    }
}
