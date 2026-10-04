using System;
using System.Collections.Generic;
using System.Linq;

namespace CustomSpawns.Invasions
{
    // Authoritative campaign state. Engine objects and save-system attributes belong in the adapter.
    public sealed class InvasionState
    {
        internal long NextCaptureId = 1;
        // Keyed by the clan each invasion was founded for: a clan is invaded for at most once.
        internal readonly Dictionary<string, InvasionGroupState> GroupIndex = new(StringComparer.Ordinal);
        internal readonly Dictionary<long, InvasionCapture> CaptureIndex = new();
        internal readonly Dictionary<string, InvasionFoothold> FootholdIndex = new(StringComparer.Ordinal);
        public IReadOnlyCollection<InvasionGroupState> Groups => GroupIndex.Values;
        public IReadOnlyCollection<InvasionCapture> Captures => CaptureIndex.Values;
        public IReadOnlyCollection<InvasionFoothold> Footholds => FootholdIndex.Values;

        public InvasionGroupState RegisterMember(string templateId, string clanId, string partyId)
        {
            InvasionGroupState? group = FindGroupHolding(clanId);
            if (group == null)
            {
                group = new InvasionGroupState(templateId, clanId);
                GroupIndex.Add(group.ClanId, group);
            }
            foreach (InvasionGroupState other in Groups.Where(g => g.ClanId != group.ClanId)) other.Members.Remove(partyId);
            group.Members.Add(partyId);
            return group;
        }

        public InvasionGroupState? FindGroup(string? foundingClanId) =>
            foundingClanId != null && GroupIndex.TryGetValue(foundingClanId, out var group) ? group : null;

        // The invasion a clan belongs to, whether it founded it or was expanded into. There is only ever one.
        public InvasionGroupState? FindGroupHolding(string? clanId) =>
            clanId == null ? null : Groups.SingleOrDefault(group => group.HoldsClan(clanId));

        public InvasionGroupState? FindMemberGroup(string partyId) => Groups.SingleOrDefault(g => g.Members.Contains(partyId));

        // Whether the invasion a spawn template feeds is still being fought.
        public bool HasGroupOf(string? spawnTemplateId) =>
            spawnTemplateId != null && Groups.Any(group => group.SpawnTemplateId == spawnTemplateId);
        public InvasionFoothold? FindFoothold(string settlementId) => FootholdIndex.TryGetValue(settlementId, out var foothold) ? foothold : null;
        public InvasionCapture? FindCapture(long id) => CaptureIndex.TryGetValue(id, out var capture) ? capture : null;

        // Vanilla rebuilds a living invasion party under a party name no recognition path of ours matches:
        // the live group that clan still fights for is the one that has to take the party back. It takes back
        // what it lost and no more, so a group already fielding its configured budget adopts nothing; saved
        // membership can still name parties the engine has dropped, so the budget is read against a live count.
        public InvasionGroupState? FindAdoptingGroup(string? clanId, Func<InvasionGroupState, int> countLiveMembers,
            Func<InvasionGroupState, int> partyBudget)
        {
            InvasionGroupState? group = FindFightingGroup(clanId);
            return group != null && countLiveMembers(group) < partyBudget(group) ? group : null;
        }

        // The live invasion a clan fights for: adoption offers a party to it, and a refused adoption names it.
        public InvasionGroupState? FindFightingGroup(string? clanId)
        {
            InvasionGroupState? group = FindGroupHolding(clanId);
            return group != null && !group.IsSuspended && !IsGroupSpent(group) ? group : null;
        }

        // A kingdom carrying a live invasion must outlive the loss of its last settlement so the invasion can retake one.
        public bool ProtectsKingdomOf(IEnumerable<string> clanIds) =>
            clanIds.Any(clanId => FindFightingGroup(clanId) != null);

        // An invasion left with no town to hold and no party to retake one has nothing left to fight with.
        public bool IsGroupSpent(InvasionGroupState group) =>
            group.Members.Count == 0 && !Footholds.Any(foothold => foothold.GroupId == group.ClanId);

