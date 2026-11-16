using System;
using System.Collections.Generic;
using System.Linq;
using CustomSpawns.Invasions.CapitalObjectives;

namespace CustomSpawns.Invasions
{
    // Authoritative campaign state. Engine objects and save-system attributes belong in the adapter.
    public sealed class InvasionState
    {
        public long NextCaptureId = 1;
        // Keyed by the clan each invasion was founded for: a clan is invaded for at most once.
        public readonly Dictionary<string, Invasion> InvasionIndex = new(StringComparer.Ordinal);
        public readonly Dictionary<long, InvasionCapture> CaptureIndex = new();
        public readonly Dictionary<string, InvasionFoothold> FootholdIndex = new(StringComparer.Ordinal);
        // The spawn template of every founded realm whose kingdom has fallen, once per fall: a line never replaces one.
        public readonly List<string> FallenRealmTemplateIds = new();
        public IReadOnlyCollection<Invasion> Invasions => InvasionIndex.Values;
        public IReadOnlyCollection<InvasionCapture> Captures => CaptureIndex.Values;
        public IReadOnlyCollection<InvasionFoothold> Footholds => FootholdIndex.Values;

        public string? RegisterParty(string partyId, string? templateId,
            (string? Id, bool IsEliminated, bool IsBandit) clan,
            (string? SavedRole, bool IsAdopted, bool IsPrimarySpawn) partyRole,
            Func<string, bool> hasConfiguration)
        {
            Invasion? saved = FindInvasionHolding(clan.Id);
            string role = partyRole.IsAdopted ? "Adopted" : partyRole.IsPrimarySpawn ? "Original" : "Escort";

            // Saved membership outlives configuration and clan eligibility so cleanup remains possible.
            if (saved != null)
            {
                if (saved.SpawnTemplateId != templateId)
                {
                    return null;
                }

                saved.Members.Add(partyId);
                return partyRole.SavedRole ?? role;
            }

            if (templateId == null || clan.Id == null || clan.IsEliminated || clan.IsBandit ||
                !hasConfiguration(templateId))
            {
                return null;
            }

            RegisterMember(templateId, clan.Id, partyId);
            return role;
        }

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

        /**
         * The live invasion a clan fights for. Vanilla rebuilds a living invasion party under a party name
         * no recognition path of ours matches, and this is the invasion that has to take it back: the clan
         * it belongs to is enough to say so, and how much the invasion may field is the strength ceiling's
         * question, not adoption's.
         */
        public Invasion? FindFightingInvasion(string? clanId)
        {
            Invasion? invasion = FindInvasionHolding(clanId);
            return invasion != null && !invasion.IsSuspended && !IsInvasionSpent(invasion) ? invasion : null;
        }

        // Decide before forgetting: removing the last party can spend or reconcile away its invasion.
        public string? TakeLordOfRemovedInvasionParty(string? clanId, string partyId, string? leaderId,
            Action forgetParty)
        {
            bool wasFightingAnInvasion = leaderId != null &&
                FindFightingInvasion(clanId)?.MemberPartyIds.Contains(partyId) == true;

            forgetParty();

            return wasFightingAnInvasion ? leaderId : null;
        }

        // Party-loss evidence survives ledger cleanup but never bypasses the hero protection gates.
        public bool IsRemovableInvasionLord(string? clanId, bool isMainHero, bool belongsToPlayerClan,
            bool isChild, bool isAlive, bool isLord, bool lostItsInvasionParty) =>
            !isMainHero && !belongsToPlayerClan && !isChild && isAlive && isLord &&
            (lostItsInvasionParty || FindFightingInvasion(clanId) != null);

        // Removing a clan's last living adult lord destroys its parties unless a successor is created first.
        public bool NeedsSuccessor(bool isClanLeader,
            IEnumerable<(bool IsDeparting, bool IsChild, bool IsAlive, bool IsLord)> heroes) =>
            isClanLeader && !heroes.Any(hero => !hero.IsDeparting && !hero.IsChild && hero.IsAlive && hero.IsLord);

        // Only an unregistered lord party can be one the game rebuilt for an invasion.
        public string? FindAdoptingTemplate(string? clanId, bool alreadyRegistered, bool isLordParty) =>
            alreadyRegistered || !isLordParty ? null : FindFightingInvasion(clanId)?.SpawnTemplateId;

        /**
         * A kingdom carrying a live invasion must outlive the loss of its last settlement so the invasion can
         * retake its capital. Vanilla would disband it and scatter its clans into other realms; the invasion's
         * own collapse ends it instead, once it has been too weak to come back for long enough.
         */
        public bool ProtectsKingdomOf(IEnumerable<string> clanIds) =>
            clanIds.Any(clanId => FindFightingInvasion(clanId) != null);

