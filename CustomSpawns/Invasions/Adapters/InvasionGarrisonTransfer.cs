using System;
using System.Collections.Generic;
using System.Linq;
using CustomSpawns.Data.Dto;
using CustomSpawns.Spawn;
using NLog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace CustomSpawns.Invasions.Adapters
{
    internal sealed class InvasionGarrisonTransfer
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly DynamicSpawnData _parties;

        internal InvasionGarrisonTransfer(DynamicSpawnData parties)
        {
            _parties = parties;
        }

        internal bool TryApply(InvasionCaptureContext capture, Town town, InvasionGroupState group,
            InvasionData configuration)
        {
            if (!IsReady(capture, town, group)) return false;

            MobileParty garrison = town.GarrisonParty!;
            var donors = new Dictionary<string, MobileParty>(StringComparer.Ordinal);
            foreach (string participantId in capture.ParticipantPartyIds)
            {
                MobileParty? party = MobileParty.All.FirstOrDefault(candidate => candidate.IsActive &&
                    candidate.StringId == participantId);
                if (party == null) continue;
                if (party.MapEvent != null) return false;
                CsPartyData? data = _parties.GetDynamicSpawnData(party);
                if (data?.InvasionGroupId != group.Id || party.ActualClan?.StringId != group.ClanId) continue;
                donors.Add(participantId, party);
            }

            OccupationGarrisonAllocationResult allocation;
            Dictionary<(string DonorId, string CharacterId), TransferBinding> bindings;
            try
            {
                allocation = BuildAllocation(capture, town, garrison, donors, configuration, out bindings);
            }
            catch (System.Exception exception)
            {
                Logger.Warn("Invasion garrison transfer preflight deferred: " + exception.Message);
                return false;
            }

            if (allocation.IsInsufficient)
            {
                Logger.Warn("Invasion garrison transfer is bounded below its target for '" + capture.SettlementId + "'.");
            }
            if (allocation.Entries.Count == 0) return true;

            try
            {
                foreach (OccupationGarrisonTransferEntry entry in allocation.Entries)
                {
                    TransferBinding binding = bindings[(entry.DonorId, entry.CharacterId)];
                    binding.Source.MemberRoster.AddToCounts(binding.Character, -entry.Count, false, 0, -entry.XpToMove);
                    garrison.MemberRoster.AddToCounts(binding.Character, entry.Count, false, 0, entry.XpToMove);
                    if (!Matches(binding.Source.MemberRoster, binding.Character, entry.SourceCountAfter,
                            entry.SourceXpAfter, binding.SourceWounded) ||
                        !Matches(garrison.MemberRoster, binding.Character, entry.DestinationCountAfter,
                            entry.DestinationXpAfter, binding.DestinationWounded))
                    {
                        throw new InvalidOperationException("native roster postcondition changed unexpectedly");
                    }
                }
            }
            catch (System.Exception exception)
            {
                capture.Cancelled = true;
                Logger.Error("Invasion garrison transfer cancelled after roster mutation for '" +
                             capture.SettlementId + "': " + exception.Message);
                return false;
            }

            Logger.Info("Invasion garrison transfer applied to '" + capture.SettlementId + "': " +
                        allocation.Entries.Count + " troops, " + allocation.XpMoved + " XP.");
            return true;
        }

        private static bool IsReady(InvasionCaptureContext capture, Town town, InvasionGroupState group)
        {
            return capture.Joined && capture.ParticipantSnapshotCaptured && !capture.Cancelled && !capture.OccupationApplied &&
                   capture.OwnershipGroupId == group.Id && capture.ExpectedOwnerClanId == town.Settlement.OwnerClan?.StringId &&
                   town.GarrisonParty?.IsActive == true && town.Settlement.Party.MapEvent == null;
        }

        private static OccupationGarrisonAllocationResult BuildAllocation(InvasionCaptureContext capture, Town town,
            MobileParty garrison, IReadOnlyDictionary<string, MobileParty> parties, InvasionData configuration,
            out Dictionary<(string DonorId, string CharacterId), TransferBinding> bindings)
        {
            TroopRoster destinationRoster = garrison.MemberRoster;
            var destination = new Dictionary<string, OccupationGarrisonDestinationStackSnapshot>(StringComparer.Ordinal);
            bindings = new Dictionary<(string DonorId, string CharacterId), TransferBinding>();
            var donors = new List<OccupationGarrisonDonorSnapshot>();
            var militaryPower = Campaign.Current.Models.MilitaryPowerModel;
            var strengthContext = militaryPower.GetContextForPosition(town.Settlement.Position);

            foreach (KeyValuePair<string, MobileParty> entry in parties.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                MobileParty party = entry.Value;
                TroopRoster roster = party.MemberRoster;
                var candidates = new List<OccupationGarrisonCandidateSnapshot>();
                int healthyTotal = 0;
                for (int index = 0; index < roster.Count; index++)
                {
                    TroopRosterElement element = roster.GetElementCopyAtIndex(index);
                    CharacterObject character = element.Character;
                    int healthy = element.Number - element.WoundedNumber;
                    if (character.IsHero || healthy <= 0) continue;
                    healthyTotal += healthy;
                    int sourceCap = GetUpgradeXpCap(character, party.Party);
                    int destinationCap = GetUpgradeXpCap(character, garrison.Party);
                    candidates.Add(new OccupationGarrisonCandidateSnapshot(character.StringId, false, character.IsRanged,
                        healthy, element.Number, element.Xp,
                        militaryPower.GetTroopPower(character, BattleSideEnum.Defender, strengthContext, 1f), sourceCap));
                    if (!destination.ContainsKey(character.StringId))
                    {
                        TroopRosterElement destinationElement = GetElement(destinationRoster, character);
                        destination.Add(character.StringId, new OccupationGarrisonDestinationStackSnapshot(character.StringId,
                            destinationElement.Number, destinationElement.Xp, destinationCap));
                    }
                    bindings.Add((entry.Key, character.StringId), new TransferBinding(party, character,
                        element.WoundedNumber, GetWoundedCount(destinationRoster, character)));
                }
                donors.Add(new OccupationGarrisonDonorSnapshot(entry.Key, healthyTotal,
                    configuration.MaximumDonorLossFraction, candidates));
            }

            var snapshot = new OccupationGarrisonAllocationSnapshot(capture.ParticipantPartyIds, donors,
                destination.Values.ToList(), destinationRoster.TotalManCount, garrison.Party.PartySizeLimit,
                garrison.Party.CalculateCurrentStrength(), town.Settlement.MilitiaPartyComponent?.MobileParty?.Party.CalculateCurrentStrength() ?? 0f,
                configuration.MinimumGarrisonTroopCount,
                configuration.MaximumGarrisonTroopCount, configuration.MilitiaStrengthRatio);
            return new OccupationGarrisonAllocation().Allocate(snapshot);
        }

        private static int GetUpgradeXpCap(CharacterObject character, PartyBase owner)
        {
            int cap = 0;
            for (int index = 0; index < character.UpgradeTargets.Length; index++)
            {
                cap = Math.Max(cap, character.GetUpgradeXpCost(owner, index));
            }
            return cap;
        }

        private static TroopRosterElement GetElement(TroopRoster roster, CharacterObject character)
        {
            int index = roster.FindIndexOfTroop(character);
            return index >= 0 ? roster.GetElementCopyAtIndex(index) : default;
        }

        private static int GetWoundedCount(TroopRoster roster, CharacterObject character)
        {
            int index = roster.FindIndexOfTroop(character);
            return index >= 0 ? roster.GetElementCopyAtIndex(index).WoundedNumber : 0;
        }

        private static bool Matches(TroopRoster roster, CharacterObject character, int expectedCount,
            int expectedXp, int expectedWounded)
        {
            int index = roster.FindIndexOfTroop(character);
            if (index < 0) return expectedCount == 0 && expectedXp == 0 && expectedWounded == 0;
            TroopRosterElement element = roster.GetElementCopyAtIndex(index);
            return element.Number == expectedCount && element.Xp == expectedXp && element.WoundedNumber == expectedWounded;
        }

        private sealed class TransferBinding
        {
            public TransferBinding(MobileParty source, CharacterObject character, int sourceWounded, int destinationWounded)
            {
                Source = source;
                Character = character;
                SourceWounded = sourceWounded;
                DestinationWounded = destinationWounded;
            }

            public MobileParty Source { get; }
            public CharacterObject Character { get; }
            public int SourceWounded { get; }
            public int DestinationWounded { get; }
        }
    }
}
