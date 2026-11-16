using System.Linq;
using CustomSpawns.AI;
using Helpers;
using NLog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace CustomSpawns.Invasions.Bank.Adapters
{
    // Maps campaign facts and the domain's destination to the normal hourly AI score. Short-term
    // avoidance remains vanilla's, so a returning lord can still flee a stronger enemy on the way.
    public sealed class BannerlordInvasionResupplyOrders : IInvasionResupplyRoutes
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly BannerlordInvasionWarbands _bannerlordInvasionWarbands;

        public BannerlordInvasionResupplyOrders(BannerlordInvasionWarbands bannerlordInvasionWarbands)
        {
            _bannerlordInvasionWarbands = bannerlordInvasionWarbands;
        }

        public void AddBehaviorScore(MobileParty party, PartyThinkParams thinkParams,
            InvasionResupply invasionResupply)
        {
            bool wasReturning = invasionResupply.FindReturningParties().Contains(party.StringId);
            var snapshot = new InvasionResupplyParty(_bannerlordInvasionWarbands.Snapshot(party),
                party.PartySizeRatio, isMainParty: party.IsMainParty, isInArmy: party.Army != null,
                isInEncounter: party.MapEvent != null || party.SiegeEvent != null ||
                               party.CurrentSettlement?.SiegeEvent != null,
                isDisbanding: party.IsDisbanding,
                isMovementLocked: !party.IsActive || party.LeaderHero == null ||
                                  party.Ai.DoNotMakeNewDecisions || party.IsInRaftState);
            string? destination = invasionResupply.FindDestination(snapshot,
                Campaign.Current.Models.ArmyManagementCalculationModel.AIMobilePartySizeRatioToCallToArmy);

            if (destination == null)
            {
                if (wasReturning)
                {
                    Logger.Info("Invasion resupply order ended: party=" + party.StringId +
                                " troops=" + party.Party.NumberOfAllMembers +
                                " strength=" + party.Party.CalculateCurrentStrength() +
                                " party_limit=" + party.Party.PartySizeLimit +
                                " size_ratio=" + party.PartySizeRatio);
                }

                return;
            }

            Settlement? capital = Settlement.Find(destination);

            if (capital == null || !TryFindNavigation(party, capital, out MobileParty.NavigationType navigation,
                    out bool isFromPort))
            {
                invasionResupply.Remove(party.StringId);
                return;
            }

            var behavior = new AIBehaviorData(capital, AiBehavior.GoToSettlement,
                navigation, false, isFromPort, false);

            if (thinkParams.TryGetBehaviorScore(behavior, out _))
            {
                thinkParams.SetBehaviorScore(behavior, CustomSpawnsAiBehaviour.DominantAiBehaviorScore);
            }
            else
            {
                thinkParams.AddBehaviorScore((behavior, CustomSpawnsAiBehaviour.DominantAiBehaviorScore));
            }

            if (!wasReturning)
            {
                Logger.Info("Invasion resupply ordered: party=" + party.StringId + " capital=" + destination +
                            " troops=" + party.Party.NumberOfAllMembers +
                            " strength=" + party.Party.CalculateCurrentStrength() +
                            " party_limit=" + party.Party.PartySizeLimit +
                            " size_ratio=" + party.PartySizeRatio);
            }
        }

        public bool IsUnderSiege(string settlementId) =>
            Settlement.Find(settlementId)?.SiegeEvent != null;

        public bool CanReach(string partyId, string settlementId)
        {
            MobileParty? party = Campaign.Current.CampaignObjectManager.Find<MobileParty>(partyId);
            Settlement? capital = Settlement.Find(settlementId);

            if (party == null || capital == null)
            {
                return false;
            }

            return TryFindNavigation(party, capital, out _, out _);
        }

        private bool TryFindNavigation(MobileParty party, Settlement capital,
            out MobileParty.NavigationType navigation, out bool isFromPort)
        {
            if (party.CurrentSettlement == capital)
            {
                navigation = MobileParty.NavigationType.Default;
                isFromPort = false;
                return true;
            }

            AiHelper.GetBestNavigationTypeAndAdjustedDistanceOfSettlementForMobileParty(party, capital,
                false, out navigation, out float distance, out isFromPort);

            return navigation != MobileParty.NavigationType.None && !float.IsNaN(distance) &&
                   !float.IsInfinity(distance) && distance >= 0f && distance < Campaign.MapDiagonal * 5f;
        }
    }
}
