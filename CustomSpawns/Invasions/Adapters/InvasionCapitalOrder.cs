using System;
using CustomSpawns.AI;
using CustomSpawns.Invasions.CapitalObjectives;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace CustomSpawns.Invasions.Adapters
{
    public abstract class InvasionCapitalOrder : IPartyOrder
    {
        private readonly IInvasionCapitalObjectiveQuery _invasionCapitalObjectiveQuery;
        private readonly InvasionCapitalOrderMapping _invasionCapitalOrderMapping = new();

        protected InvasionCapitalOrder(IInvasionCapitalObjectiveQuery invasionCapitalObjectiveQuery)
        {
            _invasionCapitalObjectiveQuery = invasionCapitalObjectiveQuery ??
                                               throw new ArgumentNullException(
                                                   nameof(invasionCapitalObjectiveQuery));
        }

        protected abstract AiBehavior Behavior { get; }
        protected abstract InvasionCapitalObjectiveKind Kind { get; }

        public AIBehaviorData? Find(MobileParty party)
        {
            if (!IsEligible(party))
            {
                return null;
            }

            InvasionCapitalObjectiveDirective? directive =
                _invasionCapitalObjectiveQuery.FindForKingdom(party.MapFaction.StringId);
            if (!directive.HasValue || !string.Equals(directive.Value.CommanderPartyId,
                    party.StringId, StringComparison.Ordinal))
            {
                return null;
            }

            InvasionCapitalOrderIntent? intent = _invasionCapitalOrderMapping.Find(
                directive.Value.Kind, directive.Value.Phase, Kind, Behavior);
            if (!intent.HasValue)
            {
                return null;
            }

            Settlement? target = Settlement.Find(directive.Value.TargetSettlementId);
            if (target == null || !target.IsActive || !target.IsFortification)
            {
                return null;
            }

            bool isTargetingPort = IsTargetingPort(party, target);
            AiHelper.GetBestNavigationTypeAndAdjustedDistanceOfSettlementForMobileParty(party, target,
                isTargetingPort, out MobileParty.NavigationType navigation, out _, out bool isFromPort);
            if (navigation == MobileParty.NavigationType.None)
            {
                return null;
            }

            return new AIBehaviorData(target, intent.Value.Behavior, navigation,
                intent.Value.WillGatherArmy, isFromPort, isTargetingPort);
        }

        protected virtual bool IsTargetingPort(MobileParty party, Settlement target) => false;

        private static bool IsEligible(MobileParty party)
        {
            return party.IsActive && !party.IsMainParty && party.IsLordParty && !party.IsDisbanding &&
                   party.MapEvent == null && party.CurrentSettlement?.SiegeEvent == null &&
                   party.MapFaction != null &&
                   (party.Army == null || party.Army.LeaderParty == party);
        }
    }
}
