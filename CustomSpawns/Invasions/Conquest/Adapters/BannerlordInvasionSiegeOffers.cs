using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace CustomSpawns.Invasions.Conquest.Adapters
{
    // How vanilla judges a siege for a lord, and offering it one vanilla did not weigh.
    public sealed class BannerlordInvasionSiegeOffers
    {
        public float FindTargetScore(MobileParty party, Settlement fief, float strength) =>
            Campaign.Current.Models.TargetScoreCalculatingModel.GetTargetScoreForFaction(fief,
                Army.ArmyTypes.Besieger, party, strength);

        // The score offered, or nought when the siege is unwanted or the lord has no way there.
        public float AddSiege(MobileParty party, PartyThinkParams thinkParams, Settlement fief, bool willGatherArmy,
            float score)
        {
            if (score <= 0f)
            {
                return 0f;
            }

            AiHelper.GetBestNavigationTypeAndAdjustedDistanceOfSettlementForMobileParty(party, fief, false,
                out MobileParty.NavigationType navigationType, out float _, out bool isFromPort);

            if (navigationType == MobileParty.NavigationType.None)
            {
                return 0f;
            }

            thinkParams.AddBehaviorScore((new AIBehaviorData(fief, AiBehavior.BesiegeSettlement, navigationType,
                willGatherArmy, isFromPort, false), score));
            return score;
        }
    }
}
