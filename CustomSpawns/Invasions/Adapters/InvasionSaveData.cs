using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.SaveSystem;

namespace CustomSpawns.Invasions.Adapters
{
    // Save DTOs are boundary copies, never a second live state holder.
    public sealed class InvasionSaveData
    {
        [SaveableField(1)] public long NextGroupId = 1;
        [SaveableField(2)] public long NextCaptureId = 1;
        [SaveableField(3)] public List<InvasionGroupSaveData> Groups = new();
        [SaveableField(4)] public List<InvasionCaptureSaveData> Captures = new();
        [SaveableField(5)] public List<InvasionCreditSaveData> Credits = new();
        [SaveableField(6)] public List<InvasionCaptureEvidenceSaveData> OwnedCaptureEvidence = new();

        internal static InvasionSaveData FromState(InvasionState state) => new()
        {
            NextGroupId = state.NextGroupId,
            NextCaptureId = state.NextCaptureId,
            Groups = state.Groups.Select(InvasionGroupSaveData.FromState).ToList(),
            Captures = state.Captures.Select(InvasionCaptureSaveData.FromState).ToList(),
            Credits = state.Credits.Select(InvasionCreditSaveData.FromState).ToList()
        };

        internal InvasionState ToState()
        {
            var state = new InvasionState { NextGroupId = Math.Max(1, NextGroupId), NextCaptureId = Math.Max(1, NextCaptureId) };
            foreach (var group in Groups ?? new List<InvasionGroupSaveData>()) state.GroupIndex.Add(group.Id, group.ToState());
            foreach (var capture in Captures ?? new List<InvasionCaptureSaveData>()) state.CaptureIndex.Add(capture.Id, capture.ToState());
            foreach (var credit in Credits ?? new List<InvasionCreditSaveData>())
                if (state.FindGroup(credit.GroupId) != null && state.FindCapture(credit.ContextId)?.Joined == true)
                    state.CreditIndex.Add(credit.SettlementId, credit.ToState());
            state.NextCaptureId = Math.Max(state.NextCaptureId, state.Captures.Select(c => c.Id + 1).DefaultIfEmpty(1).Max());
            return state;
        }
    }

    // Native references preserve identity across a save made between ownership and the player's aftermath choice.
    // This is evidence for the saved context, not another copy of the invasion rules or mutable credit state.
    public sealed class InvasionCaptureEvidenceSaveData
    {
        [SaveableField(1)] public long ContextId;
        [SaveableField(2)] public string PreviousOwnerClanId = "";
        [SaveableField(3)] public string ExpectedOwnerClanId = "";
        [SaveableField(4)] public Hero? Capturer;
        [SaveableField(5)] public List<MobileParty> Participants = new();
    }

    public sealed class InvasionGroupSaveData
    {
        [SaveableField(1)] public string Id = "";
        [SaveableField(2)] public string SpawnTemplateId = "";
        [SaveableField(3)] public string ClanId = "";
        [SaveableField(4)] public bool HasFirstCoastalConquest;
        [SaveableField(5)] public bool IsSuspended;
        [SaveableField(6)] public double NextReinforcementDeadlineDays;
        [SaveableField(7)] public List<string> Members = new();
        [SaveableField(8)] public double ReinforcementIntervalDays;

        internal static InvasionGroupSaveData FromState(InvasionGroupState value) => new()
        {
            Id = value.Id,
            SpawnTemplateId = value.SpawnTemplateId,
            ClanId = value.ClanId,
            HasFirstCoastalConquest = value.HasFirstCoastalConquest,
            IsSuspended = value.IsSuspended,
            NextReinforcementDeadlineDays = value.NextReinforcementDeadlineDays,
            ReinforcementIntervalDays = value.ReinforcementIntervalDays,
            Members = value.Members.ToList()
        };

        internal InvasionGroupState ToState() => new(Id, SpawnTemplateId, ClanId)
        {
            HasFirstCoastalConquest = HasFirstCoastalConquest,
            IsSuspended = IsSuspended,
            NextReinforcementDeadlineDays = NextReinforcementDeadlineDays,
            ReinforcementIntervalDays = ReinforcementIntervalDays,
            Members = new HashSet<string>(Members ?? new List<string>(), StringComparer.Ordinal)
        };
    }

