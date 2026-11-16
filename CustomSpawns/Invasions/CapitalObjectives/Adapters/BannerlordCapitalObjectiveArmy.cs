using System;
using System.Collections.Generic;
using System.Linq;
using Helpers;
using NLog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace CustomSpawns.Invasions.CapitalObjectives.Adapters
{
    /**
     * Applies the domain's capital mobilization plan to native armies. All parties are resolved and
     * checked before any existing army is dispersed. Recreating a dispersed army recovers its roster,
     * not its exact campaign operation, so that compensation is reported as degraded.
     */
    public sealed class BannerlordCapitalObjectiveArmy
    {
        private readonly CapitalArmyStagingRules _capitalArmyStagingRules;
        private readonly BannerlordCapitalObjectivePartyEligibility _partyEligibility = new();

        public BannerlordCapitalObjectiveArmy(CapitalArmyStagingRules capitalArmyStagingRules)
        {
            _capitalArmyStagingRules = capitalArmyStagingRules;
        }

        public IReadOnlyList<CapitalArmyPartyCandidate> FindCandidates(Invasion invasion,
            Kingdom kingdom)
        {
            return MobileParty.All
                .Where(party => invasion.HoldsClan(party.ActualClan?.StringId))
                .Select(party => Candidate(party, invasion, kingdom))
                .OrderBy(candidate => candidate.PartyId, StringComparer.Ordinal)
                .ToArray();
        }

        public CapitalArmyMobilizationResult Mobilize(CapitalArmyMobilizationPlan plan,
            Invasion invasion, Kingdom kingdom, Settlement target)
        {
            var deferred = plan.DeferredParties.ToList();
            Dictionary<string, MobileParty> partiesById = MobileParty.All
                .GroupBy(party => party.StringId, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            var accepted = new List<MobileParty>();

            foreach (string partyId in plan.PartyIds)
            {
                if (!partiesById.TryGetValue(partyId, out MobileParty? party))
                {
                    deferred.Add(new CapitalArmyPartyDeferral(partyId, "party_not_found"));
                    continue;
                }

                string? reason = FindDeferralReason(party, invasion, kingdom);
                if (reason != null)
                {
                    deferred.Add(new CapitalArmyPartyDeferral(partyId, reason));
                    continue;
                }

                accepted.Add(party);
            }

            MobileParty? commander = accepted.FirstOrDefault(party =>
                string.Equals(party.StringId, plan.CommanderPartyId, StringComparison.Ordinal));
            if (commander == null)
            {
                return Failure("commander_unavailable", deferred);
            }

            if (!_partyEligibility.CanCommand(commander))
            {
                return Failure("commander_cannot_create_army", deferred);
            }

            Settlement? selectedStaging;
            try
            {
                CapitalArmyStagingCandidate? selected = _capitalArmyStagingRules.FindStaging(
                    Settlement.All.Where(settlement => settlement.IsFortification ||
                            settlement.Village?.Bound?.IsCastle == true)
                        .Select(settlement => StagingCandidate(settlement, commander, target)));
                selectedStaging = selected.HasValue ? Settlement.Find(selected.Value.SettlementId) : null;
                if (selectedStaging == null)
                {
                    return Failure("native_gathering_settlement_unavailable", deferred);
                }
            }
            catch (System.Exception)
            {
                return Failure("native_gathering_preflight_failed", deferred);
            }

            Army[] previousArmies = accepted.Select(party => party.Army)
                .Where(army => army != null)
                .Cast<Army>()
                .Distinct()
                .ToArray();
            ArmySnapshot[] previousArmySnapshots = previousArmies
                .Select(army => new ArmySnapshot(army, target))
                .ToArray();

            try
            {
                foreach (Army previousArmy in previousArmies)
                {
                    DisbandArmyAction.ApplyByObjectiveFinished(previousArmy);
                }
            }
            catch (System.Exception)
            {
                return FailureAfterMutation("native_army_disband_failed", deferred, commander,
                    previousArmySnapshots);
            }

            var revalidated = new List<MobileParty>();
            foreach (MobileParty party in accepted)
            {
                string? reason = party.Army != null
                    ? "previous_army_not_disbanded"
                    : FindDeferralReason(party, invasion, kingdom);
                if (reason != null)
                {
                    deferred.Add(new CapitalArmyPartyDeferral(party.StringId, reason));
                    continue;
                }

                revalidated.Add(party);
            }

            commander = revalidated.FirstOrDefault(party =>
                string.Equals(party.StringId, plan.CommanderPartyId, StringComparison.Ordinal));
            if (commander?.LeaderHero == null)
            {
                return FailureAfterMutation("commander_unavailable_after_disband", deferred,
                    commander, previousArmySnapshots);
            }

            var members = new MBList<MobileParty>();
            foreach (MobileParty party in revalidated.Where(party => party != commander))
            {
                members.Add(party);
            }

            Army.ArmyTypes armyType = plan.ObjectiveKind == InvasionCapitalObjectiveKind.Defence
                ? Army.ArmyTypes.Defender
                : Army.ArmyTypes.Besieger;

            try
            {
                foreach (MobileParty party in revalidated)
                {
                    party.BesiegerCamp = null;
                    party.Ai.SetDoNotMakeNewDecisions(false);
                }

                kingdom.CreateArmy(commander.LeaderHero, target, armyType, members);

                Army? gatheringArmy = commander.Army;
                if (gatheringArmy?.LeaderParty == commander)
                {
                    // Recapture must also gather when the invasion has no owned fortress left.
                    MoveToStaging(gatheringArmy, selectedStaging);
                }
            }
            catch (System.Exception)
            {
                return FailureAfterMutation("native_army_creation_failed", deferred, commander,
                    previousArmySnapshots);
            }

            Army? army = commander.Army;
            Settlement? staging = army?.AiBehaviorObject as Settlement;
            string? invalidArmyReason = army?.LeaderParty != commander
                ? "native_army_leader_mismatch"
                : staging == null
                    ? "native_gathering_settlement_missing"
                    : !_capitalArmyStagingRules.IsEligible(
                        StagingCandidate(staging, commander, target))
                        ? "native_gathering_settlement_invalid"
                        : null;
            if (invalidArmyReason != null)
            {
                return FailureAfterMutation(invalidArmyReason, deferred, commander,
                    previousArmySnapshots);
            }

            Army createdArmy = army!;
            ISet<MobileParty> actualParties = new HashSet<MobileParty>(createdArmy.Parties);
            foreach (MobileParty omitted in revalidated.Where(party =>
                         !actualParties.Contains(party)))
            {
                deferred.Add(new CapitalArmyPartyDeferral(omitted.StringId,
                    "native_army_assignment_failed"));
            }

            commander.Ai.SetDoNotMakeNewDecisions(false);
            string[] actualRoster = createdArmy.Parties.Select(party => party.StringId)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            return new CapitalArmyMobilizationResult(true, commander.StringId, actualRoster,
                staging!.StringId, UniqueDeferrals(deferred));
        }

        public IReadOnlyList<string> Invite(Invasion invasion, Kingdom kingdom,
            string commanderPartyId, IReadOnlyList<string> partyIds)
        {
            MobileParty? commander = MobileParty.All.FirstOrDefault(party =>
                string.Equals(party.StringId, commanderPartyId, StringComparison.Ordinal));
            Army? army = commander?.Army;
            if (commander == null || army?.LeaderParty != commander ||
                FindDeferralReason(commander, invasion, kingdom) != null)
            {
                return Array.Empty<string>();
            }

            // Restore gathering immediately on load, before native army inactivity is checked.
            if (invasion.CapitalObjective?.StagingSettlementId is string stagingId &&
                Settlement.Find(stagingId) is Settlement staging)
            {
                if (invasion.CapitalObjective.Kind == InvasionCapitalObjectiveKind.Defence)
                {
                    army.Cohesion = 100f;
                }
                commander.BesiegerCamp = null;
                if (commander.DefaultBehavior != AiBehavior.GoToPoint || army.AiBehaviorObject != staging)
                {
                    MoveToStaging(army, staging);
                }
            }

            MobileParty[] reinforcements = MobileParty.All.Where(party =>
                    partyIds.Contains(party.StringId) &&
                    FindDeferralReason(party, invasion, kingdom) == null)
                .ToArray();
            Army[] previousArmies = reinforcements.Select(party => party.Army)
                .Where(previous => previous != null && previous != army)
                .Cast<Army>()
                .Distinct()
                .ToArray();
            var invited = new List<string>();
            try
            {
                foreach (Army previous in previousArmies)
                {
                    DisbandArmyAction.ApplyByObjectiveFinished(previous);
                }

                foreach (MobileParty party in reinforcements)
                {
                    if (commander.Army != army ||
                        party.Army != null && party.Army != army ||
                        FindDeferralReason(party, invasion, kingdom) != null)
                    {
                        continue;
                    }

                    party.BesiegerCamp = null;
                    party.Ai.SetDoNotMakeNewDecisions(false);
                    party.Army = army;
                    if (party.Army == army)
                    {
                        invited.Add(party.StringId);
                    }
                }
            }
            catch (System.Exception exception)
            {
                LogManager.GetCurrentClassLogger().Warn(exception,
                    "Invasion capital reinforcements interrupted: commander=" + commanderPartyId);
            }

            return invited;
        }

        private static void MoveToStaging(Army army, Settlement staging)
        {
            MobileParty commander = army.LeaderParty;
            army.AiBehaviorObject = staging;
            commander.SetMoveGoToPoint(NavigationHelper.FindReachablePointAroundPosition(
                staging.GatePosition, commander.NavigationCapability,
                army.GatheringPositionMaxDistanceToTheSettlement,
                army.GatheringPositionMinDistanceToTheSettlement), commander.NavigationCapability);
        }

        public bool IsUnified(InvasionCapitalObjective? objective, MobileParty? commander)
        {
            Army? army = commander?.Army;
            if (objective == null || army?.LeaderParty != commander)
            {
                return false;
            }

            if (objective.Phase == InvasionCapitalObjectivePhase.Gathering)
            {
                Settlement? staging = Settlement.Find(objective.StagingSettlementId);
                if (staging == null || !staging.IsActive || staging.IsUnderSiege ||
                    staging.IsUnderRaid || staging.Party.MapEvent != null)
                {
                    return false;
                }
            }

            foreach (string partyId in objective.MobilizedPartyIds)
            {
                MobileParty? party = MobileParty.All.FirstOrDefault(candidate =>
                    string.Equals(candidate.StringId, partyId, StringComparison.Ordinal));
                if (party == null || !party.IsActive)
                {
                    continue;
                }

                if (objective.Phase == InvasionCapitalObjectivePhase.Gathering &&
                    party.Army != army)
                {
                    return false;
                }

                if (objective.Phase == InvasionCapitalObjectivePhase.Advancing &&
                    party.Army != null && party.Army != army)
                {
                    return false;
                }
            }

            return true;
        }

        public void ReleaseUnattachedInvitations(string commanderPartyId)
        {
            MobileParty? commander = MobileParty.All.FirstOrDefault(candidate =>
                string.Equals(candidate.StringId, commanderPartyId, StringComparison.Ordinal));
            Army? army = commander?.Army;

            if (commander == null || army?.LeaderParty != commander)
            {
                return;
            }

            MobileParty[] unarrivedParties = army.Parties.Where(party =>
                    party != commander && party.AttachedTo != commander)
                .ToArray();
            foreach (MobileParty unarrivedParty in unarrivedParties)
            {
                unarrivedParty.Army = null;
            }
        }

        private CapitalArmyPartyCandidate Candidate(MobileParty party, Invasion invasion,
            Kingdom kingdom)
        {
            string? reason = FindDeferralReason(party, invasion, kingdom);
            bool mobilizable = reason == null;
            bool leadsArmy = party.Army?.LeaderParty == party;
            bool canLeadArmy = mobilizable && _partyEligibility.CanCommand(party);
            float strength = leadsArmy
                ? party.Army!.Parties.Where(member => member.IsActive)
                    .Sum(member => member.Party.CalculateCurrentStrength())
                : party.Party.CalculateCurrentStrength();
            return new CapitalArmyPartyCandidate(party.StringId, Math.Max(0f, strength),
                mobilizable, leadsArmy, canLeadArmy, reason);
        }

        private string? FindDeferralReason(MobileParty party, Invasion invasion,
            Kingdom kingdom)
        {
            if (!party.IsActive)
            {
                return "inactive";
            }

            if (party.IsMainParty)
            {
                return "player_party";
            }

            if (!_partyEligibility.IsCombatParty(party))
            {
                return "not_combat_party";
            }

            if (party.IsDisbanding)
            {
                return "disbanding";
            }

            // Native army-member AI also escorts leaderless custom war parties.
            if (party.LeaderHero?.IsActive == false || party.IsLordParty && party.LeaderHero == null)
            {
                return "leader_unavailable";
            }

            if (party.MapFaction != kingdom)
            {
                return "different_kingdom";
            }

            if (!invasion.HoldsClan(party.ActualClan?.StringId))
            {
                return "outside_invasion";
            }

            if (party.MapEvent != null)
            {
                return "in_battle";
            }

            if (party.CurrentSettlement?.SiegeEvent != null)
            {
                return "inside_besieged_settlement";
            }

            Army? army = party.Army;
            if (army != null &&
                !invasion.HoldsClan(army.LeaderParty.ActualClan?.StringId))
            {
                return "outside_invasion_army";
            }

            if (army?.Parties.Any(member =>
                    !invasion.HoldsClan(member.ActualClan?.StringId)) == true)
            {
                return "mixed_invasion_army";
            }

            if (army?.Parties.Any(member => member.IsMainParty) == true)
            {
                return "player_army";
            }

            if (army?.Parties.Any(member => member.MapEvent != null) == true)
            {
                return "army_in_battle";
            }

            return null;
        }

        public string DescribeCandidates(Invasion invasion, Kingdom kingdom) =>
            "candidates=[" + string.Join(";", MobileParty.All
                .Where(party => invasion.HoldsClan(party.ActualClan?.StringId))
                .OrderBy(party => party.StringId, StringComparer.Ordinal)
                .Select(party => party.StringId + ":component=" + party.PartyComponent?.GetType().Name +
                    ",lord=" + party.IsLordParty + ",leader=" + party.LeaderHero?.StringId +
                    ",leader_state=" + party.LeaderHero?.HeroState +
                    ",leader_in_party=" + (party.LeaderHero?.PartyBelongedTo == party) +
                    ",troops=" + party.MemberRoster.TotalManCount +
                    ",healthy=" + party.MemberRoster.TotalHealthyCount +
                    ",strength=" + party.Party.CalculateCurrentStrength() +
                    ",mobilizable=" + (FindDeferralReason(party, invasion, kingdom) == null) +
                    ",can_command=" + _partyEligibility.CanCommand(party) +
                    ",deferred=" + FindDeferralReason(party, invasion, kingdom) +
                    ",army_leader=" + party.Army?.LeaderParty?.StringId +
                    ",attached_to=" + party.AttachedTo?.StringId +
                    ",battle=" + party.MapEvent?.Id +
                    ",settlement=" + party.CurrentSettlement?.StringId +
                    ",order=" + party.DefaultBehavior +
                    ",target_settlement=" + party.TargetSettlement?.StringId +
                    ",target_party=" + party.TargetParty?.StringId)) + "]";

        private static CapitalArmyMobilizationResult Failure(string reason,
            IReadOnlyList<CapitalArmyPartyDeferral> deferred) =>
            new(false, null, Array.Empty<string>(), null, UniqueDeferrals(deferred), reason);

        private static IReadOnlyList<CapitalArmyPartyDeferral> UniqueDeferrals(
            IEnumerable<CapitalArmyPartyDeferral> deferred) => deferred
            .GroupBy(item => item.PartyId, StringComparer.Ordinal)
            .Select(group => group.Last())
            .OrderBy(item => item.PartyId, StringComparer.Ordinal)
            .ToArray();

        private static CapitalArmyStagingCandidate StagingCandidate(Settlement settlement,
            MobileParty commander, Settlement target)
        {
            var mapDistanceModel = Campaign.Current.Models.MapDistanceModel;
            MobileParty.NavigationType navigationCapability = commander.NavigationCapability;
            float distanceFromCommander = commander.CurrentSettlement != null
                ? mapDistanceModel.GetDistance(commander.CurrentSettlement, settlement,
                    false, false, navigationCapability, out _)
                : mapDistanceModel.GetDistance(commander, settlement, false,
                    navigationCapability, out _);
            float distanceFromTarget = mapDistanceModel.GetDistance(target, settlement,
                false, false, navigationCapability);
            return new CapitalArmyStagingCandidate(settlement.StringId, settlement.IsActive,
                settlement.IsFortification,
                settlement.IsUnderSiege || settlement.IsUnderRaid, settlement.Party.MapEvent != null,
                settlement == target, distanceFromCommander < Campaign.MapDiagonalSquared,
                distanceFromTarget < Campaign.MapDiagonalSquared,
                settlement.Village?.Bound?.IsCastle == true, distanceFromTarget);
        }

        private static CapitalArmyMobilizationResult FailureAfterMutation(string reason,
            IReadOnlyList<CapitalArmyPartyDeferral> deferred, MobileParty? commander,
            IReadOnlyList<ArmySnapshot> previousArmies)
        {
            bool previousArmiesWereIntact = previousArmies.All(IsSourceArmyIntact);
            bool replacementDisbanded = DisbandReplacementArmy(commander, previousArmies);
            bool previousArmiesRestored = RestorePreviousArmies(previousArmies);
            string failureReason = !replacementDisbanded || !previousArmiesRestored
                ? reason + "_rollback_failed"
                : previousArmiesWereIntact
                    ? reason
                    : reason + "_rollback_degraded";
            return Failure(failureReason, deferred);
        }

        private static bool IsSourceArmyIntact(ArmySnapshot snapshot) =>
            ReferenceEquals(snapshot.LeaderParty.Army, snapshot.SourceArmy) &&
            snapshot.SourceArmy.LeaderParty == snapshot.LeaderParty;

        private static bool DisbandReplacementArmy(MobileParty? commander,
            IReadOnlyList<ArmySnapshot> previousArmies)
        {
            Army? currentArmy = commander?.Army;
            if (currentArmy?.LeaderParty != commander || previousArmies.Any(snapshot =>
                    ReferenceEquals(snapshot.SourceArmy, currentArmy)))
            {
                return true;
            }

            try
            {
                DisbandArmyAction.ApplyByObjectiveFinished(currentArmy);
                return true;
            }
            catch (System.Exception)
            {
                return false;
            }
        }

        private static bool RestorePreviousArmies(IReadOnlyList<ArmySnapshot> snapshots)
        {
            bool allRestored = true;

            foreach (ArmySnapshot snapshot in snapshots)
            {
                if (IsSourceArmyIntact(snapshot))
                {
                    continue;
                }

                if (snapshot.LeaderParty.Army != null ||
                    snapshot.LeaderParty.LeaderHero?.IsActive != true)
                {
                    allRestored = false;
                    continue;
                }

                MobileParty[] activeMembers = snapshot.MemberParties
                    .Where(party => party.IsActive)
                    .ToArray();
                if (activeMembers.Any(party => party.Army != null))
                {
                    allRestored = false;
                    continue;
                }

                var members = new MBList<MobileParty>();
                foreach (MobileParty member in activeMembers)
                {
                    members.Add(member);
                }

                try
                {
                    snapshot.Kingdom.CreateArmy(snapshot.LeaderParty.LeaderHero,
                        snapshot.TargetSettlement, snapshot.ArmyType, members);
                }
                catch (System.Exception)
                {
                    allRestored = false;
                    continue;
                }

                Army? restoredArmy = snapshot.LeaderParty.Army;
                if (restoredArmy?.LeaderParty != snapshot.LeaderParty ||
                    activeMembers.Any(party => party.Army != restoredArmy))
                {
                    allRestored = false;
                }
            }

            return allRestored;
        }

        private sealed class ArmySnapshot
        {
            public ArmySnapshot(Army army, Settlement fallbackTarget)
            {
                SourceArmy = army;
                Kingdom = army.Kingdom;
                LeaderParty = army.LeaderParty;
                ArmyType = army.ArmyType;
                TargetSettlement = army.LeaderParty.TargetSettlement ??
                                   army.AiBehaviorObject as Settlement ?? fallbackTarget;
                MemberParties = army.Parties.Where(party => party != army.LeaderParty)
                    .ToArray();
            }

            public Army SourceArmy { get; }
            public Kingdom Kingdom { get; }
            public MobileParty LeaderParty { get; }
            public Army.ArmyTypes ArmyType { get; }
            public Settlement TargetSettlement { get; }
            public IReadOnlyList<MobileParty> MemberParties { get; }
        }
    }
}
