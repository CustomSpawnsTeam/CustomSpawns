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
        private readonly BannerlordCapitalObjectivePartyEligibility _partyEligibility = new();

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

            bool gathering = directive.Value.Phase == InvasionCapitalObjectivePhase.Gathering;
            string? settlementId = gathering
                ? directive.Value.StagingSettlementId
                : directive.Value.TargetSettlementId;
            Settlement? target = settlementId == null ? null : Settlement.Find(settlementId);
            if (target == null || !target.IsActive ||
                (gathering ? target.IsUnderSiege || target.IsUnderRaid || target.Party.MapEvent != null
                    : !target.IsFortification))
            {
                return null;
            }

            if (gathering && party.Army?.LeaderParty != party)
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

            intent.Value.PrepareMovement(party.ThinkParamsCache,
                () => MoveToStaging(party, target, navigation));

            return new AIBehaviorData(target, intent.Value.Behavior, navigation,
                false, isFromPort, isTargetingPort);
        }

        private static void MoveToStaging(MobileParty party, Settlement staging,
            MobileParty.NavigationType navigation)
        {
            Army army = party.Army;
            if (party.DefaultBehavior == AiBehavior.GoToPoint && army.AiBehaviorObject == staging)
            {
                return;
            }

            army.AiBehaviorObject = staging;
            party.SetMoveGoToPoint(NavigationHelper.FindReachablePointAroundPosition(
                staging.GatePosition, navigation, army.GatheringPositionMaxDistanceToTheSettlement,
                army.GatheringPositionMinDistanceToTheSettlement), navigation);
        }

        protected virtual bool IsTargetingPort(MobileParty party, Settlement target) => false;

        private bool IsEligible(MobileParty party)
        {
            return party.IsActive && !party.IsMainParty && _partyEligibility.CanCommand(party) &&
                   !party.IsDisbanding && party.MapEvent == null &&
                   party.CurrentSettlement?.SiegeEvent == null && party.MapFaction != null;
        }
    }
}
