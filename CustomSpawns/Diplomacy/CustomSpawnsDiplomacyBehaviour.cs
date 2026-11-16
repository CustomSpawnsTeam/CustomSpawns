using System;
using System.Collections.Generic;
using CustomSpawns.CampaignData.Implementations;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

namespace CustomSpawns.Diplomacy
{
    public class CustomSpawnsDiplomacyBehaviour : CampaignBehaviorBase
    {
        private readonly CustomSpawnsDiplomacyFactionsProvider _factionsProvider;
        private readonly CustomSpawnsDiplomacyReconciliationProvider _reconciliationProvider;
        private readonly ForceNoKingdomMembershipProvider _membershipProvider;
        private readonly DailyLogger _dailyLogger;
        private readonly IFactionWarStanceProvider _factionWarStanceProvider;
        private readonly ISet<string> _reportedMissingClanIds = new HashSet<string>();
        private readonly DiplomacyReconciliationScheduler _reconciliationScheduler = new DiplomacyReconciliationScheduler();
        private bool _hasInitialized;
        private bool _isReconciling;

        public CustomSpawnsDiplomacyBehaviour(
            CustomSpawnsDiplomacyFactionsProvider factionsProvider,
            CustomSpawnsClanDiplomacyProvider clanDiplomacyProvider,
            NonMercenaryCustomSpawnsFactionsProvider forceNoKingdomProvider,
            DailyLogger dailyLogger,
            IFactionWarStanceProvider factionWarStanceProvider)
        {
            _factionsProvider = factionsProvider;
            _factionWarStanceProvider = factionWarStanceProvider;
            _reconciliationProvider = new CustomSpawnsDiplomacyReconciliationProvider(clanDiplomacyProvider);
            _membershipProvider = new ForceNoKingdomMembershipProvider(forceNoKingdomProvider);
            _dailyLogger = dailyLogger;
        }

        public override void RegisterEvents()
        {
            CampaignEvents.TickEvent.AddNonSerializedListener(this, OnTick);
            CampaignEvents.KingdomCreatedEvent.AddNonSerializedListener(this, OnKingdomCreated);
            CampaignEvents.OnClanChangedKingdomEvent.AddNonSerializedListener(this, OnClanChangedKingdom);
            CampaignEvents.WarDeclared.AddNonSerializedListener(this, OnWarDeclared);
            CampaignEvents.MakePeace.AddNonSerializedListener(this, OnPeaceMade);
        }

        public override void SyncData(IDataStore dataStore) { }

        private void OnTick(float deltaThime)
        {
            if (!_hasInitialized)
            {
                _hasInitialized = true;
                EnforceNoKingdomMembership();
                Reconcile();
                return;
            }

            if (_reconciliationScheduler.TakePendingRequest())
            {
                Reconcile();
            }
        }

        private void OnKingdomCreated(Kingdom kingdom)
        {
            Reconcile();
        }

        private void OnClanChangedKingdom(Clan clan, Kingdom oldKingdom, Kingdom newKingdom,
            ChangeKingdomAction.ChangeKingdomActionDetail detail, bool showNotification)
        {
            EnforceNoKingdom(clan);
            Reconcile();
        }

        private void OnWarDeclared(IFaction faction1, IFaction faction2, DeclareWarAction.DeclareWarDetail detail)
        {
            _reconciliationScheduler.Request();
        }

        private void OnPeaceMade(IFaction faction1, IFaction faction2, MakePeaceAction.MakePeaceDetail detail)
        {
            _reconciliationScheduler.Request();
        }

        private void EnforceNoKingdomMembership()
        {
            foreach (Clan clan in Clan.All)
            {
                EnforceNoKingdom(clan);
            }
        }

        private void EnforceNoKingdom(Clan clan)
        {
            ForceNoKingdomMembershipAction action = _membershipProvider.GetAction(clan.StringId,
                clan.Kingdom != null, clan.IsUnderMercenaryService, clan.Kingdom?.RulingClan == clan);
            if (action == ForceNoKingdomMembershipAction.LeaveKingdomAsMercenary)
            {
                ChangeKingdomAction.ApplyByLeaveKingdomAsMercenary(clan, false);
                return;
            }

            if (action == ForceNoKingdomMembershipAction.LeaveKingdom)
            {
                ChangeKingdomAction.ApplyByLeaveKingdom(clan, false);
            }
        }

