using System;
using CustomSpawns.AI;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace CustomSpawns.Invasions.Adapters
{
    /**
     * Keeps a garrison army fighting. Vanilla gathers it and leads it, but its lord, left to himself, would march
     * after whatever town he fancied and break the army up once it took one. Once gathered he is told to besiege
     * the conquest's nearest town, and with none to take, to defend the capital: holding or patrolling would count
     * as idleness, and vanilla disperses an army that idles two days.
     */
    public sealed class InvasionGarrisonArmyOrder : IPartyOrder
    {
        private readonly Func<InvasionState> _state;
        private readonly BannerlordInvasionGarrisonArmyTargets _bannerlordInvasionGarrisonArmyTargets;
        private readonly IInvasionGarrisonArmyLeaders _invasionGarrisonArmyLeaders;

        public InvasionGarrisonArmyOrder(Func<InvasionState> state,
            BannerlordInvasionGarrisonArmyTargets bannerlordInvasionGarrisonArmyTargets,
            IInvasionGarrisonArmyLeaders invasionGarrisonArmyLeaders)
        {
            _state = state;
            _bannerlordInvasionGarrisonArmyTargets = bannerlordInvasionGarrisonArmyTargets;
            _invasionGarrisonArmyLeaders = invasionGarrisonArmyLeaders;
        }

        public AIBehaviorData? Find(MobileParty party)
        {
            Army? army = party.Army;

            if (army == null || army.LeaderParty != party || !IsFree(party) || army.IsWaitingForArmyMembers() ||
                !_invasionGarrisonArmyLeaders.LeadsGarrisonArmy(party.LeaderHero?.StringId))
            {
                return null;
            }

            Invasion? invasion = _state().FindFightingInvasion(party.ActualClan?.StringId);

            if (invasion == null)
            {
                return null;
            }

            Settlement? target = _bannerlordInvasionGarrisonArmyTargets.FindTarget(invasion, party.Position);

            if (target != null)
            {
                return Order(party, target, AiBehavior.BesiegeSettlement);
            }

            Settlement? home = _bannerlordInvasionGarrisonArmyTargets.FindHeldCapital(invasion) ??
                               _bannerlordInvasionGarrisonArmyTargets.FindNearestRealmFortification(invasion, party);

            return home == null ? null : Order(party, home, AiBehavior.DefendSettlement);
        }

        private static AIBehaviorData? Order(MobileParty party, Settlement settlement, AiBehavior behavior)
        {
            AiHelper.GetBestNavigationTypeAndAdjustedDistanceOfSettlementForMobileParty(party, settlement, false,
                out MobileParty.NavigationType navigation, out _, out bool isFromPort);

            return navigation == MobileParty.NavigationType.None
                ? null
                : new AIBehaviorData(settlement, behavior, navigation, false, isFromPort, false);
        }

        private static bool IsFree(MobileParty party) => party.IsActive && !party.IsMainParty && !party.IsDisbanding &&
            party.MapEvent == null && party.CurrentSettlement?.SiegeEvent == null;
    }
}
