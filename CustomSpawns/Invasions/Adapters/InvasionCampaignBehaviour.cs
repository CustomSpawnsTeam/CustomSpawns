using System;
using System.Collections.Generic;
using System.Linq;
using CustomSpawns.AI;
using CustomSpawns.AI.Offensives;
using CustomSpawns.Data.Dao;
using CustomSpawns.Data.Dto;
using CustomSpawns.Spawn;
using CustomSpawns.UtilityBehaviours;
using NLog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace CustomSpawns.Invasions.Adapters
{
    public sealed class InvasionCampaignBehaviour : CampaignBehaviorBase, IInvasionFootholdQuery,
        IInvasionLandingQuery, IInvasionReinforcementQuery
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly SpawnDao _spawns;
        private readonly DynamicSpawnData _parties;
        private readonly SpawnGroupService _spawnGroups;
        private readonly InvasionCoastalTownResolver _coastalTownResolver = new();
        private readonly Func<int, int> _randomIndex;
        private InvasionCaptureCorrelation _correlation;
        private InvasionReinforcements _reinforcements;
        private bool _initialized;
        public InvasionState State { get; private set; } = new();

        internal InvasionCampaignBehaviour(SpawnDao spawns, DynamicSpawnData parties, SpawnGroupService spawnGroups,
            SaveInitialiser initialiser, Func<int, int>? randomIndex = null)
        {
            _spawns = spawns;
            _parties = parties;
            _spawnGroups = spawnGroups;
            _randomIndex = randomIndex ?? MBRandom.RandomInt;
            _correlation = new InvasionCaptureCorrelation(State);
            _reinforcements = new InvasionReinforcements(State, this, _randomIndex);
            // DynamicSpawnData registers its discovery callback first; scheduling/AI are composed later.
            initialiser.RunCallbackOnFirstCampaignTick(Initialize);
            parties.PartyRegistered += RegisterParty;
            parties.PartyRemoved += RemoveParty;
        }

        public override void RegisterEvents()
        {
            CampaignEvents.MapEventStarted.AddNonSerializedListener(this, OnMapEventStarted);
            CampaignEvents.MapEventEnded.AddNonSerializedListener(this, OnMapEventEnded);
            CampaignEvents.OnSiegeAftermathAppliedEvent.AddNonSerializedListener(this, OnAftermath);
            CampaignEvents.OnSettlementOwnerChangedEvent.AddNonSerializedListener(this, OnOwnerChanged);
            CampaignEvents.CanKingdomBeDiscontinuedEvent.AddNonSerializedListener(this, CanKingdomBeDiscontinued);
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, OnHourlyTick);
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, ScheduleReinforcements);
        }

        public override void SyncData(IDataStore dataStore)
        {
            InvasionSaveData? saved = dataStore.IsSaving ? InvasionSaveData.FromState(State) : null;
            if (saved != null)
                saved.OwnedCaptureEvidence = _correlation.ExportOwnedResults().Select(result => new InvasionCaptureEvidenceSaveData
                {
                    ContextId = result.ContextId, PreviousOwnerClanId = result.PreviousOwnerClanId,
                    ExpectedOwnerClanId = result.ExpectedOwnerClanId, Capturer = result.Capturer as Hero,
                    Participants = result.Participants.Cast<MobileParty>().ToList()
                }).ToList();
            dataStore.SyncData("_invasionState", ref saved);
            if (!dataStore.IsLoading) return;
            State = saved?.ToState() ?? new InvasionState();
            _correlation = new InvasionCaptureCorrelation(State);
            _reinforcements = new InvasionReinforcements(State, this, _randomIndex);
            foreach (var evidence in saved?.OwnedCaptureEvidence ?? new List<InvasionCaptureEvidenceSaveData>())
                _correlation.RestoreOwnedResult(evidence.ContextId, evidence.PreviousOwnerClanId,
                    evidence.ExpectedOwnerClanId, evidence.Capturer, (evidence.Participants ?? new List<MobileParty>()).Cast<object>());
            _initialized = false;
            _coastalTownResolver.ClearCache();
        }

        private void Initialize()
        {
            if (_initialized) return;
            _initialized = true;
            Reconcile();
            foreach (MobileParty party in MobileParty.All.Where(p => p.IsActive))
            {
                CsPartyData? data = _parties.GetDynamicSpawnData(party);
                if (data != null) RegisterParty(party, data);
            }
            // Only a unique live native anchor can restore an incomplete occurrence.
            var liveEvents = Settlement.All.Select(s => s.Party.MapEvent).Where(e => e != null).Distinct().ToList();
            var rebound = new HashSet<long>(_correlation.ExportOwnedResults().Select(result => result.ContextId));
            foreach (var context in State.Captures.Where(c => !c.Cancelled && !c.Joined))
            {
                if (rebound.Contains(context.Id)) continue;
                var matches = liveEvents.Where(e => e!.MapEventSettlement?.StringId == context.SettlementId &&
                    BitConverter.DoubleToInt64Bits(e.BattleStartTime.ToSeconds) == context.BattleStartTimeBits).ToList();
                int savedMatches = State.Captures.Count(c => !c.Cancelled && !c.Joined &&
                    c.SettlementId == context.SettlementId && c.BattleStartTimeBits == context.BattleStartTimeBits);
                if (matches.Count != 1 || savedMatches != 1 || _correlation.IsBound(matches[0]!)) continue;
                _correlation.Bind(matches[0]!, context.Id, context.PreviousOwnerClanId ??
                    context.AftermathPreviousOwnerClanId ?? matches[0]!.MapEventSettlement.OwnerClan?.StringId ?? "");
                rebound.Add(context.Id);
            }
            State.DiscardUnboundCaptures(rebound);
        }

        private void RegisterParty(MobileParty party, CsPartyData data)
        {
            // Saved membership survives removed configurations so cleanup remains possible.
            InvasionGroupState? saved = State.FindGroup(data.InvasionGroupId);
            if (saved != null && saved.ClanId == party.ActualClan?.StringId && saved.SpawnTemplateId == data.OwningSpawnTemplateId)
            {
                saved.Members.Add(party.StringId);
                data.SetInvasionIdentity(saved.Id, data.InvasionRole ?? InvasionRoleFor(data));
                return;
            }
            string? templateId = data.OwningSpawnTemplateId;
            Clan? clan = party.ActualClan;
            if (templateId == null || clan == null || clan.IsEliminated || clan.IsBanditFaction ||
                _spawns.FindByPartyTemplateId(templateId)?.Invasion == null)
            {
                data.SetInvasionIdentity(null, null);
                return;
            }
            var group = State.RegisterMember(templateId, clan.StringId, party.StringId);
            data.SetInvasionIdentity(group.Id, InvasionRoleFor(data));
        }

        private void RemoveParty(MobileParty party)
        {
            State.RemoveMember(party.StringId);
            Reconcile();
        }

        private static string InvasionRoleFor(CsPartyData data) => data.IsIndependentReinforcement ? "Reinforcement" :
            data.IsPrimarySpawn ? "Original" : "Escort";

        private void Reconcile()
        {
            State.Reconcile(MobileParty.All.Where(p => p.IsActive).Select(p => p.StringId),
                id => Settlement.Find(id)?.OwnerClan?.StringId,
                group => GetConfiguration(group) != null && Clan.FindFirst(c => c.StringId == group.ClanId)?.IsEliminated == false,
                group => FindRetirableKingdom(group) != null);
            _correlation.Prune();
        }

        private void OnHourlyTick()
        {
            Reconcile();
            RetireSpentInvasions();
            ApplyReadyOccupations();
        }

        public InvasionData? GetConfiguration(InvasionGroupState group) => _spawns.FindByPartyTemplateId(group.SpawnTemplateId)?.Invasion;

        public bool HasPersistentInvasionGroup(CsPartyData partyData) => State.FindGroup(partyData.InvasionGroupId) != null;

        Func<Settlement, bool>? IInvasionLandingQuery.GetInitialCoastalTargetFilter(MobileParty party)
        {
            Initialize();
            InvasionGroupState? group = State.FindMemberGroup(party.StringId);
            if (group == null || group.IsSuspended ||
                party.ActualClan?.StringId != group.ClanId) return null;
            InvasionData? configuration = GetConfiguration(group);
            if (configuration?.RequiresFirstCoastalConquest != true) return null;
            bool factionHasCoastalTown = Settlement.All.Any(settlement => settlement.IsTown &&
                settlement.MapFaction == party.MapFaction && _coastalTownResolver.IsCoastalTown(settlement));
            return factionHasCoastalTown ? null : _coastalTownResolver.IsCoastalTown;
        }

        IReadOnlyList<MobileParty> IInvasionLandingQuery.GetLandingParties(MobileParty party)
        {
            Initialize();
            InvasionGroupState? group = State.FindMemberGroup(party.StringId);
            if (group == null) return new[] { party };
            return MobileParty.All.Where(member => member.IsActive && group.Members.Contains(member.StringId))
                .ToList();
        }

        private void OnMapEventStarted(MapEvent mapEvent, PartyBase attacker, PartyBase defender)
        {
            Initialize();
            Settlement? town = mapEvent.MapEventSettlement;
            BattleSideEnum conqueringSide = GetConqueringSide(mapEvent.IsSiegeAssault, mapEvent.IsSallyOut, mapEvent.IsBlockadeSallyOut);
            if (conqueringSide == BattleSideEnum.None || town?.IsTown != true) return;
            MobileParty? besieger = mapEvent.GetMapEventSide(conqueringSide).LeaderParty?.MobileParty;
            if (besieger == null) return;
            if (_correlation.IsBound(mapEvent)) return;
            var context = State.StartCapture(town.StringId, BitConverter.DoubleToInt64Bits(mapEvent.BattleStartTime.ToSeconds), besieger.StringId);
            _correlation.Bind(mapEvent, context.Id, town.OwnerClan?.StringId ?? "");
        }

        private void OnMapEventEnded(MapEvent mapEvent)
        {
            if (!IsSuccessfulFinalization(mapEvent))
            {
                _correlation.DiscardUnsuccessfulEvent(mapEvent);
                return;
            }
            // Native ownership follows this event synchronously; player aftermath may follow much later.
            // Preserve immutable result evidence while native participant sides still exist.
            RecordNativeResult(mapEvent);
        }

        private void RecordNativeResult(MapEvent? mapEvent)
        {
            if (mapEvent?.MapEventSettlement == null || !IsSuccessfulFinalization(mapEvent)) return;
            BattleSideEnum conqueringSide = GetConqueringSide(mapEvent.IsSiegeAssault, mapEvent.IsSallyOut, mapEvent.IsBlockadeSallyOut);
            PartyBase? capturerParty = mapEvent.GetMapEventSide(conqueringSide).LeaderParty;
            _correlation.RecordResult(mapEvent, mapEvent.MapEventSettlement.StringId,
                BitConverter.DoubleToInt64Bits(mapEvent.BattleStartTime.ToSeconds), true,
                capturerParty?.Owner, capturerParty?.MobileParty?.MapFaction?.Leader?.Clan?.StringId ?? "",
                mapEvent.PartiesOnSide(conqueringSide).Where(p => p.Party.IsMobile)
                    .Select(p => (object)p.Party.MobileParty));
        }

        private static bool IsSuccessfulFinalization(MapEvent? mapEvent) => mapEvent != null &&
            GetConqueringSide(mapEvent.IsSiegeAssault, mapEvent.IsSallyOut, mapEvent.IsBlockadeSallyOut) != BattleSideEnum.None &&
            mapEvent.WinningSide == GetConqueringSide(mapEvent.IsSiegeAssault, mapEvent.IsSallyOut, mapEvent.IsBlockadeSallyOut) && mapEvent.IsFinalized;

        // Native sally-out attackers are the garrison; the besiegers are the defenders.
        internal static BattleSideEnum GetConqueringSide(bool isSiegeAssault, bool isSallyOut, bool isBlockadeSallyOut) =>
            isSiegeAssault ? BattleSideEnum.Attacker : isSallyOut || isBlockadeSallyOut ? BattleSideEnum.Defender : BattleSideEnum.None;

        private void OnAftermath(MobileParty attacker, Settlement settlement, SiegeAftermathAction.SiegeAftermath aftermath,
            Clan previousOwner, Dictionary<MobileParty, float> contributions)
        {
            Initialize();
            MapEvent? mapEvent = settlement.Party.MapEvent ?? attacker.MapEvent;
            RecordNativeResult(mapEvent);
            var context = _correlation.ResolveAftermath(mapEvent, IsSuccessfulFinalization(mapEvent),
                settlement.StringId, attacker.StringId, previousOwner?.StringId ?? "", settlement.OwnerClan?.StringId ?? "",
                contributions.Keys.Cast<object>());
            if (context == null) return;
            var participants = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (MobileParty party in contributions.Keys)
            {
                var group = State.FindMemberGroup(party.StringId);
                if (group != null && group.ClanId == party.ActualClan?.StringId) participants[party.StringId] = group.Id;
            }
            // Copy immediately: the native dictionary is cleared after the callback.
            _correlation.ObserveAftermath(context.Id, attacker.StringId, previousOwner?.StringId ?? "", participants);
            StartReinforcementSchedule(context.Id);
        }

        private void OnOwnerChanged(Settlement settlement, bool openToClaim, Hero newOwner, Hero oldOwner,
            Hero capturer, ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail detail)
        {
            Initialize();
            InvasionTownCredit? previousCredit = State.FindCredit(settlement.StringId);
            State.ObserveOwner(settlement.StringId, settlement.OwnerClan?.StringId);
            if (previousCredit != null && State.FindCredit(settlement.StringId) == null)
                Logger.Info("Invasion coastal credit lost: group=" + previousCredit.GroupId + " town=" + settlement.StringId);
            if (!settlement.IsTown || detail != ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail.BySiege) return;
            MobileParty? party = capturer?.PartyBelongedTo;
            if (party == null) return;
            var group = State.FindMemberGroup(party.StringId);
            MapEvent? mapEvent = settlement.Party.MapEvent ?? party.MapEvent;
            if (!ReferenceEquals(mapEvent?.MapEventSettlement, settlement)) return;
            RecordNativeResult(mapEvent);
            var context = _correlation.ResolveOwnership(mapEvent, IsSuccessfulFinalization(mapEvent), settlement.StringId, capturer,
                oldOwner?.Clan?.StringId ?? "", newOwner?.Clan?.StringId ?? "", settlement.OwnerClan?.StringId ?? "");
            if (group == null || context == null || party.ActualClan?.StringId != group.ClanId) return;
            InvasionData? configuration = GetConfiguration(group);
            if (configuration == null) return;
            _correlation.ObserveOwnership(context.Id, group.Id, party.StringId, oldOwner?.Clan?.StringId ?? "",
                newOwner?.Clan?.StringId ?? "", true);
            StartReinforcementSchedule(context.Id);
        }

        bool IInvasionFootholdQuery.IsGroupWithoutTown(string? invasionGroupId)
        {
            Initialize();
            return State.IsGroupWithoutTown(invasionGroupId);
        }

        private void CanKingdomBeDiscontinued(Kingdom kingdom, ref bool result)
        {
            Initialize();
            if (State.ProtectsKingdomOf(kingdom.Clans.Select(clan => clan.StringId))) result = false;
        }

        /**
         * Vanilla only asks whether a kingdom can be discontinued when a settlement changes hands or when
         * the player switches kingdom, so the kingdom an invasion was given would outlive the invasion
         * itself once its protection stops applying. The missing trigger is supplied here; the rule that
         * decides and the actions that end it are vanilla's own.
         */
        private void RetireSpentInvasions()
        {
            foreach (InvasionGroupState group in State.Groups.ToList())
            {
                if (!State.IsGroupSpent(group)) continue;
                Kingdom? kingdom = FindRetirableKingdom(group);
                if (kingdom == null) continue;
                Logger.Info("Invasion spent: group=" + group.Id + " kingdom=" + kingdom.StringId);
                DiscontinueKingdom(kingdom);
            }
        }

        private static Kingdom? FindRetirableKingdom(InvasionGroupState group)
        {
            Kingdom? kingdom = Clan.FindFirst(clan => clan.StringId == group.ClanId)?.Kingdom;
            if (kingdom == null) return null;
            bool retirable = !kingdom.IsEliminated && kingdom != Clan.PlayerClan.Kingdom &&
                             !kingdom.Settlements.Any();
            if (retirable) CampaignEventDispatcher.Instance.CanKingdomBeDiscontinued(kingdom, ref retirable);
            return retirable ? kingdom : null;
        }

        // FactionDiscontinuationCampaignBehavior.DiscontinueKingdom: its clans leave as independents, and
        // vanilla then gives them its usual time to survive on their own before destroying them.
        private static void DiscontinueKingdom(Kingdom kingdom)
        {
            foreach (Clan clan in kingdom.Clans.ToList())
            {
                FinalizeMapEvents(clan);
                ChangeKingdomAction.ApplyByLeaveByKingdomDestruction(clan);
            }

            kingdom.RulingClan = null;
            DestroyKingdomAction.Apply(kingdom);
        }

        // A kingdom is only retired once it holds no settlement, so only its parties can still be fighting.
        private static void FinalizeMapEvents(Clan clan)
        {
            foreach (WarPartyComponent component in clan.WarPartyComponents.ToList())
            {
                if (component?.Party.IsActive != true) continue;
                component.MobileParty.MapEvent?.FinalizeEvent();
                component.MobileParty.SiegeEvent?.FinalizeSiegeEvent();
            }
        }

        private void StartReinforcementSchedule(long contextId)
        {
            InvasionGroupState? group = _reinforcements.StartSchedule(contextId);
            if (group == null) return;
            Logger.Info("Invasion coastal credit joined: group=" + group.Id + " town=" +
                        State.FindCapture(contextId)!.SettlementId + " next_arrival_day=" +
                        group.NextReinforcementDeadlineDays);
        }

        private void ScheduleReinforcements()
        {
            Initialize();
            Reconcile();
            foreach (InvasionReinforcementDraw draw in _reinforcements.DrawDue())
            {
                if (draw.Outcome == InvasionReinforcementOutcome.Landing) Land(draw);
                else Logger.Info("Invasion reinforcement slot skipped: group=" + draw.Group.Id +
                                 " reason=" + SkipReason(draw.Outcome));
            }
        }

        private static string SkipReason(InvasionReinforcementOutcome outcome) =>
            outcome == InvasionReinforcementOutcome.ActivePartyCapReached ? "active_cap" :
            outcome == InvasionReinforcementOutcome.NoLandingSite ? "no_credited_port" : "configuration_or_clan";

        private void Land(InvasionReinforcementDraw draw)
        {
            InvasionData? configuration = GetConfiguration(draw.Group);
            SpawnDto? spawn = _spawns.FindByPartyTemplateId(draw.Group.SpawnTemplateId);
            Clan? clan = Clan.FindFirst(candidate => candidate.StringId == draw.Group.ClanId);
            Settlement? landing = Settlement.Find(draw.LandingSettlementId!);
            PartyTemplateObject? template = configuration == null ? null :
                Campaign.Current.ObjectManager.GetObject<PartyTemplateObject>(configuration.ReinforcementPartyTemplateId);
            if (spawn == null || clan == null || template == null || landing == null ||
                !IsEligibleLandingSettlement(landing, draw.Group))
            {
                Logger.Info("Invasion reinforcement slot skipped: group=" + draw.Group.Id +
                            " reason=template_or_port_changed");
                return;
            }

            MobileParty? reinforcement = _spawnGroups.SpawnInvasionReinforcement(spawn, template, landing, clan);
            Logger.Info("Invasion reinforcement " + (reinforcement == null ? "failed" : "arrived") + ": group=" +
                        draw.Group.Id + " town=" + landing.StringId + " next_arrival_day=" +
                        draw.Group.NextReinforcementDeadlineDays);
        }

        double IInvasionReinforcementQuery.NowInDays => CampaignTime.Now.ToDays;

        InvasionReinforcementSettings? IInvasionReinforcementQuery.GetSettings(InvasionGroupState group)
        {
            InvasionData? configuration = GetConfiguration(group);
            Clan? clan = Clan.FindFirst(candidate => candidate.StringId == group.ClanId);
            return configuration == null || clan?.IsEliminated != false
                ? null
                : new InvasionReinforcementSettings(configuration.ReinforcementIntervalInDays,
                    configuration.ReinforcementActivePartyCap);
        }

        bool IInvasionReinforcementQuery.IsCoastalTown(string settlementId) =>
            _coastalTownResolver.IsCoastalTown(Settlement.Find(settlementId));

        IReadOnlyList<string> IInvasionReinforcementQuery.GetLandingSites(InvasionGroupState group) =>
            Settlement.All.Where(settlement => IsEligibleLandingSettlement(settlement, group))
                .Select(settlement => settlement.StringId).ToList();

        int IInvasionReinforcementQuery.CountActiveReinforcements(InvasionGroupState group) =>
            MobileParty.All.Count(party => party.IsActive && _parties.GetDynamicSpawnData(party) is { } data &&
                data.InvasionGroupId == group.Id && data.IsIndependentReinforcement);

        private bool IsEligibleLandingSettlement(Settlement settlement, InvasionGroupState group)
        {
            InvasionTownCredit? credit = State.FindCredit(settlement.StringId);
            return credit?.GroupId == group.Id && settlement.IsTown &&
                   settlement.OwnerClan?.StringId == group.ClanId && settlement.SiegeEvent == null &&
                   _coastalTownResolver.IsCoastalTown(settlement);
        }

        public float GetOngoingFoodSupport(Town town)
        {
            InvasionTownCredit? credit = State.FindCredit(town.Settlement.StringId);
            InvasionGroupState? group = State.FindGroup(credit?.GroupId);
            InvasionData? configuration = group == null ? null : GetConfiguration(group);
            return IsEligibleForFood(town, group, configuration) ? configuration!.DailyFoodBonus : 0f;
        }

        private void ApplyReadyOccupations()
        {
            foreach (InvasionCaptureContext capture in GetReadyOccupations())
            {
                Town? town = Settlement.Find(capture.SettlementId)?.Town;
                InvasionGroupState? group = State.FindGroup(capture.OwnershipGroupId);
                InvasionData? configuration = group == null ? null : GetConfiguration(group);
                if (town?.GarrisonParty == null || group == null || configuration == null)
                    continue;

                foreach (MobileParty party in MobileParty.All.Where(p => p.IsActive &&
                             capture.ParticipantPartyIds.Contains(p.StringId) &&
                             _parties.GetDynamicSpawnData(p)?.IsEscort == true).ToList())
                {
                    town.GarrisonParty.MemberRoster.Add(party.MemberRoster);
                    DestroyPartyAction.Apply(null, party);
                }

                if (IsEligibleForFood(town, group, configuration))
                    InvasionOccupationFood.Apply(town, configuration.FoodStockFloor);
                if (!PromoteCapturer(capture, town, group))
                    continue;
                if (State.CompleteOccupation(capture.Id, town.Settlement.OwnerClan?.StringId ?? ""))
                    Logger.Info("Invasion occupation applied: group=" + group.Id + " town=" + town.Settlement.StringId);
            }
        }

        private static bool PromoteCapturer(InvasionCaptureContext capture, Town town, InvasionGroupState group)
        {
            Clan? clan = Clan.FindFirst(candidate => candidate.StringId == group.ClanId);
            MobileParty? party = MobileParty.All.FirstOrDefault(candidate => candidate.IsActive &&
                candidate.StringId == capture.CapturerId);
            Hero? leader = party?.LeaderHero;
            if (clan == null || party == null || leader == null || party.ActualClan != clan || leader.Clan != clan)
                return false;

            if (party.PartyComponent is not LordPartyComponent)
                LordPartyComponent.ConvertPartyToLordParty(party, leader, leader);
            if (clan.Kingdom == null)
            {
                clan.SetInitialHomeSettlement(town.Settlement);
                Campaign.Current.KingdomManager.CreateKingdom(clan.Name, clan.InformalName, clan.Culture, clan);
            }

            return true;
        }

        private bool IsEligibleForFood(Town town, InvasionGroupState? group, InvasionData? configuration)
        {
            Clan? clan = group == null ? null : Clan.FindFirst(candidate => candidate.StringId == group.ClanId);
            InvasionTownCredit? credit = State.FindCredit(town.Settlement.StringId);
            return InvasionFoodSupportRules.IsEligibleForOngoingSupport(credit?.GroupId == group?.Id,
                configuration != null && _coastalTownResolver.IsCoastalTown(town.Settlement),
                group?.ClanId == town.Settlement.OwnerClan?.StringId, configuration != null && !group!.IsSuspended &&
                clan?.IsEliminated == false, town.Settlement.SiegeEvent != null);
        }

        // Occupation is applied on the hourly tick after Bannerlord has finished the capture callbacks.
        public IReadOnlyList<InvasionCaptureContext> GetReadyOccupations()
        {
            Initialize();
            Reconcile();
            var ready = new List<InvasionCaptureContext>();
            foreach (var context in State.Captures)
            {
                Settlement? settlement = Settlement.Find(context.SettlementId);
                if (settlement?.Town?.GarrisonParty?.IsActive != true || settlement.Party.MapEvent != null ||
                    settlement.SiegeEvent != null ||
                    !State.CanApplyOccupation(context.Id, settlement.OwnerClan?.StringId ?? "")) continue;
                bool donorInBattle = MobileParty.All.Any(p => p.IsActive && context.ParticipantPartyIds.Contains(p.StringId) && p.MapEvent != null);
                if (!donorInBattle) ready.Add(context);
            }
            return ready;
        }
    }
}
