using System;
using System.Collections.Generic;
using System.Linq;

namespace CustomSpawns.Invasions
{
    // Authoritative campaign state. Engine objects and save-system attributes belong in the adapter.
    public sealed class InvasionState
    {
        internal long NextGroupId = 1;
        internal long NextCaptureId = 1;
        internal readonly Dictionary<string, InvasionGroupState> GroupIndex = new(StringComparer.Ordinal);
        internal readonly Dictionary<long, InvasionCaptureContext> CaptureIndex = new();
        internal readonly Dictionary<string, InvasionTownCredit> CreditIndex = new(StringComparer.Ordinal);
        public IReadOnlyCollection<InvasionGroupState> Groups => GroupIndex.Values;
        public IReadOnlyCollection<InvasionCaptureContext> Captures => CaptureIndex.Values;
        public IReadOnlyCollection<InvasionTownCredit> Credits => CreditIndex.Values;

        public InvasionGroupState RegisterMember(string templateId, string clanId, string partyId)
        {
            InvasionGroupState? group = Groups.SingleOrDefault(g => g.SpawnTemplateId == templateId && g.ClanId == clanId);
            if (group == null)
            {
                group = new InvasionGroupState((NextGroupId++).ToString(System.Globalization.CultureInfo.InvariantCulture), templateId, clanId);
                GroupIndex.Add(group.Id, group);
            }
            foreach (InvasionGroupState other in Groups.Where(g => g.Id != group.Id)) other.Members.Remove(partyId);
            group.Members.Add(partyId);
            return group;
        }

        public InvasionGroupState? FindGroup(string? id) => id != null && GroupIndex.TryGetValue(id, out var group) ? group : null;
        public InvasionGroupState? FindMemberGroup(string partyId) => Groups.SingleOrDefault(g => g.Members.Contains(partyId));
        public InvasionTownCredit? FindCredit(string settlementId) => CreditIndex.TryGetValue(settlementId, out var credit) ? credit : null;
        public InvasionCaptureContext? FindCapture(long id) => CaptureIndex.TryGetValue(id, out var capture) ? capture : null;

        // A kingdom carrying a live invasion must outlive the loss of its last settlement so the invasion can retake one.
        public bool ProtectsKingdomOf(IEnumerable<string> clanIds) =>
            clanIds.Any(clanId => Groups.Any(group => !group.IsSuspended && !IsGroupSpent(group) && group.ClanId == clanId));

        // An invasion left with no town to hold and no party to retake one has nothing left to fight with.
        public bool IsGroupSpent(InvasionGroupState group) =>
            group.Members.Count == 0 && !Credits.Any(credit => credit.GroupId == group.Id);

        // A group holding no town is put back on the offensive; vanilla steers its lords while it still holds one.
        public bool IsGroupWithoutTown(string? groupId)
        {
            InvasionGroupState? group = FindGroup(groupId);
            return group != null && !group.IsSuspended && !Credits.Any(credit => credit.GroupId == group.Id);
        }

        public InvasionCaptureContext StartCapture(string settlementId, long battleStartTimeBits, string attackerLeaderId)
        {
            // A later siege can never consume an earlier siege's unjoined snapshot.
            foreach (var old in Captures.Where(c => c.SettlementId == settlementId && !c.Joined)) old.Cancelled = true;
            var context = new InvasionCaptureContext(NextCaptureId++, settlementId, battleStartTimeBits, attackerLeaderId);
            CaptureIndex.Add(context.Id, context);
            return context;
        }

        public void ObserveOwner(string settlementId, string? ownerClanId)
        {
            var credit = FindCredit(settlementId);
            if (credit != null && FindGroup(credit.GroupId)?.ClanId != ownerClanId) CreditIndex.Remove(settlementId);
            foreach (var context in Captures.Where(c => c.SettlementId == settlementId && c.OwnershipObserved &&
                         FindGroup(c.OwnershipGroupId)?.ClanId != ownerClanId))
                context.Cancelled = true;
        }

        public bool CanApplyOccupation(long contextId, string currentOwnerClanId)
        {
            var context = FindCapture(contextId);
            if (context == null) return false;
            ObserveOwner(context.SettlementId, currentOwnerClanId);
            return context.Joined && context.ParticipantSnapshotCaptured && !context.Cancelled && !context.OccupationApplied &&
                FindCredit(context.SettlementId)?.ContextId == contextId && FindGroup(context.OwnershipGroupId)?.IsSuspended == false;
        }

