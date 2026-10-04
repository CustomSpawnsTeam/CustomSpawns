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
        public void OriginalsAndEscortsShareGroupAndDeadlineButOtherClansAreIsolated()
        {
            _group.WindReinforcementSchedule("infantry", 7, 12);
            Assert.AreSame(_group, _state.RegisterMember("invasion", "clan", "second-original"));
            Assert.AreEqual(12, _state.RegisterMember("invasion", "clan", "escort").ReinforcementSchedules
                .Single(schedule => schedule.RuleId == "infantry").NextDeadlineDays);
            Assert.AreNotSame(_group, _state.RegisterMember("invasion", "other-clan", "foreign"));
            Assert.That(_group.MemberPartyIds, Is.EquivalentTo(new[] { "capturer", "second-original", "escort" }));
        }

        [Test]
        public void AnInvasionThatExpandedIntoAnotherClanKeepsOneGroupAcrossASave()
        {
            _group.AddClan("overflow-clan");

            Assert.AreSame(_group, _state.RegisterMember("invasion", "overflow-clan", "overflow-party"));
            Assert.True(_group.HoldsClan("overflow-clan"));
            Assert.False(_group.HoldsClan("other-clan"));

            InvasionGroupState restored = RoundTrip().Groups.Single();
            Assert.AreEqual("clan", restored.ClanId);
            Assert.That(restored.ClanIds, Is.EqualTo(new[] { "clan", "overflow-clan" }));
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
            Assert.AreEqual(1, _state.Footholds.Count);
            Assert.True(_state.CompleteOccupation(capture.Id, "clan"));
            Assert.False(_state.CompleteOccupation(capture.Id, "clan"));
            _state = RoundTrip();
            Ownership(capture);
            Aftermath(capture);
            Assert.False(_state.CanApplyOccupation(capture.Id, "clan"));
            Assert.AreEqual(1, _state.Footholds.Count);
        }

        [TestCase("ally", "capturer", true)]
        [TestCase("clan", "nonmember", true)]
        [TestCase("clan", "capturer", false)]
        public void AlliedNonmemberAndPeacefulAcquisitionsNeverReceiveFoothold(string owner, string capturer, bool siege)
        {
            var capture = Start();
            Aftermath(capture);
            Assert.False(Correlation.ObserveOwnership(capture.Id, _group.ClanId, capturer, "enemy", owner, siege));
            Assert.That(_state.Footholds, Is.Empty);
        }

        [Test]
        public void CapturerMustBelongToTheCopiedParticipantsNotMerelyToTheGroup()
        {
            var other = _state.RegisterMember("invasion", "clan", "other-party");
            var capture = Start();
            Ownership(capture);
            Correlation.ObserveAftermath(capture.Id, "capturer", "enemy", new Dictionary<string, string> { ["other-party"] = other.ClanId });
            Assert.False(capture.IsConfirmed);
            Assert.That(_state.Footholds, Is.Empty);
        }

        [Test]
        public void EmptyParticipantListIsPresentButCannotAuthorizeAnUnattributedCapture()
        {
            var capture = Start();
            Ownership(capture);
            Assert.False(capture.HasParticipants);
            Assert.False(_state.CanApplyOccupation(capture.Id, "clan"));
            Correlation.ObserveAftermath(capture.Id, "capturer", "enemy", new Dictionary<string, string>());
            Assert.True(capture.HasParticipants);
            Assert.False(capture.IsConfirmed);
            Assert.False(_state.CompleteOccupation(capture.Id, "clan"));
            Assert.True(RoundTrip().FindCapture(capture.Id)!.HasParticipants);
        }

        [Test]
        public void EnemyTakeoverCancelsPendingAndPeacefulReturnNeverRevivesFootholdButNewSiegeDoes()
        {
            var first = Conquer();
            _state.ObserveOwner("town", "enemy");
            Assert.That(_state.Footholds, Is.Empty);
            Assert.False(_state.CanApplyOccupation(first.Id, "clan"));
            _state.ObserveOwner("town", "clan");
            Assert.That(_state.Footholds, Is.Empty);
            var second = Conquer(200);
            Assert.AreNotEqual(first.Id, second.Id);
            Assert.True(_state.CanApplyOccupation(second.Id, "clan"));
            Assert.AreEqual(second.Id, _state.Footholds.Single().CaptureId);
        }

        [Test]
        public void SiegeAloneDoesNotRevokeExistingFoothold()
        {
            var capture = Conquer();
            _state.StartCapture("town", 200, "enemy");
            Assert.AreEqual(capture.Id, _state.Footholds.Single().CaptureId);
            _state.Reconcile(new[] { "capturer" }, _ => "clan", _ => true, _ => false);
            Assert.AreEqual(capture.Id, _state.Footholds.Single().CaptureId);
        }

        [Test]
        public void DestroyingOriginalKeepsFootholdAndOnlyEmptyGroupStartsFresh()
        {
            Conquer();
            _state.Reconcile(new string[0], _ => "clan", _ => true, _ => false);
            Assert.AreSame(_group, _state.Groups.Single());
            _state.Reconcile(new string[0], _ => "enemy", _ => true, _ => false);
            Assert.That(_state.Groups, Is.Empty);
            var fresh = _state.RegisterMember("invasion", "clan", "new-invasion");
            Assert.AreNotSame(_group, fresh);
        }

        [Test]
        public void SurvivingEscortKeepsGroupWithoutTerritory()
        {
            _state.RegisterMember("invasion", "clan", "escort");
            _state.Reconcile(new[] { "escort" }, _ => null, _ => true, _ => false);
            Assert.AreSame(_group, _state.Groups.Single());
            Assert.That(_group.MemberPartyIds, Is.EquivalentTo(new[] { "escort" }));
        }

        [Test]
        public void MissingConfigurationOrEliminatedClanSuspendsEffectsButPreservesSafeCleanupState()
        {
            var capture = Conquer();
            _state.Reconcile(new[] { "capturer" }, _ => "clan", _ => false, _ => false);
            Assert.True(_group.IsSuspended);
            Assert.False(_state.CanApplyOccupation(capture.Id, "clan"));
            Assert.AreEqual(1, _state.Footholds.Count);
            _state = RoundTrip();
            _state.Reconcile(new string[0], _ => "enemy", _ => false, _ => false);
            Assert.That(_state.Footholds, Is.Empty);
            Assert.That(_state.Groups, Is.Empty);
        }

        [Test]
        public void ConfirmedPendingSurvivesSaveWithNoLiveMapEventAndNoDuplicatePackage()
        {
            var capture = Conquer();
            _group.WindReinforcementSchedule("infantry", 7, 45.5);
            _state = RoundTrip();
            _state.DiscardUnboundCaptures(new HashSet<long>());
            var saved = _state.FindCapture(capture.Id)!;
            Assert.True(saved.IsConfirmed);
            Assert.False(saved.Cancelled);
            Assert.AreEqual(45.5, _state.Groups.Single().ReinforcementSchedules.Single().NextDeadlineDays);
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
            Assert.False(later.IsConfirmed);
            Assert.True(first.Cancelled);
            Assert.That(_state.Footholds, Is.Empty);
            Aftermath(later);
            Assert.True(later.IsConfirmed);
            Assert.AreEqual(later.Id, _state.Footholds.Single().CaptureId);
        }

        [Test]
        public void WrongAftermathLeaderOrOwnerCannotJoin()
        {
            var capture = Start();
            Ownership(capture);
            Assert.False(Correlation.ObserveAftermath(capture.Id, "other-leader", "enemy", Participants()));
            Assert.True(Correlation.ObserveAftermath(capture.Id, "capturer", "wrong-previous-owner", Participants()));
            Assert.False(capture.IsConfirmed);
        }

        [Test]
        public void LegacySaveHasNoRetroactiveFootholdAndNewIdentityPreservesPrimaryAccounting()
        {
            var empty = new InvasionSaveData().ToState();
            empty.RegisterMember("invasion", "clan", "existing");
            empty.Reconcile(new[] { "existing" }, _ => "clan", _ => true, _ => false);
            Assert.That(empty.Footholds, Is.Empty);
            var party = new CsPartyData("primary", "primary", true, null);
            Assert.IsNull(party.InvasionRole);
            party.SetInvasionRole("Original");
            Assert.True(party.IsPrimarySpawn);
            Assert.IsNull(party.SupportedParty);
            Assert.AreEqual(7, FieldId(typeof(CsPartyData), "_invasionRole"));
        }

        [Test]
        public void DestroyedDonorIdentityCannotBeReusedByANewSpawn()
        {
            var capture = Conquer();
            _state.RemoveMember("capturer");
            _state.RegisterMember("invasion", "clan", "capturer");
            Assert.That(capture.ParticipantPartyIds, Is.Empty);
            Assert.True(capture.HasParticipants);
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

        [Test]
        public void LiveInvasionProtectsItsKingdomButAForeignOrSuspendedOneDoesNot()
        {
            Assert.True(_state.ProtectsKingdomOf(new[] { "other-clan", "clan" }));
            Assert.False(_state.ProtectsKingdomOf(new[] { "other-clan" }));

            _state.Reconcile(new[] { "capturer" }, _ => "clan", _ => false, _ => false);
            Assert.False(_state.ProtectsKingdomOf(new[] { "clan" }));
        }

        [Test]
        public void ASpentInvasionStopsProtectingItsKingdomAndIsHeldUntilTheKingdomRetires()
        {
            Conquer();
            _state.Reconcile(new string[0], _ => "enemy", _ => true, _ => true);

            Assert.AreSame(_group, _state.Groups.Single());
            Assert.False(_state.ProtectsKingdomOf(new[] { "clan" }));

            _state.Reconcile(new string[0], _ => "enemy", _ => true, _ => false);
            Assert.That(_state.Groups, Is.Empty);
        }

        [Test]
        public void AGroupIsBackOnTheOffensiveOnlyOnceItHoldsNoTownAtAll()
        {
            Assert.True(_state.IsGroupWithoutTown(_group.ClanId));
            Assert.False(_state.IsGroupWithoutTown("absent"));

            Conquer();
            Assert.False(_state.IsGroupWithoutTown(_group.ClanId));

            _state.ObserveOwner("town", "enemy");
            Assert.True(_state.IsGroupWithoutTown(_group.ClanId));

            _state.Reconcile(new[] { "capturer" }, _ => "clan", _ => false, _ => false);
            Assert.False(_state.IsGroupWithoutTown(_group.ClanId));
        }

        [Test]
        public void LosingOneOfTwoTownsLeavesTheGroupHoldingAFoothold()
        {
            Conquer();
            var second = _state.StartCapture("second-town", 300, "capturer");
            Correlation.ObserveOwnership(second.Id, _group.ClanId, "capturer", "enemy", "clan", true);
            Aftermath(second);
            Assert.That(_state.Footholds.Count, Is.EqualTo(2));

            _state.ObserveOwner("town", "enemy");
            Assert.False(_state.IsGroupWithoutTown(_group.ClanId));
        }

        [Test]
        public void OnlyALiveGroupTakesBackARebuiltLordPartyOfItsClans()
        {
            _group.AddClan("overflow-clan");
            Assert.AreSame(_group, Adopting("clan"));
            Assert.AreSame(_group, Adopting("overflow-clan"));
            Assert.IsNull(Adopting("other-clan"));
            Assert.IsNull(Adopting(null));

            _group.IsSuspended = true;
            Assert.IsNull(Adopting("clan"));

            _group.IsSuspended = false;
            _state.RemoveMember("capturer");
            Assert.IsNull(Adopting("clan"));
        }

        [Test]
        public void AGroupHoldingATownTakesBackARebuiltLordPartyAfterLosingEveryParty()
        {
            Conquer();
            _state.RemoveMember("capturer");

            Assert.AreSame(_group, Adopting("clan"));
        }

        [Test]
        public void AGroupTakesNoLordBackWhileItAlreadyFieldsItsBudgetedParties()
        {
            _state.RegisterMember("invasion", "clan", "second");

            Assert.IsNull(_state.FindAdoptingGroup("clan", LiveMembers, _ => 2));
            Assert.AreSame(_group, _state.FindAdoptingGroup("clan", LiveMembers, _ => 3));

            _state.RemoveMember("second");
            Assert.AreSame(_group, _state.FindAdoptingGroup("clan", LiveMembers, _ => 2));
        }

        [Test]
        public void MembershipTheEngineNoLongerBacksDoesNotSpendTheBudget()
        {
            _state.RegisterMember("invasion", "clan", "dropped-before-reconciliation");

            Assert.AreSame(_group,
                _state.FindAdoptingGroup("clan", group => group.MemberPartyIds.Count(id => id == "capturer"), _ => 2));
        }

        private InvasionGroupState? Adopting(string? clanId) => _state.FindAdoptingGroup(clanId, LiveMembers, _ => int.MaxValue);
        private static int LiveMembers(InvasionGroupState group) => group.MemberPartyIds.Count;
        private InvasionCapture Start(long ticks = 100) => _state.StartCapture("town", ticks, "capturer");
        private Dictionary<string, string> Participants() => new() { ["capturer"] = _group.ClanId };
        private void Ownership(InvasionCapture c) => Correlation.ObserveOwnership(c.Id, _group.ClanId, "capturer", "enemy", "clan", true);
        private void Aftermath(InvasionCapture c) => Correlation.ObserveAftermath(c.Id, "capturer", "enemy", Participants());
        private InvasionCapture Conquer(long ticks = 100) { var c = Start(ticks); Ownership(c); Aftermath(c); return c; }
        private InvasionCaptureCorrelation Correlation => new(_state);
        private InvasionState RoundTrip() => InvasionSaveData.FromState(_state).ToState();
        private static short FieldId(System.Type type, string field) => type.GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetCustomAttribute<SaveableFieldAttribute>()!.LocalSaveId;
    }
}
