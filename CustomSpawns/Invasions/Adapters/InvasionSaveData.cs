using System;
using System.Collections.Generic;
using System.Linq;
using CustomSpawns.Invasions.CapitalObjectives;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.SaveSystem;

namespace CustomSpawns.Invasions.Adapters
{
    // Save DTOs are boundary copies, never a second live state holder.
    public sealed class InvasionStateSaveData
    {
        [SaveableField(2)] public long NextCaptureId = 1;
        [SaveableField(3)] public List<InvasionSaveData> Invasions = new();
        [SaveableField(4)] public List<InvasionCaptureSaveData> Captures = new();
        [SaveableField(5)] public List<InvasionFootholdSaveData> Footholds = new();
        [SaveableField(6)] public List<InvasionCaptureEvidenceSaveData> OwnedCaptureEvidence = new();

        public static InvasionStateSaveData FromState(InvasionState state) => new()
        {
            NextCaptureId = state.NextCaptureId,
            Invasions = state.Invasions.Select(InvasionSaveData.FromState).ToList(),
            Captures = state.Captures.Select(InvasionCaptureSaveData.FromState).ToList(),
            Footholds = state.Footholds.Select(InvasionFootholdSaveData.FromState).ToList()
        };

        public InvasionState ToState()
        {
            var state = new InvasionState { NextCaptureId = Math.Max(1, NextCaptureId) };
            foreach (var invasion in Invasions ?? new List<InvasionSaveData>()) state.InvasionIndex.Add(invasion.ClanId, invasion.ToState());
            foreach (var capture in Captures ?? new List<InvasionCaptureSaveData>()) state.CaptureIndex.Add(capture.Id, capture.ToState());
            foreach (var foothold in Footholds ?? new List<InvasionFootholdSaveData>())
                if (state.FindInvasion(foothold.InvasionClanId) != null && state.FindCapture(foothold.CaptureId)?.IsConfirmed == true)
                    state.FootholdIndex.Add(foothold.SettlementId, foothold.ToState());
            state.NextCaptureId = Math.Max(state.NextCaptureId, state.Captures.Select(c => c.Id + 1).DefaultIfEmpty(1).Max());
            return state;
        }
    }

    // Native references preserve identity across a save made between ownership and the player's aftermath choice.
    // This is evidence for the saved capture, not another copy of the invasion rules or mutable foothold state.
    public sealed class InvasionCaptureEvidenceSaveData
    {
        [SaveableField(1)] public long CaptureId;
        [SaveableField(2)] public string PreviousOwnerClanId = "";
        [SaveableField(3)] public string ExpectedOwnerClanId = "";
        [SaveableField(4)] public Hero? Capturer;
        [SaveableField(5)] public List<MobileParty> Participants = new();
    }

    public sealed class InvasionSaveData
    {
        [SaveableField(2)] public string SpawnTemplateId = "";
        [SaveableField(3)] public string ClanId = "";
        [SaveableField(5)] public bool IsSuspended;
        [SaveableField(6)] public string? CapitalSettlementId;
        [SaveableField(7)] public List<string> Members = new();
        [SaveableField(9)] public List<string> ClanIds = new();
        [SaveableField(10)] public List<InvasionReinforcementScheduleSaveData> ReinforcementSchedules = new();
        // Negative for a convoy clock that has yet to be wound, which is every invasion without a capital.
        [SaveableField(11)] public double NextFoodConvoyDays = -1;
        // Negative until this invasion loses its capital for the first time.
        [SaveableField(12)] public double CapitalRetakeDeadlineDays = -1;
        [SaveableField(13)] public InvasionCapitalObjectiveSaveData? CapitalObjective;

