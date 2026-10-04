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
        internal readonly Dictionary<string, Invasion> InvasionIndex = new(StringComparer.Ordinal);
        internal readonly Dictionary<long, InvasionCapture> CaptureIndex = new();
        internal readonly Dictionary<string, InvasionFoothold> FootholdIndex = new(StringComparer.Ordinal);
        public IReadOnlyCollection<Invasion> Invasions => InvasionIndex.Values;
        public IReadOnlyCollection<InvasionCapture> Captures => CaptureIndex.Values;
        public IReadOnlyCollection<InvasionFoothold> Footholds => FootholdIndex.Values;

        public Invasion RegisterMember(string templateId, string clanId, string partyId)
        {
            Invasion? invasion = FindInvasionHolding(clanId);
            if (invasion == null)
            {
                invasion = new Invasion(templateId, clanId);
                InvasionIndex.Add(invasion.ClanId, invasion);
            }
            foreach (Invasion other in Invasions.Where(g => g.ClanId != invasion.ClanId)) other.Members.Remove(partyId);
            invasion.Members.Add(partyId);
            return invasion;
        }

        public Invasion? FindInvasion(string? foundingClanId) =>
            foundingClanId != null && InvasionIndex.TryGetValue(foundingClanId, out var invasion) ? invasion : null;

        // The invasion a clan belongs to, whether it founded it or was expanded into. There is only ever one.
        public Invasion? FindInvasionHolding(string? clanId) =>
            clanId == null ? null : Invasions.SingleOrDefault(invasion => invasion.HoldsClan(clanId));

        public Invasion? FindInvasionOfParty(string partyId) => Invasions.SingleOrDefault(g => g.Members.Contains(partyId));

        // Whether the invasion a spawn template feeds is still being fought.
        public bool HasInvasionOf(string? spawnTemplateId) =>
            spawnTemplateId != null && Invasions.Any(invasion => invasion.SpawnTemplateId == spawnTemplateId);
        public InvasionFoothold? FindFoothold(string settlementId) => FootholdIndex.TryGetValue(settlementId, out var foothold) ? foothold : null;
        public InvasionCapture? FindCapture(long id) => CaptureIndex.TryGetValue(id, out var capture) ? capture : null;

        // Vanilla rebuilds a living invasion party under a party name no recognition path of ours matches:
        // the live invasion that clan still fights for is the one that has to take the party back. It takes back
        // what it lost and no more, so a invasion already fielding its configured budget adopts nothing; saved
        // membership can still name parties the engine has dropped, so the budget is read against a live count.
        public Invasion? FindAdoptingInvasion(string? clanId, Func<Invasion, int> countLiveMembers,
            Func<Invasion, int> partyBudget)
        {
            Invasion? invasion = FindFightingInvasion(clanId);
            return invasion != null && countLiveMembers(invasion) < partyBudget(invasion) ? invasion : null;
        }

        // The live invasion a clan fights for: adoption offers a party to it, and a refused adoption names it.
        public Invasion? FindFightingInvasion(string? clanId)
        {
            Invasion? invasion = FindInvasionHolding(clanId);
            return invasion != null && !invasion.IsSuspended && !IsInvasionSpent(invasion) ? invasion : null;
        }

        // A kingdom carrying a live invasion must outlive the loss of its last settlement so the invasion can retake one.
        public bool ProtectsKingdomOf(IEnumerable<string> clanIds) =>
            clanIds.Any(clanId => FindFightingInvasion(clanId) != null);

        // An invasion left with no town to hold and no party to retake one has nothing left to fight with.
        public bool IsInvasionSpent(Invasion invasion) =>
            invasion.Members.Count == 0 && !Footholds.Any(foothold => foothold.InvasionClanId == invasion.ClanId);

        // A invasion holding no town is put back on the offensive; vanilla steers its lords while it still holds one.
        public bool IsInvasionWithoutTown(string? clanId)
        {
            Invasion? invasion = FindInvasionHolding(clanId);
            return invasion != null && !invasion.IsSuspended && !Footholds.Any(foothold => foothold.InvasionClanId == invasion.ClanId);
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
            if (foothold != null && FindInvasion(foothold.InvasionClanId)?.HoldsClan(ownerClanId) != true) FootholdIndex.Remove(settlementId);
            foreach (var capture in Captures.Where(c => c.SettlementId == settlementId && c.OwnershipObserved &&
                         FindInvasion(c.CapturingInvasionClanId)?.HoldsClan(ownerClanId) != true))
                capture.Cancelled = true;
        }

        public bool CanApplyOccupation(long captureId, string currentOwnerClanId)
        {
            var capture = FindCapture(captureId);
            if (capture == null) return false;
            ObserveOwner(capture.SettlementId, currentOwnerClanId);
            return capture.IsConfirmed && capture.HasParticipants && !capture.Cancelled && !capture.OccupationApplied &&
                FindFoothold(capture.SettlementId)?.CaptureId == captureId && FindInvasion(capture.CapturingInvasionClanId)?.IsSuspended == false;
        }

        // Called only after the engine adapter successfully applies the initial occupation package.
        public bool CompleteOccupation(long captureId, string currentOwnerClanId)
        {
            if (!CanApplyOccupation(captureId, currentOwnerClanId)) return false;
            CaptureIndex[captureId].OccupationApplied = true;
            return true;
        }

        public void Reconcile(IEnumerable<string> activePartyIds, Func<string, string?> ownerOfTown,
            Func<Invasion, bool> effectsEnabled, Func<Invasion, bool> awaitsKingdomRetirement)
        {
            var active = new HashSet<string>(activePartyIds, StringComparer.Ordinal);
            foreach (var capture in Captures)
                foreach (string absent in capture.Participants.Keys.Where(id => !active.Contains(id)).ToList())
                    capture.Participants.Remove(absent);
            foreach (var foothold in Footholds.ToList()) ObserveOwner(foothold.SettlementId, ownerOfTown(foothold.SettlementId));
            foreach (var invasion in Invasions.ToList())
            {
                invasion.Members.IntersectWith(active);
                invasion.IsSuspended = !effectsEnabled(invasion);
                // A spent invasion outlives itself while the kingdom it leaves behind still has to be retired: it is what names that kingdom.
                if (!IsInvasionSpent(invasion) || awaitsKingdomRetirement(invasion)) continue;
                InvasionIndex.Remove(invasion.ClanId);
                foreach (var capture in Captures.Where(c => c.CapturingInvasionClanId == invasion.ClanId)) capture.Cancelled = true;
            }
            foreach (var capture in Captures.Where(c => c.Cancelled).ToList()) CaptureIndex.Remove(capture.Id);
        }

        public void RemoveMember(string partyId)
        {
            foreach (var invasion in Invasions) invasion.Members.Remove(partyId);
            // An ID may be reused by a later spawn; it must never become a donor for an older battle.
            foreach (var capture in Captures) capture.Participants.Remove(partyId);
        }

        public void DiscardUnboundCaptures(ISet<long> reboundCaptureIds)
        {
            foreach (var capture in Captures.Where(c => !c.IsConfirmed && !reboundCaptureIds.Contains(c.Id))) capture.Cancelled = true;
        }
    }

    public sealed class Invasion
    {
        internal Invasion(string templateId, string clanId)
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
        internal InvasionFoothold(string settlementId, string invasionClanId, long captureId)
        { SettlementId = settlementId; InvasionClanId = invasionClanId; CaptureId = captureId; }
        public string SettlementId { get; }
        public string InvasionClanId { get; }
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
        public string? CapturingInvasionClanId { get; internal set; }
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
