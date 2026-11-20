using System;
using System.Collections.Generic;
using System.Linq;
using CustomSpawns.AI.Model;
using CustomSpawns.AI.Offensives;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;

namespace CustomSpawns.Invasions.Reinforcements.Adapters
{
    /**
     * Where an invasion is allowed to land, and which parties land together. Siege targeting restricts and
     * ranks settlements through this without knowing what an invasion is.
     */
    public interface IInvasionLandingQuery
    {
        // The towns a party may take while its invasion holds no coastal town; null once it may march inland.
        Func<Settlement, bool>? GetInitialCoastalTargetFilter(MobileParty party);

        // The parties that land together, so one town can be the nearest for the invasion as a whole.
        IReadOnlyList<MobileParty> GetLandingParties(MobileParty party);

        // Whether this party serves a live invasion, which is what makes it mass on one objective.
        bool ServesLiveInvasion(MobileParty party);
    }

    public sealed class InvasionCoastalSiegeTargetPolicy : ISiegeTargetPolicy
    {
        // How far a party may be drawn onto an objective its siblings are already on. Grouping vanilla
        // Calradia's fortifications by owning kingdom, a kingdom spans 145 to 414 end to end, median 240,
        // so a kingdom's own territory is about this wide: an invasion masses within its own conquest and
        // not across the map.
        private const float GatheringReach = 250f;

        // How much closer a party's own nearest candidate must be to keep it from joining its siblings:
        // the sibling objective wins while it is no further than this multiple of that candidate.
        private const float JoinBias = 1.5f;

        private readonly IInvasionLandingQuery _invasionLandingQuery;
        private readonly IMapPathQuery _mapPathQuery;
        private readonly Func<MobileParty, Settlement?> _findObjective;

        // For the cases where no party has recorded an objective yet, which is also how an invasion starts.
        public InvasionCoastalSiegeTargetPolicy(IInvasionLandingQuery landing, IMapPathQuery mapPaths)
            : this(landing, mapPaths, _ => null)
        {
        }

        public InvasionCoastalSiegeTargetPolicy(IInvasionLandingQuery landing, IMapPathQuery mapPaths,
            Func<MobileParty, Settlement?> findObjective)
        {
            _invasionLandingQuery = landing;
            _mapPathQuery = mapPaths;
            _findObjective = findObjective;
        }

        // Objective selection does not weigh strength for an invasion that gathers: it commits first and
        // masses afterwards, and the ratio is asked again of the force that has actually arrived.
        public bool GathersBeforeAssault(MobileParty party)
        {
            return _invasionLandingQuery.ServesLiveInvasion(party);
        }

        public bool IsCandidateAllowed(MobileParty party, SiegeTargetCandidate candidate)
        {
            Func<Settlement, bool>? initialCoastalTargetFilter =
                _invasionLandingQuery.GetInitialCoastalTargetFilter(party);
            return IsEligibleCoastalTarget(party, candidate.Target, candidate.TargetId,
                initialCoastalTargetFilter);
        }

        public bool IsExistingTargetAllowed(MobileParty party, Settlement target, bool matchingNativeAssault)
        {
            if (matchingNativeAssault)
            {
                return true;
            }

            Func<Settlement, bool>? initialCoastalTargetFilter =
                _invasionLandingQuery.GetInitialCoastalTargetFilter(party);
            return IsEligibleCoastalTarget(party, target, target.StringId, initialCoastalTargetFilter);
        }

        // A landing takes its first town as one force, so a town another party of the same landing is
        // already besieging is one to march to and support rather than one to give up on.
        public bool IsJoinableSiege(MobileParty party, Settlement settlement)
        {
            return _invasionLandingQuery.GetLandingParties(party)
                .Any(landingParty => IsBesieging(landingParty, settlement));
        }

