using System.Collections.Generic;
using System.Linq;
using CustomSpawns.Invasions.Adapters;
using CustomSpawns.Invasions.Conquest;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace CustomSpawns.Invasions.GarrisonArmies.Adapters
{
    // Where a garrison army marches, who is called to it, and where its men go home to.
    public sealed class BannerlordInvasionGarrisonArmyTargets
    {
        private readonly IInvasionConquestTargets _invasionConquestTargets;
        private readonly InvasionNearestChoice _invasionNearestChoice;

        public BannerlordInvasionGarrisonArmyTargets(IInvasionConquestTargets invasionConquestTargets,
            InvasionNearestChoice invasionNearestChoice)
        {
            _invasionConquestTargets = invasionConquestTargets;
            _invasionNearestChoice = invasionNearestChoice;
        }

        // The capital while the invasion's clans hold it.
        public Settlement? FindHeldCapital(Invasion invasion)
        {
            Settlement? capital = invasion.CapitalSettlementId == null
                ? null
                : Settlement.Find(invasion.CapitalSettlementId);

            return capital?.Town?.GarrisonParty != null && invasion.HoldsClan(capital.OwnerClan?.StringId)
                ? capital
                : null;
        }

        /**
         * The fortification nearest the given point of the kingdom the invasion conquers now, so an army that has
         * taken one town moves on to the next, and follows the conquest to its next kingdom once this one falls.
         * Null while that kingdom has nothing left or the realm is not at war with it.
         */
        public Settlement? FindTarget(Invasion invasion, CampaignVec2 from)
        {
            IFaction? realm = invasion.FindLeadingClan()?.MapFaction;
            string? kingdomId = _invasionConquestTargets.FindConquestTargetKingdomId(invasion);
            Kingdom? kingdom = kingdomId == null ? null : Kingdom.All.FirstOrDefault(k => k.StringId == kingdomId);

            if (realm == null || kingdom == null || !realm.IsAtWarWith(kingdom))
            {
                return null;
            }

            return FindNearest(kingdom.Settlements.Where(settlement => settlement.IsFortification &&
                settlement.IsActive), from);
        }

        /**
         * Every lord of the invasion's clans free to answer a call to arms, wherever he is, the way vanilla calls a
         * kingdom's lords: leading his own party, no prisoner, in no army, not fighting or besieging, nor shut in
         * a besieged town. The realm's ruler is never called: see InvasionGarrisonArmyRulerRelease.
         */
        public MBList<MobileParty> FindFreeLords(Invasion invasion, MobileParty leader)
        {
            MBList<MobileParty> lords = new();

            foreach (MobileParty party in MobileParty.All.Where(party => party != leader && party.IsActive &&
                         party.IsLordParty && !party.IsMainParty && !party.IsDisbanding &&
                         invasion.HoldsClan(party.ActualClan?.StringId) && party.MapFaction == leader.MapFaction &&
                         party.LeaderHero is { IsActive: true, IsPrisoner: false, IsKingdomLeader: false } &&
                         party.Army == null &&
                         party.MapEvent == null && party.SiegeEvent == null && party.BesiegedSettlement == null &&
                         party.CurrentSettlement?.SiegeEvent == null))
            {
                lords.Add(party);
            }

            return lords;
        }

        // The realm's fortification nearest the party, for men whose capital is lost.
        public Settlement? FindNearestRealmFortification(Invasion invasion, MobileParty party)
        {
            IFaction? realm = invasion.FindLeadingClan()?.MapFaction;

            return realm == null
                ? null
                : FindNearest(realm.Settlements.Where(settlement => settlement.IsFortification &&
                    settlement.Town != null && !settlement.IsUnderSiege), party.Position);
        }

        private Settlement? FindNearest(IEnumerable<Settlement> settlements, CampaignVec2 from)
        {
            List<Settlement> candidates = settlements.ToList();
            string? nearestId = _invasionNearestChoice.FindNearestId(candidates.Select(settlement =>
                new InvasionCandidateDistance(settlement.StringId, settlement.GatePosition.Distance(from))));

            return candidates.FirstOrDefault(settlement => settlement.StringId == nearestId);
        }
    }
}
