using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Localization;

namespace CustomSpawns.Invasions.Alliances.Adapters
{
    /**
     * No kingdom trades with an invasion's realm but the other realms of its line; see BannerlordInvasionPacts.
     * Vanilla answers everything else.
     */
    public sealed class InvasionTradeAgreementModel : TradeAgreementModel
    {
        private readonly TradeAgreementModel _tradeAgreementModel;
        private readonly BannerlordInvasionPacts _bannerlordInvasionPacts;

        public InvasionTradeAgreementModel(TradeAgreementModel decorated,
            BannerlordInvasionPacts bannerlordInvasionPacts)
        {
            _tradeAgreementModel = decorated;
            _bannerlordInvasionPacts = bannerlordInvasionPacts;
        }

        public override bool CanMakeTradeAgreement(Kingdom kingdom, Kingdom other, bool checkOtherSideTradeSupport,
            out TextObject reason, bool includeReason = false)
        {
            if (!_bannerlordInvasionPacts.MayMakePact(kingdom, other))
            {
                reason = new TextObject("{=cs_invasion_no_trade}No kingdom trades with an invader.");
                return false;
            }

            return _tradeAgreementModel.CanMakeTradeAgreement(kingdom, other, checkOtherSideTradeSupport, out reason,
                includeReason);
        }

        public override int GetProfitPerCaravanVisit(MobileParty mobileParty) =>
            _tradeAgreementModel.GetProfitPerCaravanVisit(mobileParty);

        public override CampaignTime GetTradeAgreementDurationInYears(Kingdom iniatatingKingdom,
            Kingdom otherKingdom) =>
            _tradeAgreementModel.GetTradeAgreementDurationInYears(iniatatingKingdom, otherKingdom);

        public override int GetMaximumTradeAgreementCount(Kingdom kingdom) =>
            _tradeAgreementModel.GetMaximumTradeAgreementCount(kingdom);

        public override int GetInfluenceCostOfProposingTradeAgreement(Clan clan) =>
            _tradeAgreementModel.GetInfluenceCostOfProposingTradeAgreement(clan);

        public override float GetScoreOfStartingTradeAgreement(Kingdom kingdom, Kingdom targetKingdom, Clan clan,
            out TextObject explanation, bool includeExplanation = false) =>
            _tradeAgreementModel.GetScoreOfStartingTradeAgreement(kingdom, targetKingdom, clan, out explanation,
                includeExplanation);
    }
}