        // A group holding no town is put back on the offensive; vanilla steers its lords while it still holds one.
        public bool IsGroupWithoutTown(string? clanId)
        {
            InvasionGroupState? group = FindGroupHolding(clanId);
            return group != null && !group.IsSuspended && !Footholds.Any(foothold => foothold.GroupId == group.ClanId);
        }

        public InvasionCapture StartCapture(string settlementId, long battleStartTimeBits, string attackerLeaderId)
        {
            // A later siege can never consume an earlier siege's unconfirmed snapshot.
            foreach (var old in Captures.Where(c => c.SettlementId == settlementId && !c.IsConfirmed)) old.Cancelled = true;
            var capture = new InvasionCapture(NextCaptureId++, settlementId, battleStartTimeBits, attackerLeaderId);
            CaptureIndex.Add(capture.Id, capture);
            return capture;
        }

        public void ObserveOwner(string settlementId, string? ownerClanId)
        {
            var foothold = FindFoothold(settlementId);
            if (foothold != null && FindGroup(foothold.GroupId)?.HoldsClan(ownerClanId) != true) FootholdIndex.Remove(settlementId);
            foreach (var capture in Captures.Where(c => c.SettlementId == settlementId && c.OwnershipObserved &&
                         FindGroup(c.CapturingGroupId)?.HoldsClan(ownerClanId) != true))
                capture.Cancelled = true;
        }

        public bool CanApplyOccupation(long captureId, string currentOwnerClanId)
        {
            var capture = FindCapture(captureId);
            if (capture == null) return false;
            ObserveOwner(capture.SettlementId, currentOwnerClanId);
            return capture.IsConfirmed && capture.HasParticipants && !capture.Cancelled && !capture.OccupationApplied &&
                FindFoothold(capture.SettlementId)?.CaptureId == captureId && FindGroup(capture.CapturingGroupId)?.IsSuspended == false;
        }

        // Called only after the engine adapter successfully applies the initial occupation package.
        public bool CompleteOccupation(long captureId, string currentOwnerClanId)
        {
            if (!CanApplyOccupation(captureId, currentOwnerClanId)) return false;
            CaptureIndex[captureId].OccupationApplied = true;
            return true;
        }

        public void Reconcile(IEnumerable<string> activePartyIds, Func<string, string?> ownerOfTown,
            Func<InvasionGroupState, bool> effectsEnabled, Func<InvasionGroupState, bool> awaitsKingdomRetirement)
        {
            var active = new HashSet<string>(activePartyIds, StringComparer.Ordinal);
            foreach (var capture in Captures)
                foreach (string absent in capture.Participants.Keys.Where(id => !active.Contains(id)).ToList())
                    capture.Participants.Remove(absent);
            foreach (var foothold in Footholds.ToList()) ObserveOwner(foothold.SettlementId, ownerOfTown(foothold.SettlementId));
            foreach (var group in Groups.ToList())
            {
                group.Members.IntersectWith(active);
                group.IsSuspended = !effectsEnabled(group);
                // A spent group outlives itself while the kingdom it leaves behind still has to be retired: it is what names that kingdom.
                if (!IsGroupSpent(group) || awaitsKingdomRetirement(group)) continue;
                GroupIndex.Remove(group.ClanId);
                foreach (var capture in Captures.Where(c => c.CapturingGroupId == group.ClanId)) capture.Cancelled = true;
            }
            foreach (var capture in Captures.Where(c => c.Cancelled).ToList()) CaptureIndex.Remove(capture.Id);
        }

        public void RemoveMember(string partyId)
        {
            foreach (var group in Groups) group.Members.Remove(partyId);
            // An ID may be reused by a later spawn; it must never become a donor for an older battle.
            foreach (var capture in Captures) capture.Participants.Remove(partyId);
        }