        // An invasion left with no town to hold and no party to retake one has nothing left to fight with.
        public bool IsInvasionSpent(Invasion invasion) =>
            invasion.Members.Count == 0 && !Footholds.Any(foothold => foothold.InvasionClanId == invasion.ClanId);

        // Configured spawn AI gets the invasion to its first capital. Once that capital exists, explicit
        // kingdom attack and defence orders own it and lord parties remain under vanilla steering.
        // Parties of no invasion are always ours.
        public bool RequiresConfiguredPartySteering(string? clanId)
        {
            Invasion? invasion = FindInvasionHolding(clanId);
            return invasion == null || (!invasion.IsSuspended && invasion.CapitalSettlementId == null);
        }

        public bool HoldsCapital(Invasion invasion) => invasion.CapitalSettlementId != null &&
            FindFoothold(invasion.CapitalSettlementId)?.InvasionClanId == invasion.ClanId;

        public Func<string, InvasionSettlementFacts, bool>? FindInitialTargetFilter(string partyId,
            string? partyClanId,
            Func<string, (bool RequiresFirstCoastalConquest, IInvasionSupplyLine? SupplyLine)> findConfiguration,
            Func<IInvasionSupplyLine, bool> factionHasSupplyBase)
        {
            Invasion? invasion = FindInvasionOfParty(partyId);

            if (invasion == null || invasion.IsSuspended || !invasion.HoldsClan(partyClanId))
            {
                return null;
            }

            // Recovering the founding capital takes priority over the optional first-supply-base rule.
            if (invasion.CapitalSettlementId != null && !HoldsCapital(invasion))
            {
                string capitalId = invasion.CapitalSettlementId;
                return (settlementId, _) => settlementId == capitalId;
            }

            var configuration = findConfiguration(invasion.SpawnTemplateId);
            IInvasionSupplyLine? supplyLine = configuration.SupplyLine;

            if (!configuration.RequiresFirstCoastalConquest || supplyLine?.DependsOnASupplyBase != true ||
                factionHasSupplyBase(supplyLine))
            {
                return null;
            }

            return (_, settlement) => supplyLine.IsSupplyBase(settlement);
        }

        public InvasionCapture StartCapture(string settlementId, long battleStartTimeBits, string attackerLeaderId)
        {
            // A later siege can never consume an earlier siege's unconfirmed snapshot.
            foreach (var old in Captures.Where(c => c.SettlementId == settlementId && !c.IsConfirmed)) old.Cancelled = true;
            var capture = new InvasionCapture(NextCaptureId++, settlementId, battleStartTimeBits, attackerLeaderId);
            CaptureIndex.Add(capture.Id, capture);
            return capture;
        }

        // Ownership and aftermath reach us as two unrelated native callbacks. Each records its half, and the
        // capture is confirmed only once both halves agree they describe the same siege.
        public bool ObserveCaptureOwnership(long captureId, string invasionClanId, string capturerId,
            string previousOwnerClanId, string newOwnerClanId, bool bySiege)
        {
            var capture = FindCapture(captureId);
            var invasion = FindInvasion(invasionClanId);

            if (capture == null || capture.Cancelled || capture.IsConfirmed || invasion == null || invasion.IsSuspended ||
                !bySiege || invasion.ClanId != newOwnerClanId || !invasion.Members.Contains(capturerId))
            {
                return false;
            }

            if (capture.OwnershipObserved)
            {
                return false;
            }

            capture.CapturingInvasionClanId = invasionClanId;
            capture.CapturerId = capturerId;
            capture.PreviousOwnerClanId = previousOwnerClanId;
            capture.OwnershipObserved = true;
            TryConfirmCapture(capture);
            return true;
        }

        public bool ObserveCaptureAftermath(long captureId, string attackerId, string previousOwnerClanId,
            IReadOnlyDictionary<string, string> participantInvasions)
        {
            var capture = FindCapture(captureId);

            if (capture == null || capture.Cancelled || capture.IsConfirmed || capture.StartingAttackerId != attackerId)
            {
                return false;
            }

            if (capture.HasParticipants)
            {
                return false;
            }

            capture.AftermathPreviousOwnerClanId = previousOwnerClanId;
            capture.Participants = participantInvasions.Where(p => FindInvasion(p.Value)?.Members.Contains(p.Key) == true)
                .ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
            capture.HasParticipants = true;
            TryConfirmCapture(capture);
            return true;
        }