        public static InvasionSaveData FromState(Invasion value) => new()
        {
            SpawnTemplateId = value.SpawnTemplateId,
            ClanId = value.ClanId,
            IsSuspended = value.IsSuspended,
            CapitalSettlementId = value.CapitalSettlementId,
            Members = value.Members.ToList(),
            ClanIds = value.ClanIds.ToList(),
            ReinforcementSchedules = value.ReinforcementSchedules
                .Select(InvasionReinforcementScheduleSaveData.FromState).ToList(),
            NextFoodConvoyDays = value.NextFoodConvoyDays ?? -1,
            CapitalRetakeDeadlineDays = -1,
            CapitalObjective = value.CapitalObjective == null
                ? null
                : InvasionCapitalObjectiveSaveData.FromState(value.CapitalObjective)
        };

        public Invasion ToState()
        {
            var invasion = new Invasion(SpawnTemplateId, ClanId)
            {
                IsSuspended = IsSuspended,
                CapitalSettlementId = CapitalSettlementId,
                NextFoodConvoyDays = NextFoodConvoyDays < 0 ? null : NextFoodConvoyDays,
                Members = new HashSet<string>(Members ?? new List<string>(), StringComparer.Ordinal)
            };

            InvasionCapitalObjective? objective = CapitalObjective?.ToState();
            if (objective == null && CapitalObjective == null && CapitalRetakeDeadlineDays >= 0 &&
                !string.IsNullOrWhiteSpace(CapitalSettlementId))
            {
                objective = new InvasionCapitalObjective(InvasionCapitalObjectiveKind.Attack,
                    InvasionCapitalObjectivePhase.Advancing, CapitalSettlementId, null,
                    CapitalRetakeDeadlineDays, InvasionCapitalObjectiveDormantReason.None, null);
            }

            invasion.RestoreCapitalObjective(objective);
            // A save taken before the invasion could expand carries the founding clan alone.
            foreach (string clanId in ClanIds ?? new List<string>()) invasion.AddClan(clanId);
            foreach (var schedule in ReinforcementSchedules ?? new List<InvasionReinforcementScheduleSaveData>())
                invasion.WindReinforcementSchedule(schedule.RuleId, schedule.IntervalDays, schedule.NextDeadlineDays,
                    schedule.WavesSent);
            return invasion;
        }
    }

    public sealed class InvasionCapitalObjectiveSaveData
    {
        [SaveableField(1)] public int Kind;
        [SaveableField(2)] public int Phase;
        [SaveableField(3)] public string TargetSettlementId = "";
        [SaveableField(4)] public double GatherDeadlineDays = -1;
        [SaveableField(5)] public double AttackDeadlineDays = -1;
        [SaveableField(6)] public int DormantReason;
        [SaveableField(7)] public string CommanderPartyId = "";
        [SaveableField(8)] public string DefenceSiegeEpisodeId = "";
        [SaveableField(9)] public string StagingSettlementId = "";
        [SaveableField(10)] public List<string> MobilizedPartyIds = new();

        public static InvasionCapitalObjectiveSaveData FromState(
            InvasionCapitalObjective value) => new()
        {
            Kind = (int)value.Kind,
            Phase = (int)value.Phase,
            TargetSettlementId = value.TargetSettlementId ?? "",
            GatherDeadlineDays = value.GatherDeadlineDays ?? -1,
            AttackDeadlineDays = value.AttackDeadlineDays ?? -1,
            DormantReason = (int)value.DormantReason,
            CommanderPartyId = value.CommanderPartyId ?? "",
            DefenceSiegeEpisodeId = value.DefenceSiegeEpisodeId ?? "",
            StagingSettlementId = value.StagingSettlementId ?? "",
            MobilizedPartyIds = value.MobilizedPartyIds.ToList()
        };

