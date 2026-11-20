using System;
using System.Collections.Generic;
using System.Linq;
using CustomSpawns.AI.Model;
using CustomSpawns.AI.Offensives;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace CustomSpawns.AI.Adapters
{
    /**
     * The Bannerlord lookups a settlement offensive needs whether it raids or besieges: how strong the
     * attacking force is, what the party can see around itself, and where it waits while it has no
     * target. Only target selection differs between the two, so only that is left to the
     * behaviour-specific ports.
     */
    public abstract class BannerlordSettlementOffensiveAdapter
    {
        private readonly Func<MobileParty, float> _getSeeingRange;
        private readonly Func<MobileParty, IEnumerable<RoamingPartyStrength>> _getRoamingParties;
        private readonly Func<MobileParty, IEnumerable<MobileParty>> _getAccompanyingParties;

        protected BannerlordSettlementOffensiveAdapter(Func<MobileParty, float> getSeeingRange,
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
         * A party inside an army it does not lead is knowingly assessed on its own strength plus its
         * escorts: the rest of the army is left out, and an escort that is itself an army member is
         * counted here even though the army leader counts it too. That is the long-standing besiege
         * assessment, kept as it is so raid inherits it rather than besiege targeting changing.
         */
        public virtual float GetAttackerStrength(MobileParty party)
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
            return OffensiveStrengthRules.SumVisibleHostileStrength(
                _getRoamingParties(observer), _getSeeingRange(observer));
        }

        protected float GetSeeingRange(MobileParty observer) => _getSeeingRange(observer);

        protected IEnumerable<MobileParty> GetAccompanyingParties(MobileParty party) =>
            _getAccompanyingParties(party);

        public Settlement? FindClosestNonHostileSettlement(MobileParty party)
        {
            return Settlement.All.Where(s => s.IsActive && (s.IsTown || s.IsCastle || s.IsVillage) &&
                                             !party.MapFaction.IsAtWarWith(s.MapFaction))
                .OrderBy(s => party.Position.Distance(s.GatePosition))
                .FirstOrDefault();
        }

        public AIBehaviorData CreatePatrolBehavior(MobileParty party, Settlement target)
        {
            return new AIBehaviorData(target, AiBehavior.PatrolAroundPoint,
                party.NavigationCapability, false, false, false);
        }

        protected static IEnumerable<RoamingPartyStrength> ObserveRoamingParties(MobileParty observer)
        {
            // Every reader of this sweep wants live hostiles and nobody else, and working out what a party
            // is worth is the expensive part of describing one. So the ones no reader can use are dropped
            // before that, rather than priced first and discarded after, once an hour for every party.
            return MobileParty.All
                .Where(p => p.CurrentSettlement is null && p.MapEvent is null && p.IsActive &&
                            p.MapFaction.IsAtWarWith(observer.MapFaction))
                .Select(p => new RoamingPartyStrength(p == observer, p.IsActive,
                    p.MapFaction.IsAtWarWith(observer.MapFaction),
                    observer.Position.Distance(p.Position), p.Party.CalculateCurrentStrength(),
                    p.IsLordParty, p.IsFleeing(), p.IsVillager || p.IsCaravan));
        }
    }
}