        public void DiscardUnboundCaptures(ISet<long> reboundCaptureIds)
        {
            foreach (var capture in Captures.Where(c => !c.IsConfirmed && !reboundCaptureIds.Contains(c.Id))) capture.Cancelled = true;
        }
    }

    public sealed class InvasionGroupState
    {
        internal InvasionGroupState(string templateId, string clanId)
        { SpawnTemplateId = templateId; ClanId = clanId; Clans.Add(clanId); }
        public string SpawnTemplateId { get; }
        /**
         * The clan the invasion was founded for. It rules the kingdom the invasion leaves behind, and it
         * identifies the invasion: a clan is invaded for at most once, so nothing else has to.
         */
        public string ClanId { get; }
        // An invasion outgrows the war party limit of one clan, so it expands into further clans of its kingdom.
        internal List<string> Clans = new();
        public IReadOnlyList<string> ClanIds => Clans;
        public bool HoldsClan(string? clanId) => clanId != null && Clans.Contains(clanId);
        internal void AddClan(string clanId) { if (!Clans.Contains(clanId)) Clans.Add(clanId); }
        public bool IsSuspended { get; internal set; }
        // One clock per configured reinforcement rule. An invasion that has taken no coastal town has none.
        internal List<InvasionReinforcementSchedule> Schedules = new();
        public IReadOnlyList<InvasionReinforcementSchedule> ReinforcementSchedules => Schedules;

        internal InvasionReinforcementSchedule? FindReinforcementSchedule(string ruleId) =>
            Schedules.FirstOrDefault(schedule => schedule.RuleId == ruleId);

        internal InvasionReinforcementSchedule WindReinforcementSchedule(string ruleId, double intervalDays,
            double nextDeadlineDays)
        {
            var schedule = new InvasionReinforcementSchedule(ruleId, intervalDays, nextDeadlineDays);
            Schedules.Add(schedule);
            return schedule;
        }

        internal HashSet<string> Members = new(StringComparer.Ordinal);
        public IReadOnlyCollection<string> MemberPartyIds => Members;
    }

    // A running reinforcement clock. Its interval is kept so that a rule the configuration has since
    // dropped still spends its due slot once rather than being retried daily.
    public sealed class InvasionReinforcementSchedule
    {
        internal InvasionReinforcementSchedule(string ruleId, double intervalDays, double nextDeadlineDays)
        { RuleId = ruleId; IntervalDays = intervalDays; NextDeadlineDays = nextDeadlineDays; }
        public string RuleId { get; }
        public double IntervalDays { get; }
        public double NextDeadlineDays { get; internal set; }
    }

    public sealed class InvasionFoothold
    {
        internal InvasionFoothold(string settlementId, string groupId, long captureId)
        { SettlementId = settlementId; GroupId = groupId; CaptureId = captureId; }
        public string SettlementId { get; }
        public string GroupId { get; }
        public long CaptureId { get; }
    }

    public sealed class InvasionCapture
    {
        internal InvasionCapture(long id, string settlementId, long battleStartTimeBits, string startingAttackerId)
        { Id = id; SettlementId = settlementId; BattleStartTimeBits = battleStartTimeBits; StartingAttackerId = startingAttackerId; }
        public long Id { get; }
        public string SettlementId { get; }
        // Exact bit representation of the engine's BattleStartTime.ToSeconds; no formatted-time rounding.
        public long BattleStartTimeBits { get; }
        public string StartingAttackerId { get; }
        public string? CapturingGroupId { get; internal set; }
        public string? CapturerId { get; internal set; }
        public string? PreviousOwnerClanId { get; internal set; }
        public bool OwnershipObserved { get; internal set; }
        public string? AftermathPreviousOwnerClanId { get; internal set; }
        public bool HasParticipants { get; internal set; }
        internal Dictionary<string, string> Participants = new(StringComparer.Ordinal);
        public IReadOnlyCollection<string> ParticipantPartyIds => Participants.Keys;
        public bool IsConfirmed { get; internal set; }
        public bool Cancelled { get; internal set; }
        public bool OccupationApplied { get; internal set; }
    }
}
