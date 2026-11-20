using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace CustomSpawns.Invasions.Alliances.Adapters
{
    /**
     * No kingdom allies with an invasion's realm but the other realms of its line; see BannerlordInvasionPacts.
     * Vanilla answers everything else.
     */
    public sealed class InvasionAllianceModel : AllianceModel
    {
        private readonly AllianceModel _allianceModel;
        private readonly BannerlordInvasionPacts _bannerlordInvasionPacts;

        public InvasionAllianceModel(AllianceModel decorated, BannerlordInvasionPacts bannerlordInvasionPacts)
        {
            _allianceModel = decorated;
            _bannerlordInvasionPacts = bannerlordInvasionPacts;
        }

        public override CampaignTime MaxDurationOfAlliance => _allianceModel.MaxDurationOfAlliance;

        public override CampaignTime MaxDurationOfWarParticipation => _allianceModel.MaxDurationOfWarParticipation;

        public override int MaxNumberOfAlliances => _allianceModel.MaxNumberOfAlliances;

        public override CampaignTime DurationForOffers => _allianceModel.DurationForOffers;

        public override bool CanMakeAlliance(Kingdom kingdom, Kingdom targetKingdom, IFaction evaluatingFaction,
            out TextObject reason, bool includeReason = false)
        {
            if (!_bannerlordInvasionPacts.MayMakePact(kingdom, targetKingdom))
            {
                reason = new TextObject("{=cs_invasion_no_alliance}No kingdom allies with an invader.");
                return false;
            }

            return _allianceModel.CanMakeAlliance(kingdom, targetKingdom, evaluatingFaction, out reason,
                includeReason);
        }

        public override int GetCallToWarCost(Kingdom callingKingdom, Kingdom calledKingdom,
            Kingdom kingdomToCallToWarAgainst) =>
            _allianceModel.GetCallToWarCost(callingKingdom, calledKingdom, kingdomToCallToWarAgainst);

        public override ExplainedNumber GetScoreOfStartingAlliance(Kingdom kingdomDeclaresAlliance,
            Kingdom kingdomDeclaredAlliance, out TextObject explanation, bool includeDescription = false) =>
            _allianceModel.GetScoreOfStartingAlliance(kingdomDeclaresAlliance, kingdomDeclaredAlliance,
                out explanation, includeDescription);

        public override float GetSupportScoreOfStartingAllianceForClan(Kingdom kingdomDeclaresAlliance,
            Kingdom kingdomDeclaredAlliance, Clan evaluatingClan, out TextObject explanation,
            bool includeDescription = false) =>
            _allianceModel.GetSupportScoreOfStartingAllianceForClan(kingdomDeclaresAlliance, kingdomDeclaredAlliance,
                evaluatingClan, out explanation, includeDescription);

        public override float GetScoreOfCallingToWar(Kingdom callingKingdom, Kingdom calledKingdom,
            Kingdom kingdomToCallToWarAgainst, IFaction evaluatingFaction, out TextObject reason) =>
            _allianceModel.GetScoreOfCallingToWar(callingKingdom, calledKingdom, kingdomToCallToWarAgainst,
                evaluatingFaction, out reason);

        public override float GetScoreOfJoiningWar(Kingdom offeringKingdom, Kingdom kingdomToOfferToJoinWarWith,
            Kingdom kingdomToOfferToJoinWarAgainst, IFaction evaluatingFaction, out TextObject reason) =>
            _allianceModel.GetScoreOfJoiningWar(offeringKingdom, kingdomToOfferToJoinWarWith,
                kingdomToOfferToJoinWarAgainst, evaluatingFaction, out reason);

        public override int GetInfluenceCostOfProposingStartingAlliance(Clan proposingClan) =>
            _allianceModel.GetInfluenceCostOfProposingStartingAlliance(proposingClan);

        public override int GetInfluenceCostOfCallingToWar(Clan proposingClan) =>
            _allianceModel.GetInfluenceCostOfCallingToWar(proposingClan);

        public override float GetAllianceFactorForDeclaringWar(IFaction factionDeclaresWar,
            IFaction factionDeclaredWar) =>
            _allianceModel.GetAllianceFactorForDeclaringWar(factionDeclaresWar, factionDeclaredWar);

        public override float GetAllianceFactorForDeclaringPeace(IFaction factionDeclaresPeace,
            IFaction factionDeclaredPeace) =>
            _allianceModel.GetAllianceFactorForDeclaringPeace(factionDeclaresPeace, factionDeclaredPeace);

        public override Clan GetProposerClanForAllianceDecision(Kingdom proposerKingdom, Kingdom proposedKingdom) =>
            _allianceModel.GetProposerClanForAllianceDecision(proposerKingdom, proposedKingdom);
    }
}
