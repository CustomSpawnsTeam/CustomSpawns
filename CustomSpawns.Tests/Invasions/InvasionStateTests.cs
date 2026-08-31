using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CustomSpawns.Invasions;
using CustomSpawns.Invasions.Adapters;
using CustomSpawns.Spawn;
using NUnit.Framework;
using TaleWorlds.SaveSystem;

namespace CustomSpawns.Tests.Invasions
{
    public class InvasionStateTests
    {
        private InvasionState _state = null!;
        private InvasionGroupState _group = null!;

        [SetUp]
        public void SetUp()
        {
            _state = new InvasionState();
            _group = _state.RegisterMember("invasion", "clan", "capturer");
        }

        [Test]
        public void OriginalsAndEscortsShareGroupAndDeadlineButOtherClansAndTemplatesAreIsolated()
        {
            _group.NextReinforcementDeadlineDays = 12;
            Assert.AreSame(_group, _state.RegisterMember("invasion", "clan", "second-original"));
            Assert.AreEqual(12, _state.RegisterMember("invasion", "clan", "escort").NextReinforcementDeadlineDays);
            Assert.AreNotSame(_group, _state.RegisterMember("invasion", "other-clan", "foreign"));
            Assert.AreNotSame(_group, _state.RegisterMember("other-template", "clan", "other-group"));
            Assert.That(_group.MemberPartyIds, Is.EquivalentTo(new[] { "capturer", "second-original", "escort" }));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void CaptureObservationsJoinInEitherOrderAndReplayDoesNotRepeatOccupation(bool aftermathFirst)
        {
            var capture = Start();
            if (aftermathFirst) Aftermath(capture);
            Ownership(capture);
            if (!aftermathFirst) Aftermath(capture);
            Assert.True(_state.CanApplyOccupation(capture.Id, "clan"));
            Assert.AreEqual(1, _state.Credits.Count);
            Assert.True(_state.CompleteOccupation(capture.Id, "clan"));
            Assert.False(_state.CompleteOccupation(capture.Id, "clan"));
            _state = RoundTrip();
            Ownership(capture);
            Aftermath(capture);
            Assert.False(_state.CanApplyOccupation(capture.Id, "clan"));
            Assert.AreEqual(1, _state.Credits.Count);
        }

        [Test]
        public void SameClanLeaderFromOtherGroupDoesNotStealCapturersCreditOrDonateTroops()
        {
            var leaderGroup = _state.RegisterMember("second-invasion", "clan", "leader");
            _state.RegisterMember("invasion", "clan", "escort");
            var capture = _state.StartCapture("town", 100, "leader");
            _state.ObserveAftermath(capture.Id, "leader", "enemy", new Dictionary<string, string>
            {
                ["capturer"] = _group.Id, ["escort"] = _group.Id, ["leader"] = leaderGroup.Id
            });
            Ownership(capture);
            Assert.AreEqual(_group.Id, _state.Credits.Single().GroupId);
            Assert.That(capture.ParticipantPartyIds, Is.EquivalentTo(new[] { "capturer", "escort" }));
            Assert.True(_group.HasFirstCoastalConquest);
            Assert.True(_state.FindMemberGroup("escort")!.HasFirstCoastalConquest);
            Assert.False(leaderGroup.HasFirstCoastalConquest);
            Assert.True(_state.CompleteOccupation(capture.Id, "clan"));
            Assert.False(_state.CompleteOccupation(capture.Id, "clan"));
        }

        [TestCase("ally", "capturer", true)]
        [TestCase("clan", "nonmember", true)]
        [TestCase("clan", "capturer", false)]
        public void AlliedNonmemberAndPeacefulAcquisitionsNeverReceiveCredit(string owner, string capturer, bool siege)
        {
            var capture = Start();
            Aftermath(capture);
            Assert.False(_state.ObserveOwnership(capture.Id, _group.Id, capturer, "enemy", owner, siege, true));
            Assert.That(_state.Credits, Is.Empty);
        }

        [Test]
        public void CapturerMustBelongToCopiedParticipantGroupNotMerelySameClan()
        {
            var other = _state.RegisterMember("other", "clan", "other-party");
            var capture = Start();
            Ownership(capture);
            _state.ObserveAftermath(capture.Id, "capturer", "enemy", new Dictionary<string, string> { ["other-party"] = other.Id });
            Assert.False(capture.Joined);
            Assert.That(_state.Credits, Is.Empty);
        }

        [Test]
        public void EmptyParticipantSnapshotIsPresentButCannotAuthorizeAnUnattributedCapture()
        {
            var capture = Start();
            Ownership(capture);
            Assert.False(capture.ParticipantSnapshotCaptured);
            Assert.False(_state.CanApplyOccupation(capture.Id, "clan"));
            _state.ObserveAftermath(capture.Id, "capturer", "enemy", new Dictionary<string, string>());
            Assert.True(capture.ParticipantSnapshotCaptured);
            Assert.False(capture.Joined);
            Assert.False(_state.CompleteOccupation(capture.Id, "clan"));
            Assert.True(RoundTrip().FindCapture(capture.Id)!.ParticipantSnapshotCaptured);
        }

        [Test]
        public void EnemyTakeoverCancelsPendingAndPeacefulReturnNeverRevivesCreditButNewSiegeDoes()
        {
            var first = Conquer();
            _state.ObserveOwner("town", "enemy");
            Assert.That(_state.Credits, Is.Empty);
            Assert.False(_state.CanApplyOccupation(first.Id, "clan"));
            _state.ObserveOwner("town", "clan");
            Assert.That(_state.Credits, Is.Empty);
            Assert.True(_group.HasFirstCoastalConquest);
            var second = Conquer(200);
            Assert.AreNotEqual(first.Id, second.Id);
            Assert.True(_state.CanApplyOccupation(second.Id, "clan"));
            Assert.AreEqual(second.Id, _state.Credits.Single().ContextId);
        }

        [Test]
        public void SiegeAloneDoesNotRevokeExistingCredit()
        {
            var capture = Conquer();
            _state.StartCapture("town", 200, "enemy");
            Assert.AreEqual(capture.Id, _state.Credits.Single().ContextId);
            _state.Reconcile(new[] { "capturer" }, _ => "clan", _ => true);
            Assert.AreEqual(capture.Id, _state.Credits.Single().ContextId);
        }

        [Test]
        public void DestroyingOriginalKeepsFootholdAndOnlyEmptyGroupStartsFresh()
        {
            Conquer();
            _state.Reconcile(new string[0], _ => "clan", _ => true);
            Assert.AreSame(_group, _state.Groups.Single());
            _state.Reconcile(new string[0], _ => "enemy", _ => true);
            Assert.That(_state.Groups, Is.Empty);
            var fresh = _state.RegisterMember("invasion", "clan", "new-invasion");
            Assert.AreNotEqual(_group.Id, fresh.Id);
            Assert.False(fresh.HasFirstCoastalConquest);
        }

        [Test]
        public void SurvivingEscortKeepsGroupWithoutTerritory()
        {
            _state.RegisterMember("invasion", "clan", "escort");
            _state.Reconcile(new[] { "escort" }, _ => null, _ => true);
            Assert.AreSame(_group, _state.Groups.Single());
            Assert.That(_group.MemberPartyIds, Is.EquivalentTo(new[] { "escort" }));
        }

        [Test]
        public void MissingConfigurationOrEliminatedClanSuspendsEffectsButPreservesSafeCleanupState()
        {
            var capture = Conquer();
            _state.Reconcile(new[] { "capturer" }, _ => "clan", _ => false);
            Assert.True(_group.IsSuspended);
            Assert.False(_state.CanApplyOccupation(capture.Id, "clan"));
            Assert.AreEqual(1, _state.Credits.Count);
            _state = RoundTrip();
            _state.Reconcile(new string[0], _ => "enemy", _ => false);
            Assert.That(_state.Credits, Is.Empty);
            Assert.That(_state.Groups, Is.Empty);
        }

        [Test]
        public void JoinedPendingSurvivesSaveWithNoLiveMapEventAndNoDuplicatePackage()
        {
            var capture = Conquer();
            _group.NextReinforcementDeadlineDays = 45.5;
            _state = RoundTrip();
            _state.DiscardUnboundCaptures(new HashSet<long>());
            var saved = _state.FindCapture(capture.Id)!;
            Assert.True(saved.Joined);
            Assert.False(saved.Cancelled);
            Assert.AreEqual(45.5, _state.Groups.Single().NextReinforcementDeadlineDays);
            Assert.That(saved.ParticipantPartyIds, Is.EquivalentTo(new[] { "capturer" }));
            Assert.True(_state.CompleteOccupation(saved.Id, "clan"));
            Assert.False(RoundTrip().CompleteOccupation(saved.Id, "clan"));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void IncompleteLoadRequiresUniqueLiveAnchor(bool rebound)
        {
            var capture = Start();
            Ownership(capture);
            _state = RoundTrip();
            _state.DiscardUnboundCaptures(rebound ? new HashSet<long> { capture.Id } : new HashSet<long>());
            Aftermath(capture);
            Assert.AreEqual(rebound, _state.CanApplyOccupation(capture.Id, "clan"));
        }

        [Test]
        public void PreviousCaptureSnapshotCannotJoinLaterOccurrence()
        {
            var first = Start();
            Aftermath(first);
            var later = Start(200);
            Ownership(later);
            Ownership(first);
            Assert.False(later.Joined);
            Assert.True(first.Cancelled);
            Assert.That(_state.Credits, Is.Empty);
            Aftermath(later);
            Assert.True(later.Joined);
            Assert.AreEqual(later.Id, _state.Credits.Single().ContextId);
        }

        [Test]
        public void WrongAftermathLeaderOrOwnerCannotJoin()
        {
            var capture = Start();
            Ownership(capture);
            Assert.False(_state.ObserveAftermath(capture.Id, "other-leader", "enemy", Participants()));
            Assert.True(_state.ObserveAftermath(capture.Id, "capturer", "wrong-previous-owner", Participants()));
            Assert.False(capture.Joined);
        }

        [Test]
        public void LegacySaveHasNoRetroactiveCreditAndNewIdentityPreservesPrimaryAccounting()
        {
            var empty = new InvasionSaveData().ToState();
            empty.RegisterMember("invasion", "clan", "existing");
            empty.Reconcile(new[] { "existing" }, _ => "clan", _ => true);
            Assert.That(empty.Credits, Is.Empty);
            Assert.False(empty.Groups.Single().HasFirstCoastalConquest);
            var party = new CsPartyData("primary", "primary", true, null);
            Assert.IsNull(party.InvasionGroupId);
            Assert.IsNull(party.InvasionRole);
            party.SetInvasionIdentity("1", "Original");
            Assert.True(party.IsPrimarySpawn);
            Assert.IsNull(party.SupportedParty);
            Assert.AreEqual(6, FieldId(typeof(CsPartyData), "_invasionGroupId"));
            Assert.AreEqual(7, FieldId(typeof(CsPartyData), "_invasionRole"));
        }

        [Test]
        public void DestroyedDonorIdentityCannotBeReusedByANewSpawn()
        {
            var capture = Conquer();
            _state.RemoveMember("capturer");
            _state.RegisterMember("invasion", "clan", "capturer");
            Assert.That(capture.ParticipantPartyIds, Is.Empty);
            Assert.True(capture.ParticipantSnapshotCaptured);
            Assert.True(_state.CanApplyOccupation(capture.Id, "clan"));
            Assert.That(RoundTrip().FindCapture(capture.Id)!.ParticipantPartyIds, Is.Empty);
        }

        [Test]
        public void SaveMappingCopiesParticipantAndMembershipCollections()
        {
            var capture = Conquer();
            var saved = InvasionSaveData.FromState(_state);
            _state.RegisterMember("invasion", "clan", "late-member");
            capture.Participants.Clear();
            var restored = saved.ToState();
            Assert.That(restored.Groups.Single().MemberPartyIds, Does.Not.Contain("late-member"));
            Assert.That(restored.FindCapture(capture.Id)!.ParticipantPartyIds, Contains.Item("capturer"));
        }

        private InvasionCaptureContext Start(long ticks = 100) => _state.StartCapture("town", ticks, "capturer");
        private Dictionary<string, string> Participants() => new() { ["capturer"] = _group.Id };
        private void Ownership(InvasionCaptureContext c) => _state.ObserveOwnership(c.Id, _group.Id, "capturer", "enemy", "clan", true, true);
        private void Aftermath(InvasionCaptureContext c) => _state.ObserveAftermath(c.Id, "capturer", "enemy", Participants());
        private InvasionCaptureContext Conquer(long ticks = 100) { var c = Start(ticks); Ownership(c); Aftermath(c); return c; }
        private InvasionState RoundTrip() => InvasionSaveData.FromState(_state).ToState();
        private static short FieldId(System.Type type, string field) => type.GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetCustomAttribute<SaveableFieldAttribute>()!.LocalSaveId;
    }
}