    public sealed class InvasionCaptureSaveData
    {
        [SaveableField(1)] public long Id;
        [SaveableField(2)] public string SettlementId = "";
        [SaveableField(3)] public long BattleStartTimeBits;
        [SaveableField(4)] public string? OwnershipGroupId;
        [SaveableField(5)] public string? CapturerId;
        [SaveableField(6)] public string? PreviousOwnerClanId;
        [SaveableField(7)] public string? ExpectedOwnerClanId;
        [SaveableField(8)] public bool OwnershipObserved;
        [SaveableField(9)] public string StartingAttackerId = "";
        [SaveableField(10)] public string? AftermathAttackerId;
        [SaveableField(11)] public string? AftermathPreviousOwnerClanId;
        [SaveableField(12)] public bool ParticipantSnapshotCaptured;
        [SaveableField(13)] public Dictionary<string, string> Participants = new();
        [SaveableField(14)] public bool IsCoastalTown;
        [SaveableField(15)] public bool Joined;
        [SaveableField(16)] public bool Cancelled;
        [SaveableField(17)] public bool OccupationApplied;

        internal static InvasionCaptureSaveData FromState(InvasionCaptureContext value) => new()
        {
            Id = value.Id,
            SettlementId = value.SettlementId,
            BattleStartTimeBits = value.BattleStartTimeBits,
            OwnershipGroupId = value.OwnershipGroupId,
            CapturerId = value.CapturerId,
            PreviousOwnerClanId = value.PreviousOwnerClanId,
            ExpectedOwnerClanId = value.ExpectedOwnerClanId,
            OwnershipObserved = value.OwnershipObserved,
            StartingAttackerId = value.StartingAttackerId,
            AftermathAttackerId = value.AftermathAttackerId,
            AftermathPreviousOwnerClanId = value.AftermathPreviousOwnerClanId,
            ParticipantSnapshotCaptured = value.ParticipantSnapshotCaptured,
            Participants = new Dictionary<string, string>(value.Participants, StringComparer.Ordinal),
            IsCoastalTown = value.IsCoastalTown,
            Joined = value.Joined,
            Cancelled = value.Cancelled,
            OccupationApplied = value.OccupationApplied
        };

        internal InvasionCaptureContext ToState() => new(Id, SettlementId, BattleStartTimeBits, StartingAttackerId)
        {
            OwnershipGroupId = OwnershipGroupId,
            CapturerId = CapturerId,
            PreviousOwnerClanId = PreviousOwnerClanId,
            ExpectedOwnerClanId = ExpectedOwnerClanId,
            OwnershipObserved = OwnershipObserved,
            AftermathAttackerId = AftermathAttackerId,
            AftermathPreviousOwnerClanId = AftermathPreviousOwnerClanId,
            ParticipantSnapshotCaptured = ParticipantSnapshotCaptured,
            Participants = new Dictionary<string, string>(Participants ?? new Dictionary<string, string>(), StringComparer.Ordinal),
            IsCoastalTown = IsCoastalTown,
            Joined = Joined,
            Cancelled = Cancelled,
            OccupationApplied = OccupationApplied
        };
    }

    public sealed class InvasionCreditSaveData
    {
        [SaveableField(1)] public string SettlementId = "";
        [SaveableField(2)] public string GroupId = "";
        [SaveableField(3)] public long ContextId;
        [SaveableField(4)] public bool SupplyPortEligible;

        internal static InvasionCreditSaveData FromState(InvasionTownCredit value) => new()
        {
            SettlementId = value.SettlementId,
            GroupId = value.GroupId,
            ContextId = value.ContextId,
            SupplyPortEligible = value.SupplyPortEligible
        };

        internal InvasionTownCredit ToState() => new(SettlementId, GroupId, ContextId, SupplyPortEligible);
    }
}
