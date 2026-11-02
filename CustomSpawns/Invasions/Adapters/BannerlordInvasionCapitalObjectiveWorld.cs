using System;
using System.Collections.Generic;
using System.Linq;
using CustomSpawns.AI.Adapters;
using CustomSpawns.AI.Model;
using CustomSpawns.AI.Offensives;
using CustomSpawns.Invasions.CapitalObjectives;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace CustomSpawns.Invasions.Adapters
{
    /**
     * Translates live campaign state into the plain observations consumed by the capital objective.
     * The classifications deliberately match the ordinary siege adapter so both paths price the
     * same settlement and roaming forces.
     */
    public sealed class BannerlordInvasionCapitalObjectiveWorld : IInvasionCapitalObjectiveWorld
    {
        private readonly InvasionState _state;
        private readonly IInvasionCapitalRepository _invasionCapitals;
        private readonly IMapPathQuery _mapPathQuery;
        private readonly InvasionCoastalTownResolver _coastalTownResolver;
        private readonly BannerlordCapitalObjectiveArmy _capitalObjectiveArmy;
        private readonly IInvasionConquestTargets _invasionConquestTargets;
        private readonly BannerlordCapitalObjectivePartyEligibility _partyEligibility = new();

        public BannerlordInvasionCapitalObjectiveWorld(InvasionState state,
            IInvasionCapitalRepository invasionCapitals, IMapPathQuery mapPathQuery,
            InvasionCoastalTownResolver coastalTownResolver,
            BannerlordCapitalObjectiveArmy capitalObjectiveArmy, IInvasionConquestTargets invasionConquestTargets)
        {
            _invasionConquestTargets = invasionConquestTargets ??
                                       throw new ArgumentNullException(nameof(invasionConquestTargets));
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _invasionCapitals = invasionCapitals ??
                                throw new ArgumentNullException(nameof(invasionCapitals));
            _mapPathQuery = mapPathQuery ?? throw new ArgumentNullException(nameof(mapPathQuery));
            _coastalTownResolver = coastalTownResolver ??
                                   throw new ArgumentNullException(nameof(coastalTownResolver));
            _capitalObjectiveArmy = capitalObjectiveArmy ??
                                    throw new ArgumentNullException(nameof(capitalObjectiveArmy));
        }

        public CapitalObjectiveObservation Observe(string invasionId,
            string designatedCapitalSettlementId, string? targetSettlementId,
            string? commanderPartyId)
        {
            Invasion? invasion = _state.FindInvasion(invasionId);
            Settlement? capital = Settlement.Find(designatedCapitalSettlementId);
            Settlement? target = targetSettlementId == null ? null : Settlement.Find(targetSettlementId);
            MobileParty? commander = FindParty(commanderPartyId);
            Kingdom? kingdom = FindKingdom(invasion);

            bool ownsCapital = invasion != null && capital != null &&
                               invasion.HoldsClan(capital.OwnerClan?.StringId);
            bool capitalIsBesieged = capital?.SiegeEvent?.BesiegerCamp?.MapFaction != null &&
                                     kingdom != null &&
                                     capital.SiegeEvent.BesiegerCamp.MapFaction.IsAtWarWith(kingdom);
            bool commanderIsEligible = commander != null && kingdom != null &&
                                       IsEligibleCommander(commander, kingdom);
            float assembled = commander == null ? 0f : GetAssembledStrength(commander);
            ISet<MobileParty> assembledParties = commander == null
                ? new HashSet<MobileParty>()
                : new HashSet<MobileParty>(GetAssembledParties(commander));

            float friendlyCapitalStrength = capital == null || kingdom == null
                ? 0f
                : GetFriendlyCapitalStrength(capital, kingdom, assembledParties);
            HashSet<MobileParty> besiegers = capital == null
                ? new HashSet<MobileParty>()
                : GetBesiegerParties(capital);
            float besiegerStrength = besiegers.Sum(party => party.Party.CalculateCurrentStrength());
            float nearbyCapitalHostiles = capital == null || commander == null
                ? 0f
                : GetNearbyHostileLordStrength(commander, capital, besiegers);

            float targetDefenders = target == null || commander == null
                ? 0f
                : GetSettlementDefenderStrength(commander, target);
            HashSet<MobileParty> targetCamp = target == null
                ? new HashSet<MobileParty>()
                : GetBesiegerParties(target);
            float targetNearbyHostiles = target == null || commander == null
                ? 0f
                : GetNearbyHostileLordStrength(commander, target, targetCamp);

            bool matchingSiege = kingdom != null && target != null &&
                                 (target.SiegeEvent?.BesiegerCamp?.MapFaction == kingdom ||
                                  commander != null && IsJoinableSiege(commander, kingdom, target));
            bool targetOwned = invasion != null && target != null &&
                               invasion.HoldsClan(target.OwnerClan?.StringId);
            string? capitalSiegeEpisodeId = capitalIsBesieged && capital?.SiegeEvent != null
                ? BitConverter.DoubleToInt64Bits(capital.SiegeEvent.SiegeStartTime.ToSeconds)
                    .ToString("X16")
                : null;
            bool objectiveArmyIsUnified = _capitalObjectiveArmy.IsUnified(
                invasion?.CapitalObjective, commander);

            return new CapitalObjectiveObservation(CampaignTime.Now.ToDays, ownsCapital,
                capitalIsBesieged, commanderIsEligible, matchingSiege, targetOwned,
                new CapitalDefenceForces(assembled, friendlyCapitalStrength,
                    besiegerStrength + nearbyCapitalHostiles),
                new CapitalAttackForces(assembled, targetDefenders, targetNearbyHostiles),
                capitalSiegeEpisodeId, objectiveArmyIsUnified);
        }

        public IReadOnlyList<CapitalArmyPartyCandidate> FindMobilizationCandidates(
            string invasionId)
        {
            Invasion? invasion = _state.FindInvasion(invasionId);
            Kingdom? kingdom = FindKingdom(invasion);
            if (invasion == null || kingdom == null)
            {
                return Array.Empty<CapitalArmyPartyCandidate>();
            }

            return _capitalObjectiveArmy.FindCandidates(invasion, kingdom);
        }

        public IReadOnlyList<SiegeTargetFacts> FindTargetFacts(string invasionId,
            string commanderPartyId, InvasionAssaultRequirements requirements,
            string? excludedSettlementId)
        {
            Invasion? invasion = _state.FindInvasion(invasionId);
            Kingdom? kingdom = FindKingdom(invasion);
            MobileParty? commander = FindParty(commanderPartyId);
            if (invasion == null || kingdom == null || commander == null)
            {
                return Array.Empty<SiegeTargetFacts>();
            }

            // The kingdom being conquered comes first: the invasion finishes it before it turns on anyone else.
            string? conquestTargetKingdomId = _invasionConquestTargets.FindConquestTargetKingdomId(invasion);
            var facts = new List<SiegeTargetFacts>();
            foreach (Settlement settlement in Settlement.All.Where(candidate =>
                         candidate.IsTown || candidate.IsCastle))
            {
                SiegeTargetKind kind = settlement.IsTown ? SiegeTargetKind.Town : SiegeTargetKind.Castle;
                bool hostile = settlement.MapFaction != null &&
                               commander.MapFaction.IsAtWarWith(settlement.MapFaction);
                bool compatibleSiege = settlement.SiegeEvent == null ||
                                       IsJoinableSiege(commander, kingdom, settlement);
                bool strategic = requirements.MatchesStrategicRequirement(settlement.IsTown,
                    _coastalTownResolver.IsCoastalTown(settlement));
                bool reachable = settlement.IsActive &&
                                 _mapPathQuery.HasReachableLandGate(commander, settlement);
                HashSet<MobileParty> camp = GetBesiegerParties(settlement);
                float nearbyHostiles = GetNearbyHostileLordStrength(commander, settlement, camp);
                facts.Add(new SiegeTargetFacts(settlement.StringId, kind, settlement.IsActive,
                    hostile, compatibleSiege, strategic, reachable,
                    commander.Position.Distance(settlement.GatePosition),
                    GetSettlementDefenderStrength(commander, settlement), nearbyHostiles,
                    conquestTargetKingdomId != null && settlement.MapFaction?.StringId == conquestTargetKingdomId));
            }

            return facts;
        }

        public CapitalArmyMobilizationResult MobilizeArmy(CapitalArmyMobilizationPlan plan)
        {
            Invasion? invasion = _state.FindInvasion(plan.InvasionId);
            Kingdom? kingdom = FindKingdom(invasion);
            Settlement? target = Settlement.Find(plan.TargetSettlementId);

            if (invasion == null || kingdom == null || target == null || !target.IsActive)
            {
                return new CapitalArmyMobilizationResult(false, null, Array.Empty<string>(), null,
                    plan.DeferredParties, "mobilization_context_unavailable");
            }

            return _capitalObjectiveArmy.Mobilize(plan, invasion, kingdom, target);
        }

        public IReadOnlyList<string> InviteToArmy(string invasionId, string commanderPartyId,
            IReadOnlyList<string> partyIds)
        {
            Invasion? invasion = _state.FindInvasion(invasionId);
            Kingdom? kingdom = FindKingdom(invasion);
            if (invasion == null || kingdom == null)
            {
                return Array.Empty<string>();
            }

            return _capitalObjectiveArmy.Invite(invasion, kingdom, commanderPartyId, partyIds);
        }

        public void BeginAdvance(string commanderPartyId)
        {
            _capitalObjectiveArmy.ReleaseUnattachedInvitations(commanderPartyId);
            FindParty(commanderPartyId)?.Ai.SetDoNotMakeNewDecisions(false);
        }

        public void RelocateCapitalAfterObjectiveCapture(string invasionId, string settlementId)
        {
            Kingdom? kingdom = FindKingdom(_state.FindInvasion(invasionId));
            if (kingdom != null)
            {
                _invasionCapitals.RelocateAfterObjectiveCapture(kingdom.StringId, settlementId);
            }
        }

        public string Describe(Invasion invasion, string? evaluatedCommanderPartyId = null,
            string? evaluatedTargetSettlementId = null)
        {
            InvasionCapitalObjective? objective = invasion.CapitalObjective;
            MobileParty? commander = FindParty(evaluatedCommanderPartyId ??
                                               objective?.CommanderPartyId);
            Settlement? capital = invasion.CapitalSettlementId == null
                ? null
                : Settlement.Find(invasion.CapitalSettlementId);
            string? targetSettlementId = evaluatedTargetSettlementId ??
                                         objective?.TargetSettlementId;
            Settlement? target = targetSettlementId == null
                ? null
                : Settlement.Find(targetSettlementId);
            Kingdom? kingdom = FindKingdom(invasion);
            string candidates = kingdom == null
                ? "candidates=[] kingdom=none"
                : _capitalObjectiveArmy.DescribeCandidates(invasion, kingdom);
            if (commander == null)
            {
                return "day=" + CampaignTime.Now.ToDays + " commander=none target=" + target?.StringId + " " + candidates;
            }

            Army? army = commander.Army?.LeaderParty == commander ? commander.Army : null;
            MobileParty[] attached = GetAssembledParties(commander).ToArray();
            MobileParty[] called = army?.Parties.Where(party => party != commander &&
                                                   party.AttachedTo != commander).ToArray() ??
                                   Array.Empty<MobileParty>();
            int assembledTroops = attached.Sum(party => party.MemberRoster.TotalManCount);
            int assembledHealthy = attached.Sum(party => party.MemberRoster.TotalHealthyCount);
            float assembledStrength = GetAssembledStrength(commander);
            int attachedLimits = attached.Sum(party => party.Party.PartySizeLimit);
            int calledTroops = called.Sum(party => party.MemberRoster.TotalManCount);
            float calledStrength = called.Sum(party => party.Party.CalculateCurrentStrength());
            MobileParty? garrison = capital?.Town?.GarrisonParty;
            MobileParty? targetGarrison = target?.Town?.GarrisonParty;
            CapitalObjectiveObservation observation = Observe(invasion.ClanId,
                invasion.CapitalSettlementId ?? string.Empty, targetSettlementId,
                commander.StringId);
            float garrisonStrength = garrison?.Party.CalculateCurrentStrength() ?? 0f;
            float targetGarrisonStrength = targetGarrison?.Party.CalculateCurrentStrength() ?? 0f;
            HashSet<MobileParty> capitalCamp = capital == null
                ? new HashSet<MobileParty>()
                : GetBesiegerParties(capital);
            float campStrength = capitalCamp.Sum(party => party.Party.CalculateCurrentStrength());
            float nearbyCapitalHostiles = capital == null
                ? 0f
                : GetNearbyHostileLordStrength(commander, capital, capitalCamp);

            return "day=" + observation.NowDays + " commander=" + commander.StringId +
                   " army=" + (army == null ? "none" : army.Name?.ToString()) +
                   " leader_troops=" + commander.MemberRoster.TotalManCount +
                   " leader_healthy=" + commander.MemberRoster.TotalHealthyCount +
                   " leader_strength=" + commander.Party.CalculateCurrentStrength() +
                   " leader_limit=" + commander.Party.PartySizeLimit +
                   " attached_parties=" + Math.Max(0, attached.Length - 1) +
                   " called_parties=" + called.Length +
                   " assembled_troops=" + assembledTroops +
                   " assembled_healthy=" + assembledHealthy +
                   " assembled_strength=" + assembledStrength +
                   " attached_limits=" + attachedLimits +
                   " called_troops=" + calledTroops +
                   " called_strength=" + calledStrength +
                   " capital_garrison_troops=" + (garrison?.MemberRoster.TotalManCount ?? 0) +
                   " capital_garrison_strength=" +
                   garrisonStrength +
                   " capital_garrison_limit=" + (garrison?.Party.PartySizeLimit ?? 0) +
                   " militia_strength=" + (capital?.Militia ?? 0f) +
                   " militia_excluded_from_defence=true" +
                   " friendly_occupant_strength=" +
                   Math.Max(0f, observation.DefenceForces.GarrisonAndOccupantStrength -
                                garrisonStrength) +
                   " besieger_camp_strength=" + campStrength +
                   " capital_nearby_hostile_strength=" + nearbyCapitalHostiles +
                   " target=" + target?.StringId +
                   " target_garrison_troops=" +
                   (targetGarrison?.MemberRoster.TotalManCount ?? 0) +
                   " target_garrison_strength=" + targetGarrisonStrength +
                   " target_garrison_limit=" + (targetGarrison?.Party.PartySizeLimit ?? 0) +
                   " target_militia_strength=" + (target?.Militia ?? 0f) +
                   " target_defenders=" + observation.AttackForces.SettlementDefenderStrength +
                   " target_nearby_hostiles=" + observation.AttackForces.NearbyHostileStrength +
                   " defence_ratio=" + Ratio(assembledStrength +
                       observation.DefenceForces.GarrisonAndOccupantStrength,
                       observation.DefenceForces.BesiegerAndNearbyHostileStrength) +
                   " attack_target_ratio=" + Ratio(assembledStrength,
                       observation.AttackForces.SettlementDefenderStrength) +
                   " attack_roaming_ratio=" + Ratio(assembledStrength,
                       observation.AttackForces.NearbyHostileStrength) + " " + candidates;
        }

        private static float Ratio(float numerator, float denominator) => denominator <= 0f
            ? float.PositiveInfinity
            : numerator / denominator;

        private static Kingdom? FindKingdom(Invasion? invasion) => invasion == null
            ? null
            : invasion.FindLeadingClan()?.Kingdom;

        private static MobileParty? FindParty(string? partyId) => partyId == null
            ? null
            : MobileParty.All.FirstOrDefault(party =>
                string.Equals(party.StringId, partyId, StringComparison.Ordinal));

        private bool IsEligibleCommander(MobileParty party, Kingdom kingdom)
        {
            return party.IsActive && !party.IsMainParty && _partyEligibility.CanCommand(party) &&
                   !party.IsDisbanding && party.MapEvent == null &&
                   party.CurrentSettlement?.SiegeEvent == null && party.MapFaction == kingdom;
        }

        private static float GetAssembledStrength(MobileParty commander)
        {
            return GetAssembledParties(commander)
                .Where(party => party.IsActive)
                .Sum(party => party.Party.CalculateCurrentStrength());
        }

        private static IEnumerable<MobileParty> GetAssembledParties(MobileParty commander) =>
            commander.AttachedParties.Prepend(commander).Distinct();

        private float GetFriendlyCapitalStrength(Settlement capital, Kingdom kingdom,
            ISet<MobileParty> assembledParties)
        {
            float garrison = capital.Town?.GarrisonParty?.Party.CalculateCurrentStrength() ?? 0f;
            float occupants = capital.Parties
                .Where(party => !assembledParties.Contains(party) && party.IsActive &&
                                _partyEligibility.IsCombatParty(party) && !party.IsVillager &&
                                !party.IsCaravan && !party.IsGarrison && !party.IsMilitia &&
                                party.MapFaction == kingdom)
                .Distinct()
                .Sum(party => party.Party.CalculateCurrentStrength());
            return garrison + occupants;
        }

        private static float GetSettlementDefenderStrength(MobileParty attacker,
            Settlement settlement)
        {
            float garrison = settlement.Town?.GarrisonParty?.Party.CalculateCurrentStrength() ?? 0f;
            IEnumerable<SettlementDefenderPartyStrength> occupants = settlement.Parties.Select(party =>
                new SettlementDefenderPartyStrength(party.Party.CalculateCurrentStrength(),
                    party.IsActive, party.MapFaction.IsAtWarWith(attacker.MapFaction),
                    party.IsVillager, party.IsCaravan, party.IsGarrison, party.IsMilitia));
            return BannerlordBesiegeClosestSettlementAdapter.CalculateDefenderStrength(
                settlement.Militia, garrison, occupants);
        }

        private static HashSet<MobileParty> GetBesiegerParties(Settlement settlement)
        {
            IEnumerable<MobileParty>? parties = settlement.SiegeEvent?.BesiegerCamp?
                .GetInvolvedPartiesForEventType()
                .Where(party => party.MobileParty != null)
                .Select(party => party.MobileParty);
            return parties == null
                ? new HashSet<MobileParty>()
                : new HashSet<MobileParty>(parties);
        }

        private static float GetNearbyHostileLordStrength(MobileParty observer,
            Settlement target, ISet<MobileParty> excluded)
        {
            IEnumerable<RoamingPartyStrength> parties = MobileParty.All
                .Where(party => !excluded.Contains(party) && party != observer && party.IsActive &&
                                party.CurrentSettlement == null && party.MapEvent == null &&
                                party.MapFaction.IsAtWarWith(observer.MapFaction))
                .Select(party => new RoamingPartyStrength(false, party.IsActive, true,
                    target.GatePosition.Distance(party.Position),
                    party.Party.CalculateCurrentStrength(), party.IsLordParty, party.IsFleeing(),
                    party.IsVillager || party.IsCaravan));
            return OffensiveStrengthRules.SumVisibleHostileLordStrength(parties,
                observer.SeeingRange);
        }

        private static bool IsJoinableSiege(MobileParty commander, Kingdom kingdom,
            Settlement settlement)
        {
            var camp = settlement.SiegeEvent?.BesiegerCamp;
            return camp != null && (camp.MapFaction == kingdom || commander.BesiegerCamp == camp ||
                                    camp.IsBesiegerSideParty(commander) ||
                                    camp.HasInvolvedPartyForEventType(commander.Party));
        }
    }
}