        private void TryConfirmCapture(InvasionCapture capture)
        {
            if (!capture.OwnershipObserved || !capture.HasParticipants ||
                capture.PreviousOwnerClanId != capture.AftermathPreviousOwnerClanId ||
                !capture.Participants.TryGetValue(capture.CapturerId!, out string capturerInvasionClanId) ||
                capturerInvasionClanId != capture.CapturingInvasionClanId)
            {
                return;
            }

            Invasion? invasion = FindInvasion(capture.CapturingInvasionClanId);

            if (invasion == null || invasion.IsSuspended)
            {
                return;
            }

            capture.Participants = capture.Participants.Where(p => p.Value == invasion.ClanId)
                .ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
            capture.IsConfirmed = true;

            if (FootholdIndex.TryGetValue(capture.SettlementId, out var previous))
            {
                var previousCapture = FindCapture(previous.CaptureId);

                if (previousCapture != null)
                {
                    previousCapture.Cancelled = true;
                }
            }

            FootholdIndex[capture.SettlementId] = new InvasionFoothold(capture.SettlementId, invasion.ClanId, capture.Id);

            // The first settlement the invasion ever holds is the one it founds its kingdom on.
            invasion.CapitalSettlementId ??= capture.SettlementId;
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

        // Once the settlement is safe, this reconciles ownership and may cancel captures.
        public bool IsOccupationReady(long captureId, string currentOwnerClanId,
            (bool HasActiveGarrison, bool InBattle, bool UnderSiege) settlement,
            IEnumerable<(string PartyId, bool IsActive, bool IsInBattle)> parties)
        {
            // Unsafe native state must not reconcile ownership or consume a pending occupation.
            if (!settlement.HasActiveGarrison || settlement.InBattle || settlement.UnderSiege ||
                !CanApplyOccupation(captureId, currentOwnerClanId))
            {
                return false;
            }

            InvasionCapture capture = FindCapture(captureId)!;
            return !parties.Any(party => party.IsActive && capture.Participants.ContainsKey(party.PartyId) &&
                party.IsInBattle);
        }

        // Called only after the engine adapter successfully applies the initial occupation package.
        public bool CompleteOccupation(long captureId, string currentOwnerClanId)
        {
            if (!CanApplyOccupation(captureId, currentOwnerClanId)) return false;
            InvasionCapture capture = CaptureIndex[captureId];
            capture.OccupationApplied = true;
            // Applying an occupation puts the capturing clan in a kingdom, founding one if it has none.
            FindInvasion(capture.CapturingInvasionClanId)!.IsRealmFounded = true;
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
        public Invasion(string templateId, string clanId)
        { SpawnTemplateId = templateId; ClanId = clanId; Clans.Add(clanId); }
        public string SpawnTemplateId { get; }
        /**
         * The clan the invasion was founded for. It rules the kingdom the invasion leaves behind, and it
         * identifies the invasion: a clan is invaded for at most once, so nothing else has to.
         */
        public string ClanId { get; }
        // An invasion outgrows the war party limit of one clan, so it expands into further clans of its kingdom.
        public List<string> Clans = new();
        public IReadOnlyList<string> ClanIds => Clans;
        public bool HoldsClan(string? clanId) => clanId != null && Clans.Contains(clanId);
        public void AddClan(string clanId) { if (!Clans.Contains(clanId)) Clans.Add(clanId); }
        public bool IsSuspended { get; set; }

        /**
         * The clan that leads the invasion: its founder while it lives. A founder that dies out passes the
         * invasion on, as a realm passes its crown, to the clan of the invasion that rules in its place, or failing
         * that to any clan of the invasion still alive. Only when none is left is there nobody to lead it.
         */
        public string? FindLeadingClan(Func<string, bool> isLiving, Func<string, string?> findRulingClan)
        {
            if (isLiving(ClanId))
            {
                return ClanId;
            }

            string? survivor = ClanIds.FirstOrDefault(isLiving);

            if (survivor == null)
            {
                return null;
            }

            string? ruler = findRulingClan(survivor);

            return ruler != null && HoldsClan(ruler) && isLiving(ruler) ? ruler : survivor;
        }

        /**
         * The settlement the invasion founded its kingdom on. Kept once the invasion loses it, because losing
         * it is what puts the invasion back on the offensive: later conquests are the kingdom's own business,
         * but the capital it landed for has to be retaken.
         */
        public string? CapitalSettlementId { get; set; }

        /**
         * Whether the invasion has founded its kingdom on its capital. Taking the capital is not enough: the
         * kingdom is founded once the capital's occupation is applied, an hour or more later.
         */
        public bool IsRealmFounded { get; set; }

        /**
         * Whether the invasion still has a realm to fight for. It needs none until it founds its kingdom; from
         * then on its realm is a kingdom one of its clans rules. Once that is gone the invasion is over: the
         * clans it expanded into are vanilla's, and a kingdom they join is not the invasion reborn.
         */
        public bool HasRealm(bool rulesAKingdom) => !IsRealmFounded || rulesAKingdom;

        public InvasionCapitalObjective? CapitalObjective { get; private set; }
        /**
         * When the next food convoy is due at the capital, in campaign days, or null while none has been
         * scheduled. It is its own clock on purpose: supplying the capital has nothing to do with how often
         * the invasion is reinforced.
         */
        public double? NextFoodConvoyDays { get; set; }

        // Since when, in campaign days, the invasion has been too weak to retake a fief, or null while it is not.
        public double? WeakSinceDays { get; set; }

        // The fiefs its realm held when last looked at, so the loss of one is noticed.
        public int? LastFiefCount { get; set; }

        // The founder keeps the crown when another clan of its invasion is elected to rule.
        public bool ShouldRestoreCrown(string kingdomId, string? rulingClanId, bool kingdomIsEliminated,
            (bool IsEliminated, bool HasLeader, string? KingdomId) founder) =>
            !kingdomIsEliminated && HoldsClan(rulingClanId) && rulingClanId != ClanId &&
            !founder.IsEliminated && founder.HasLeader &&
            (founder.KingdomId == null || founder.KingdomId == kingdomId);

        public string? FindClanWithRoom(
            Func<string, (bool IsEliminated, Func<(int WarParties, int WarPartyLimit)> PartyCapacity)?> findClan)
        {
            foreach (string clanId in ClanIds)
            {
                var clan = findClan(clanId);

                if (!clan.HasValue || clan.Value.IsEliminated)
                {
                    continue;
                }

                var capacity = clan.Value.PartyCapacity();

                if (capacity.WarParties < capacity.WarPartyLimit)
                {
                    return clanId;
                }
            }

            return null;
        }

        // A newly founded clan already has a leader; do not create a second lord beside him.
        public bool CanReuseLeader(bool isActive, bool hasParty) => isActive && !hasParty;

        public int CountTownsHeld(
            IEnumerable<(string ClanId, Func<int?> KingdomTowns, Func<int> ClanTowns)> clans)
        {
            var invasionClans = clans.Where(clan => HoldsClan(clan.ClanId)).ToList();
            int? kingdomTowns = invasionClans.Select(clan => clan.KingdomTowns())
                .FirstOrDefault(towns => towns.HasValue);

            // The kingdom's towns raise the ceiling even when held by clans outside this invasion.
            return kingdomTowns ?? invasionClans.Sum(clan => clan.ClanTowns());
        }

        public int CountLivingLords(
            IEnumerable<(string ClanId, IEnumerable<(bool IsAlive, bool IsChild, bool IsLord)> Heroes)> clans) =>
            clans.Where(clan => HoldsClan(clan.ClanId))
                .Sum(clan => clan.Heroes.Count(hero => hero.IsAlive && !hero.IsChild && hero.IsLord));

        public void BeginCapitalDefence(string settlementId, double nowDays,
            string? siegeEpisodeId = null)
        {
            if (CapitalObjective?.Kind == InvasionCapitalObjectiveKind.Defence &&
                CapitalObjective.TargetSettlementId == settlementId &&
                CapitalObjective.DefenceSiegeEpisodeId == siegeEpisodeId)
            {
                return;
            }

            CapitalObjective = new InvasionCapitalObjective(InvasionCapitalObjectiveKind.Defence,
                settlementId, nowDays, siegeEpisodeId);
        }

        public void BeginCapitalAttack(string settlementId, double nowDays)
        {
            if (CapitalObjective?.Kind == InvasionCapitalObjectiveKind.Attack &&
                CapitalObjective.DefenceSiegeEpisodeId == null)
            {
                return;
            }

            CapitalObjective = new InvasionCapitalObjective(InvasionCapitalObjectiveKind.Attack,
                settlementId, nowDays);
        }

        public void AdvanceCapitalObjective()
        {
            RequiredCapitalObjective().MarkReady();
        }

        public void RetargetCapitalAttack(string settlementId, double nowDays)
        {
            RequiredCapitalObjective().RetargetForAttack(settlementId, nowDays);
        }

        public void MarkCapitalSiegeStarted()
        {
            RequiredCapitalObjective().MarkBesieging();
        }

        public void WaitForViableCapitalTarget()
        {
            RequiredCapitalObjective().WaitForViableTarget();
        }

        public void TimeOutCapitalAttack()
        {
            RequiredCapitalObjective().TimeOutAttack();
        }

        public void AssignCapitalObjectiveCommander(string partyId)
        {
            RequiredCapitalObjective().AssignCommander(partyId);
        }

        public void ClearCapitalObjectiveCommander()
        {
            CapitalObjective?.ClearCommander();
        }

        public void RestoreCapitalObjective(InvasionCapitalObjective? objective)
        {
            CapitalObjective = objective;
        }

        public void CompleteCapitalObjective()
        {
            CapitalObjective = null;
        }

        public void RelocateCapitalAfterObjectiveCapture(string settlementId)
        {
            InvasionCapitalObjective objective = RequiredCapitalObjective();

            if (objective.Kind != InvasionCapitalObjectiveKind.Attack ||
                objective.TargetSettlementId != settlementId)
            {
                throw new InvalidOperationException("Only the captured attack target can become the capital.");
            }

            CapitalSettlementId = settlementId;
            CapitalObjective = null;
        }

        private InvasionCapitalObjective RequiredCapitalObjective()
        {
            return CapitalObjective ?? throw new InvalidOperationException("The invasion has no capital objective.");
        }

        // One clock per configured reinforcement rule. An invasion that holds no supply base has none.
        public List<InvasionReinforcementSchedule> Schedules = new();
        public IReadOnlyList<InvasionReinforcementSchedule> ReinforcementSchedules => Schedules;

        public InvasionReinforcementSchedule? FindReinforcementSchedule(string ruleId) =>
            Schedules.FirstOrDefault(schedule => schedule.RuleId == ruleId);

        public InvasionReinforcementSchedule WindReinforcementSchedule(string ruleId, double intervalDays,
            double nextDeadlineDays, int wavesSent = 0)
        {
            var schedule = new InvasionReinforcementSchedule(ruleId, intervalDays, nextDeadlineDays, wavesSent);
            Schedules.Add(schedule);
            return schedule;
        }

        public HashSet<string> Members = new(StringComparer.Ordinal);
        public IReadOnlyCollection<string> MemberPartyIds => Members;
    }

    // A running reinforcement clock. Its interval is kept so that a rule the configuration has since
    // dropped still spends its due slot once rather than being retried daily.
    public sealed class InvasionReinforcementSchedule
    {
        public InvasionReinforcementSchedule(string ruleId, double intervalDays, double nextDeadlineDays,
            int wavesSent = 0)
        { RuleId = ruleId; IntervalDays = intervalDays; NextDeadlineDays = nextDeadlineDays; WavesSent = wavesSent; }
        public string RuleId { get; }
        public double IntervalDays { get; }
        public double NextDeadlineDays { get; set; }
        // Waves this clock has actually delivered, counted against a rule that is allowed only so many.
        public int WavesSent { get; set; }
    }

    public sealed class InvasionFoothold
    {
        public InvasionFoothold(string settlementId, string invasionClanId, long captureId)
        { SettlementId = settlementId; InvasionClanId = invasionClanId; CaptureId = captureId; }
        public string SettlementId { get; }
        public string InvasionClanId { get; }
        public long CaptureId { get; }
    }

    public sealed class InvasionCapture
    {
        public InvasionCapture(long id, string settlementId, long battleStartTimeBits, string startingAttackerId)
        { Id = id; SettlementId = settlementId; BattleStartTimeBits = battleStartTimeBits; StartingAttackerId = startingAttackerId; }
        public long Id { get; }
        public string SettlementId { get; }
        // Exact bit representation of the engine's BattleStartTime.ToSeconds; no formatted-time rounding.
        public long BattleStartTimeBits { get; }
        public string StartingAttackerId { get; }
        public string? CapturingInvasionClanId { get; set; }
        public string? CapturerId { get; set; }
        public string? PreviousOwnerClanId { get; set; }
        public bool OwnershipObserved { get; set; }
        public string? AftermathPreviousOwnerClanId { get; set; }
        public bool HasParticipants { get; set; }
        public Dictionary<string, string> Participants = new(StringComparer.Ordinal);
        public IReadOnlyCollection<string> ParticipantPartyIds => Participants.Keys;
        public bool IsConfirmed { get; set; }
        public bool Cancelled { get; set; }
        public bool OccupationApplied { get; set; }

        // Donor filtering only; the caller must first confirm the occupation is ready.
        public bool CanAbsorbEscort(string partyId, bool isActive, Func<bool> isEscort) =>
            isActive && Participants.ContainsKey(partyId) && isEscort();
    }
}