        /**
         * Choosing a target is joining an objective, so a party prefers one its siblings are already on
         * unless its own nearest candidate is clearly closer. Discounting a sibling objective's distance
         * by the join bias is exactly that comparison: the sibling wins while its distance is under the
         * bias times the party's own best. The reach bounds it, so a party whose siblings are a map away
         * takes its own target however little else there is nearby.
         */
        public Func<SiegeTargetCandidate, float> GetTargetDistance(MobileParty party)
        {
            if (!_invasionLandingQuery.ServesLiveInvasion(party))
            {
                return candidate => candidate.Distance;
            }

            HashSet<Settlement> siblingObjectives = FindSiblingObjectives(party);

            return candidate =>
                siblingObjectives.Contains(candidate.Target) && candidate.Distance <= GatheringReach
                    ? candidate.Distance / JoinBias
                    : candidate.Distance;
        }

        // Who attacks the town together: the parties of the invasion that recorded this same objective,
        // whether they have arrived yet or not, plus the besiegers of the town this party is marching on.
        // What counts at the assault is a separate question, asked of the gate.
        // Only a party serving a live invasion pools by objective, as only such a party gathers. A shared
        // besieger camp is a real force whatever the invasion's state, so camp mates always pool.
        public IReadOnlyList<MobileParty> GetAttackingForce(MobileParty party)
        {
            Settlement? objective = _invasionLandingQuery.ServesLiveInvasion(party)
                ? _findObjective(party)
                : null;
            List<MobileParty> force = new List<MobileParty> { party };

            force.AddRange(_invasionLandingQuery.GetLandingParties(party)
                .Where(member => member != party &&
                                 ((objective != null && _findObjective(member) == objective) ||
                                  IsCampMateOf(party, member))));

            return force;
        }

        private HashSet<Settlement> FindSiblingObjectives(MobileParty party)
        {
            HashSet<Settlement> objectives = new HashSet<Settlement>();

            foreach (MobileParty member in _invasionLandingQuery.GetLandingParties(party))
            {
                Settlement? objective = member == party ? null : _findObjective(member);
                if (objective != null)
                {
                    objectives.Add(objective);
                }
            }

            return objectives;
        }

        // Parties of one besieger camp are one force however far their camp positions spread them, and the
        // strength ratio is re-checked every tick, so distance alone would make a besieging force waver.
        // A party still marching to support counts a besieging landing party too, or it would weigh a
        // garrison against itself alone and never set out -- but only the besiegers of the very town it is
        // marching on, or a remnant stranded across the map would borrow their strength against a town
        // they have nothing to do with and besiege it regardless of the force ratio. SetMoveBesiegeSettlement
        // sets both the target and the behaviour together, and the behaviour is what tells a march to
        // support from the patrol the recon fallback issues, which overwrites the target settlement too.
        private static bool IsCampMateOf(MobileParty party, MobileParty landingParty)
        {
            BesiegerCamp? camp = party.BesiegerCamp;

            if (camp != null)
            {
                return IsInCamp(landingParty, camp);
            }

            return party.DefaultBehavior == AiBehavior.BesiegeSettlement && party.TargetSettlement != null &&
                   IsBesieging(landingParty, party.TargetSettlement);
        }

        private static bool IsBesieging(MobileParty party, Settlement settlement)
        {
            BesiegerCamp? camp = settlement.SiegeEvent?.BesiegerCamp;
            return camp != null && camp.SiegeEvent.BesiegedSettlement == settlement && IsInCamp(party, camp);
        }

        private static bool IsInCamp(MobileParty party, BesiegerCamp camp)
        {
            return party.BesiegerCamp == camp || camp.IsBesiegerSideParty(party) ||
                   camp.HasInvolvedPartyForEventType(party.Party);
        }

        private bool IsEligibleCoastalTarget(MobileParty party, Settlement target,
            string targetId, Func<Settlement, bool>? initialCoastalTargetFilter)
        {
            if (initialCoastalTargetFilter == null)
            {
                return true;
            }

            if (!initialCoastalTargetFilter(target))
            {
                ReportCoastalRejection(targetId, "not an eligible coastal town");
                return false;
            }

            if (_mapPathQuery.HasReachableLandGate(party, target))
            {
                return true;
            }

            ReportCoastalRejection(targetId, "no reachable land gate");
            return false;
        }

        private static void ReportCoastalRejection(string settlementId, string reason)
        {
            System.Diagnostics.Debug.WriteLine(
                "CustomSpawns coastal siege target rejected '" + settlementId + "': " + reason);
        }
    }
}