        // Called only after the engine adapter successfully applies the initial occupation package.
        public bool CompleteOccupation(long contextId, string currentOwnerClanId)
        {
            if (!CanApplyOccupation(contextId, currentOwnerClanId)) return false;
            CaptureIndex[contextId].OccupationApplied = true;
            return true;
        }

        public void Reconcile(IEnumerable<string> activePartyIds, Func<string, string?> ownerOfTown,
            Func<InvasionGroupState, bool> effectsEnabled, Func<InvasionGroupState, bool> awaitsKingdomRetirement)
        {
            var active = new HashSet<string>(activePartyIds, StringComparer.Ordinal);
            foreach (var context in Captures)
                foreach (string absent in context.Participants.Keys.Where(id => !active.Contains(id)).ToList())
                    context.Participants.Remove(absent);
            foreach (var credit in Credits.ToList()) ObserveOwner(credit.SettlementId, ownerOfTown(credit.SettlementId));
            foreach (var group in Groups.ToList())
            {
                group.Members.IntersectWith(active);
                group.IsSuspended = !effectsEnabled(group);
                // A spent group outlives itself while the kingdom it leaves behind still has to be retired: it is what names that kingdom.
                if (!IsGroupSpent(group) || awaitsKingdomRetirement(group)) continue;
                GroupIndex.Remove(group.Id);
                foreach (var context in Captures.Where(c => c.OwnershipGroupId == group.Id)) context.Cancelled = true;
            }
            foreach (var context in Captures.Where(c => c.Cancelled).ToList()) CaptureIndex.Remove(context.Id);
        }

        public void RemoveMember(string partyId)
        {
            foreach (var group in Groups) group.Members.Remove(partyId);
            // An ID may be reused by a later spawn; it must never become a donor for an older battle.
            foreach (var context in Captures) context.Participants.Remove(partyId);
        }

        public void DiscardUnboundCaptures(ISet<long> reboundContextIds)
        {
            foreach (var context in Captures.Where(c => !c.Joined && !reboundContextIds.Contains(c.Id))) context.Cancelled = true;
        }
    }

    public sealed class InvasionGroupState
    {
        internal InvasionGroupState(string id, string templateId, string clanId) { Id = id; SpawnTemplateId = templateId; ClanId = clanId; }
        public string Id { get; }
        public string SpawnTemplateId { get; }
        public string ClanId { get; }
        public bool IsSuspended { get; internal set; }
        // Zero means no deadline has been established yet. Scheduling is a separate feature.
        public double NextReinforcementDeadlineDays { get; internal set; }
        // Retained after configuration removal so a due slot is consumed once rather than retried daily.
        public double ReinforcementIntervalDays { get; internal set; }
        internal HashSet<string> Members = new(StringComparer.Ordinal);
        public IReadOnlyCollection<string> MemberPartyIds => Members;
    }

    public sealed class InvasionTownCredit
    {
        internal InvasionTownCredit(string settlementId, string groupId, long contextId)
        { SettlementId = settlementId; GroupId = groupId; ContextId = contextId; }
        public string SettlementId { get; }
        public string GroupId { get; }
        public long ContextId { get; }
    }

    public sealed class InvasionCaptureContext
    {
        internal InvasionCaptureContext(long id, string settlementId, long battleStartTimeBits, string startingAttackerId)
        { Id = id; SettlementId = settlementId; BattleStartTimeBits = battleStartTimeBits; StartingAttackerId = startingAttackerId; }
        public long Id { get; }
        public string SettlementId { get; }
        // Exact bit representation of the engine's BattleStartTime.ToSeconds; no formatted-time rounding.
        public long BattleStartTimeBits { get; }
        public string StartingAttackerId { get; }
        public string? OwnershipGroupId { get; internal set; }
        public string? CapturerId { get; internal set; }
        public string? PreviousOwnerClanId { get; internal set; }
        public bool OwnershipObserved { get; internal set; }
        public string? AftermathPreviousOwnerClanId { get; internal set; }
        public bool ParticipantSnapshotCaptured { get; internal set; }
        internal Dictionary<string, string> Participants = new(StringComparer.Ordinal);
        public IReadOnlyCollection<string> ParticipantPartyIds => Participants.Keys;
        public bool Joined { get; internal set; }
        public bool Cancelled { get; internal set; }
        public bool OccupationApplied { get; internal set; }
    }
}
