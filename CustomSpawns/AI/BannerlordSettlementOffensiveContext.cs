using System;
using System.Collections.Generic;
using System.Linq;
using CustomSpawns.AI.Model;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace CustomSpawns.AI
{
    /**
     * The Bannerlord lookups a settlement offensive needs whether it raids or besieges: how strong the
     * attacking force is, what the party can see around itself, and where it waits while it has no
     * target. Only target selection differs between the two, so only that is left to the
     * behaviour-specific contexts.
     */
    internal abstract class BannerlordSettlementOffensiveContext
    {
        private readonly Func<MobileParty, float> _getSeeingRange;
        private readonly Func<MobileParty, IEnumerable<RoamingPartyStrength>> _getRoamingParties;
        private readonly Func<MobileParty, IEnumerable<MobileParty>> _getAccompanyingParties;

        protected BannerlordSettlementOffensiveContext(Func<MobileParty, float> getSeeingRange,
            Func<MobileParty, IEnumerable<RoamingPartyStrength>> getRoamingParties,
            Func<MobileParty, IEnumerable<MobileParty>> getAccompanyingParties)
        {
            _getSeeingRange = getSeeingRange;
            _getRoamingParties = getRoamingParties;
            _getAccompanyingParties = getAccompanyingParties;
        }

        /**
         * Escorts spawned alongside the party fight for it, so they count towards what it can take on.
         * Once the force has become an army the army total already includes them.
         */
        public float GetAttackerStrength(MobileParty party)
        {
            if (party.Army is not null && party.Army.LeaderParty == party)
            {
                return party.Army.CalculateCurrentStrength();
            }

            return party.Party.CalculateCurrentStrength()
                   + _getAccompanyingParties(party).Sum(p => p.Party.CalculateCurrentStrength());
        }

        public float GetVisibleHostileStrength(MobileParty observer)
        {
            return SettlementOffensiveBehaviour.SumVisibleHostileStrength(
                _getRoamingParties(observer), _getSeeingRange(observer));
        }

        public Settlement? FindClosestNonHostileSettlement(MobileParty party)
        {
            return Settlement.All.Where(s => s.IsActive && (s.IsTown || s.IsCastle || s.IsVillage) &&
                                             !party.MapFaction.IsAtWarWith(s.MapFaction))
                .OrderBy(s => party.Position.Distance(s.GatePosition))
                .FirstOrDefault();
        }

        public AiBehavior GetShortTermBehavior(MobileParty party) => party.ShortTermBehavior;

        public AIBehaviorData CreatePatrolBehavior(MobileParty party, Settlement target)
        {
            return new AIBehaviorData(target, AiBehavior.PatrolAroundPoint,
                party.NavigationCapability, false, false, false);
        }

        protected static IEnumerable<RoamingPartyStrength> ObserveRoamingParties(MobileParty observer)
        {
            return MobileParty.All
                .Where(p => p.CurrentSettlement is null && p.MapEvent is null)
                .Select(p => new RoamingPartyStrength(p == observer, p.IsActive,
                    p.MapFaction.IsAtWarWith(observer.MapFaction), p.Army,
                    observer.Position.Distance(p.Position), p.Party.CalculateCurrentStrength()));
        }
    }
}