        private void Reconcile()
        {
            if (_isReconciling)
            {
                return;
            }

            _isReconciling = true;
            try
            {
                IList<Clan> policyClans = _factionsProvider.GetConfiguredClans(out IList<string> missingClanIds);
                ReportMissingClanIds(missingClanIds);
                var actionsByFactionPair = new Dictionary<FactionPair, List<DiplomacyReconciliationAction>>();
                foreach (Clan policyClan in policyClans)
                {
                    foreach (Clan targetClan in Clan.All)
                    {
                        AddReconciliationAction(actionsByFactionPair, policyClan, targetClan);
                    }
                }

                ApplyReconciliationActions(actionsByFactionPair);
            }
            finally
            {
                _isReconciling = false;
            }
        }

        private void AddReconciliationAction(Dictionary<FactionPair, List<DiplomacyReconciliationAction>> actionsByFactionPair,
            Clan policyClan, Clan targetClan)
        {
            IFaction policyMapFaction = policyClan.MapFaction;
            IFaction targetMapFaction = targetClan.MapFaction;
            if (policyMapFaction == null || targetMapFaction == null || policyMapFaction == targetMapFaction)
            {
                return;
            }

            DiplomacyReconciliationAction action = _reconciliationProvider.GetDesiredAction(policyClan, targetClan);
            if (action == DiplomacyReconciliationAction.None)
            {
                return;
            }

            FactionPair pair = new FactionPair(policyMapFaction, targetMapFaction);
            if (!actionsByFactionPair.TryGetValue(pair, out List<DiplomacyReconciliationAction> actions))
            {
                actions = new List<DiplomacyReconciliationAction>();
                actionsByFactionPair.Add(pair, actions);
            }

            actions.Add(action);
        }

        private void ApplyReconciliationActions(Dictionary<FactionPair, List<DiplomacyReconciliationAction>> actionsByFactionPair)
        {
            foreach (KeyValuePair<FactionPair, List<DiplomacyReconciliationAction>> entry in actionsByFactionPair)
            {
                DiplomacyReconciliationAction action = _reconciliationProvider.ApplyStance(
                    CustomSpawnsDiplomacyReconciliationProvider.Aggregate(entry.Value, out bool hasConflict),
                    _factionWarStanceProvider.FindStance(entry.Key.First, entry.Key.Second));
                if (hasConflict)
                {
                    _dailyLogger.Info("Conflicting diplomacy policies for " + entry.Key.First.Name + " and " +
                                      entry.Key.Second.Name + "; keeping constant war.");
                }

                if (action == DiplomacyReconciliationAction.MakePeace && entry.Key.First.IsAtWarWith(entry.Key.Second))
                {
                    MakePeaceAction.Apply(entry.Key.First, entry.Key.Second);
                }
                else if (action == DiplomacyReconciliationAction.DeclareWar && !entry.Key.First.IsAtWarWith(entry.Key.Second))
                {
                    DeclareWarAction.ApplyByDefault(entry.Key.First, entry.Key.Second);
                }
            }
        }

        private void ReportMissingClanIds(IList<string> missingClanIds)
        {
            foreach (string clanId in missingClanIds)
            {
                if (_reportedMissingClanIds.Add(clanId))
                {
                    _dailyLogger.Info("Could not find configured diplomacy clan '" + clanId + "'; its diplomacy policy is ignored.");
                }
            }
        }

        private readonly struct FactionPair : IEquatable<FactionPair>
        {
            public IFaction First { get; }
            public IFaction Second { get; }

            public FactionPair(IFaction first, IFaction second)
            {
                First = first;
                Second = second;
            }

            public bool Equals(FactionPair other)
            {
                return First == other.First && Second == other.Second || First == other.Second && Second == other.First;
            }

            public override bool Equals(object obj)
            {
                return obj is FactionPair other && Equals(other);
            }

            public override int GetHashCode()
            {
                return First.GetHashCode() ^ Second.GetHashCode();
            }
        }
    }

    public sealed class DiplomacyReconciliationScheduler
    {
        private bool _isPending;

        public void Request()
        {
            _isPending = true;
        }

        public bool TakePendingRequest()
        {
            bool wasPending = _isPending;
            _isPending = false;
            return wasPending;
        }
    }
}