        public InvasionCapitalObjective? ToState()
        {
            try
            {
                return new InvasionCapitalObjective((InvasionCapitalObjectiveKind)Kind,
                    (InvasionCapitalObjectivePhase)Phase, EmptyAsNull(TargetSettlementId),
                    NegativeAsNull(GatherDeadlineDays), NegativeAsNull(AttackDeadlineDays),
                    (InvasionCapitalObjectiveDormantReason)DormantReason,
                    EmptyAsNull(CommanderPartyId), EmptyAsNull(DefenceSiegeEpisodeId),
                    EmptyAsNull(StagingSettlementId),
                    MobilizedPartyIds ?? new List<string>());
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        private static string? EmptyAsNull(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value;

        private static double? NegativeAsNull(double value) => value < 0 ? null : value;
    }

    public sealed class InvasionReinforcementScheduleSaveData
    {
        [SaveableField(1)] public string RuleId = "";
        [SaveableField(2)] public double IntervalDays;
        [SaveableField(3)] public double NextDeadlineDays;
        [SaveableField(4)] public int WavesSent;

        public static InvasionReinforcementScheduleSaveData FromState(InvasionReinforcementSchedule value) => new()
        {
            RuleId = value.RuleId,
            IntervalDays = value.IntervalDays,
            NextDeadlineDays = value.NextDeadlineDays,
            WavesSent = value.WavesSent
        };
    }

    public sealed class InvasionCaptureSaveData
    {
        [SaveableField(1)] public long Id;
        [SaveableField(2)] public string SettlementId = "";
        [SaveableField(3)] public long BattleStartTimeBits;
        [SaveableField(4)] public string? CapturingInvasionClanId;
        [SaveableField(5)] public string? CapturerId;
        [SaveableField(6)] public string? PreviousOwnerClanId;
        [SaveableField(8)] public bool OwnershipObserved;
        [SaveableField(9)] public string StartingAttackerId = "";
        [SaveableField(11)] public string? AftermathPreviousOwnerClanId;
        [SaveableField(12)] public bool HasParticipants;
        [SaveableField(13)] public Dictionary<string, string> Participants = new();
        [SaveableField(15)] public bool IsConfirmed;
        [SaveableField(16)] public bool Cancelled;
        [SaveableField(17)] public bool OccupationApplied;

        public static InvasionCaptureSaveData FromState(InvasionCapture value) => new()
        {
            Id = value.Id,
            SettlementId = value.SettlementId,
            BattleStartTimeBits = value.BattleStartTimeBits,
            CapturingInvasionClanId = value.CapturingInvasionClanId,
            CapturerId = value.CapturerId,
            PreviousOwnerClanId = value.PreviousOwnerClanId,
            OwnershipObserved = value.OwnershipObserved,
            StartingAttackerId = value.StartingAttackerId,
            AftermathPreviousOwnerClanId = value.AftermathPreviousOwnerClanId,
            HasParticipants = value.HasParticipants,
            Participants = new Dictionary<string, string>(value.Participants, StringComparer.Ordinal),
            IsConfirmed = value.IsConfirmed,
            Cancelled = value.Cancelled,
            OccupationApplied = value.OccupationApplied
        };

        public InvasionCapture ToState() => new(Id, SettlementId, BattleStartTimeBits, StartingAttackerId)
        {
            CapturingInvasionClanId = CapturingInvasionClanId,
            CapturerId = CapturerId,
            PreviousOwnerClanId = PreviousOwnerClanId,
            OwnershipObserved = OwnershipObserved,
            AftermathPreviousOwnerClanId = AftermathPreviousOwnerClanId,
            HasParticipants = HasParticipants,
            Participants = new Dictionary<string, string>(Participants ?? new Dictionary<string, string>(), StringComparer.Ordinal),
            IsConfirmed = IsConfirmed,
            Cancelled = Cancelled,
            OccupationApplied = OccupationApplied
        };
    }

    public sealed class InvasionFootholdSaveData
    {
        [SaveableField(1)] public string SettlementId = "";
        [SaveableField(2)] public string InvasionClanId = "";
        [SaveableField(3)] public long CaptureId;

        public static InvasionFootholdSaveData FromState(InvasionFoothold value) => new()
        {
            SettlementId = value.SettlementId,
            InvasionClanId = value.InvasionClanId,
            CaptureId = value.CaptureId
        };

        public InvasionFoothold ToState() => new(SettlementId, InvasionClanId, CaptureId);
    }
}
