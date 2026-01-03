using System.Collections.Generic;
using CustomSpawns.CampaignData.Implementations;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.BarterSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.LogEntries;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;

namespace CustomSpawns.Diplomacy
{
    public class CustomSpawnsDiplomacyModel : DiplomacyModel
    {
        private readonly DiplomacyModel _diplomacyModel;
        private readonly CustomSpawnsDiplomacyProvider _customSpawnsDiplomacyProvider;
        private readonly NonMercenaryCustomSpawnsFactionsProvider _nonMercenaryCustomSpawnsFactionsProvider;
        private readonly DailyLogger _dailyLogger;

        public CustomSpawnsDiplomacyModel(DiplomacyModel diplomacyModel, CustomSpawnsDiplomacyProvider customSpawnsDiplomacyProvider, DailyLogger dailyLogger, NonMercenaryCustomSpawnsFactionsProvider nonMercenaryCustomSpawnsFactionsProvider)
        {
            _diplomacyModel = diplomacyModel;
            _customSpawnsDiplomacyProvider = customSpawnsDiplomacyProvider;
            _dailyLogger = dailyLogger;
            _nonMercenaryCustomSpawnsFactionsProvider = nonMercenaryCustomSpawnsFactionsProvider;
        }

        public override float GetStrengthThresholdForNonMutualWarsToBeIgnoredToJoinKingdom(Kingdom kingdomToJoin)
        {
            return _diplomacyModel.GetStrengthThresholdForNonMutualWarsToBeIgnoredToJoinKingdom(kingdomToJoin);
        }

        public override float GetRelationIncreaseFactor(Hero hero1, Hero hero2, float relationValue)
        {
            return _diplomacyModel.GetRelationIncreaseFactor(hero1, hero2, relationValue);
        }

        public override int GetInfluenceAwardForSettlementCapturer(Settlement settlement)
        {
            return _diplomacyModel.GetInfluenceAwardForSettlementCapturer(settlement);
        }

        public override float GetHourlyInfluenceAwardForRaidingEnemyVillage(MobileParty mobileParty)
        {
            return _diplomacyModel.GetHourlyInfluenceAwardForRaidingEnemyVillage(mobileParty);
        }

        public override float GetHourlyInfluenceAwardForBesiegingEnemyFortification(MobileParty mobileParty)
        {
            return _diplomacyModel.GetHourlyInfluenceAwardForBesiegingEnemyFortification(mobileParty);
        }

        public override float GetHourlyInfluenceAwardForBeingArmyMember(MobileParty mobileParty)
        {
            return _diplomacyModel.GetHourlyInfluenceAwardForBeingArmyMember(mobileParty);
        }

        public override float GetScoreOfClanToJoinKingdom(Clan clan, Kingdom kingdom)
        {
            return _diplomacyModel.GetScoreOfClanToJoinKingdom(clan, kingdom);
        }

        public override float GetScoreOfClanToLeaveKingdom(Clan clan, Kingdom kingdom)
        {
            return _diplomacyModel.GetScoreOfClanToLeaveKingdom(clan, kingdom);
        }

        public override float GetScoreOfKingdomToGetClan(Kingdom kingdom, Clan clan)
        {
            return _diplomacyModel.GetScoreOfKingdomToGetClan(kingdom, clan);
        }

        public override float GetScoreOfKingdomToSackClan(Kingdom kingdom, Clan clan)
        {
            return _diplomacyModel.GetScoreOfKingdomToSackClan(kingdom, clan);
        }

        public override float GetScoreOfMercenaryToJoinKingdom(Clan clan, Kingdom kingdom)
        {
            if (_nonMercenaryCustomSpawnsFactionsProvider.GetNonMercenaryCustomSpawnsFactions().Contains(clan.StringId))
            {
                return 0f;
            }
            
            return _diplomacyModel.GetScoreOfMercenaryToJoinKingdom(clan, kingdom);
        }

        public override float GetScoreOfMercenaryToLeaveKingdom(Clan clan, Kingdom kingdom)
        {
            if (_nonMercenaryCustomSpawnsFactionsProvider.GetNonMercenaryCustomSpawnsFactions().Contains(clan.StringId))
            {
                return 1f;
            }
            
            return _diplomacyModel.GetScoreOfMercenaryToJoinKingdom(clan, kingdom);
        }

        public override float GetScoreOfKingdomToHireMercenary(Kingdom kingdom, Clan mercenaryClan)
        {
            return _diplomacyModel.GetScoreOfKingdomToHireMercenary(kingdom, mercenaryClan);
        }

        public override float GetScoreOfKingdomToSackMercenary(Kingdom kingdom, Clan mercenaryClan)
        {
            return _diplomacyModel.GetScoreOfKingdomToSackMercenary(kingdom, mercenaryClan);
        }

        public override float GetScoreOfDeclaringPeaceForClan(IFaction factionDeclaresPeace, IFaction factionDeclaredPeace, Clan evaluatingClan,
            out TextObject reason, bool includeReason = false)
        {
            return _diplomacyModel.GetScoreOfDeclaringPeaceForClan(factionDeclaresPeace, factionDeclaredPeace,
                evaluatingClan, out reason, includeReason);
        }

        public override float GetScoreOfDeclaringPeace(IFaction factionDeclaresPeace, IFaction factionDeclaredPeace)
        {
            return _diplomacyModel.GetScoreOfDeclaringPeace(factionDeclaresPeace, factionDeclaredPeace);
        }

        public override bool IsPeaceSuitable(IFaction factionDeclaresPeace, IFaction factionDeclaredPeace)
        {
            if (_customSpawnsDiplomacyProvider.ShouldBeAtPeace(factionDeclaresPeace, factionDeclaredPeace))
            {
                _dailyLogger.Info("Forcing " + factionDeclaresPeace.Name + " and " + factionDeclaredPeace.Name + " to make war after peace was made due to diplomacy data");
                return true;
            }

            return _diplomacyModel.IsPeaceSuitable(factionDeclaresPeace, factionDeclaredPeace);
        }

        public override float GetScoreOfDeclaringWar(IFaction factionDeclaresWar, IFaction factionDeclaredWar, Clan evaluatingClan,
            out TextObject reason, bool includeReason = false)
        {
            return _diplomacyModel.GetScoreOfDeclaringWar(factionDeclaresWar, factionDeclaredWar, evaluatingClan, out reason,  includeReason);
        }

        public override ExplainedNumber GetWarProgressScore(IFaction factionDeclaresWar, IFaction factionDeclaredWar,
            bool includeDescriptions = false)
        {
            return _diplomacyModel.GetWarProgressScore(factionDeclaresWar, factionDeclaredWar, includeDescriptions);
        }

        public override float GetScoreOfLettingPartyGo(MobileParty party, MobileParty partyToLetGo)
        {
            return _diplomacyModel.GetScoreOfLettingPartyGo(party, partyToLetGo);
        }

        public override float GetValueOfHeroForFaction(Hero examinedHero, IFaction targetFaction, bool forMarriage = false)
        {
            return _diplomacyModel.GetValueOfHeroForFaction(examinedHero, targetFaction, forMarriage);
        }

        public override int GetRelationCostOfExpellingClanFromKingdom()
        {
            return _diplomacyModel.GetRelationCostOfExpellingClanFromKingdom();
        }

        public override int GetInfluenceCostOfSupportingClan()
        {
            return _diplomacyModel.GetInfluenceCostOfSupportingClan();
        }

        public override int GetInfluenceCostOfExpellingClan(Clan proposingClan)
        {
            return _diplomacyModel.GetInfluenceCostOfExpellingClan(proposingClan);
        }

        public override int GetInfluenceCostOfProposingPeace(Clan proposingClan)
        {
            return _diplomacyModel.GetInfluenceCostOfProposingPeace(proposingClan);
        }

        public override int GetInfluenceCostOfProposingWar(Clan proposingClan)
        {
            return _diplomacyModel.GetInfluenceCostOfProposingWar(proposingClan);
        }

        public override int GetInfluenceValueOfSupportingClan()
        {
            return _diplomacyModel.GetInfluenceValueOfSupportingClan();
        }

        public override int GetRelationValueOfSupportingClan()
        {
            return _diplomacyModel.GetRelationValueOfSupportingClan();
        }

        public override int GetInfluenceCostOfAnnexation(Clan proposingClan)
        {
            return _diplomacyModel.GetInfluenceCostOfAnnexation(proposingClan);
        }

        public override int GetInfluenceCostOfChangingLeaderOfArmy()
        {
            return _diplomacyModel.GetInfluenceCostOfChangingLeaderOfArmy();
        }

        public override int GetInfluenceCostOfDisbandingArmy()
        {
            return _diplomacyModel.GetInfluenceCostOfDisbandingArmy();
        }

        public override int GetRelationCostOfDisbandingArmy(bool isLeaderParty)
        {
            return _diplomacyModel.GetRelationCostOfDisbandingArmy(isLeaderParty);
        }

        public override int GetInfluenceCostOfPolicyProposalAndDisavowal(Clan proposingClan)
        {
            return _diplomacyModel.GetInfluenceCostOfPolicyProposalAndDisavowal(proposingClan);
        }

        public override int GetInfluenceCostOfAbandoningArmy()
        {
            return _diplomacyModel.GetInfluenceCostOfAbandoningArmy();
        }

        public override int GetEffectiveRelation(Hero hero, Hero hero1)
        {
            return _diplomacyModel.GetEffectiveRelation(hero, hero1);
        }

        public override int GetBaseRelation(Hero hero, Hero hero1)
        {
            return _diplomacyModel.GetBaseRelation(hero, hero1);
        }

        public override void GetHeroesForEffectiveRelation(Hero hero1, Hero hero2, out Hero effectiveHero1, out Hero effectiveHero2)
        {
            _diplomacyModel.GetHeroesForEffectiveRelation(hero1, hero2, out effectiveHero1, out effectiveHero2);
        }

        public override int GetRelationChangeAfterClanLeaderIsDead(Hero deadLeader, Hero relationHero)
        {
            return _diplomacyModel.GetRelationChangeAfterClanLeaderIsDead(deadLeader, relationHero);
        }

        public override int GetRelationChangeAfterVotingInSettlementOwnerPreliminaryDecision(Hero supporter, bool hasHeroVotedAgainstOwner)
        {
            return _diplomacyModel.GetRelationChangeAfterVotingInSettlementOwnerPreliminaryDecision(supporter, hasHeroVotedAgainstOwner);
        }

        public override float GetClanStrength(Clan clan)
        {
            return _diplomacyModel.GetClanStrength(clan);
        }

        public override float GetHeroCommandingStrengthForClan(Hero hero)
        {
            return _diplomacyModel.GetHeroCommandingStrengthForClan(hero);
        }

        public override float GetHeroGoverningStrengthForClan(Hero hero)
        {
            return _diplomacyModel.GetHeroGoverningStrengthForClan(hero);
        }

        public override uint GetNotificationColor(ChatNotificationType notificationType)
        {
            return _diplomacyModel.GetNotificationColor(notificationType);
        }

        public override int GetDailyTributeToPay(Clan factionToPay, Clan factionToReceive, out int tributeDurationInDays)
        {
            return _diplomacyModel.GetDailyTributeToPay(factionToPay, factionToReceive, out tributeDurationInDays);
        }

        public override float GetDecisionMakingThreshold(IFaction consideringFaction)
        {
            return _diplomacyModel.GetDecisionMakingThreshold(consideringFaction);
        }

        public override float GetValueOfSettlementsForFaction(IFaction faction)
        {
            return _diplomacyModel.GetValueOfSettlementsForFaction(faction);
        }

        public override bool CanSettlementBeGifted(Settlement settlement)
        {
            return _diplomacyModel.CanSettlementBeGifted(settlement);
        }

        public override bool IsClanEligibleToBecomeRuler(Clan clan)
        {
            return _diplomacyModel.IsClanEligibleToBecomeRuler(clan);
        }

        public override IEnumerable<BarterGroup> GetBarterGroups()
        {
            return _diplomacyModel.GetBarterGroups();
        }

        public override int GetCharmExperienceFromRelationGain(Hero hero, float relationChange, ChangeRelationAction.ChangeRelationDetail detail)
        {
            return _diplomacyModel.GetCharmExperienceFromRelationGain(hero, relationChange, detail);
        }

        public override float DenarsToInfluence()
        {
            return _diplomacyModel.DenarsToInfluence();
        }

        public override DiplomacyStance? GetShallowDiplomaticStance(IFaction faction1, IFaction faction2)
        {
            return _diplomacyModel.GetShallowDiplomaticStance(faction1, faction2);
        }

        public override DiplomacyStance GetDefaultDiplomaticStance(IFaction faction1, IFaction faction2)
        {
            return _diplomacyModel.GetDefaultDiplomaticStance(faction1, faction2);
        }

        public override bool IsAtConstantWar(IFaction attacker, IFaction enemy)
        {
            if (_customSpawnsDiplomacyProvider.ShouldBeAtConstantWar(attacker, enemy))
            {
                // _dailyLogger.Info("Forcing " + attacker.Name + " and " + enemy.Name + " to make war after peace was made due to diplomacy data");
                return true;
            }
            
            return _diplomacyModel.IsAtConstantWar(attacker, enemy);
        }

        public override int MaxRelationLimit => _diplomacyModel.MaxRelationLimit;
        public override int MinRelationLimit => _diplomacyModel.MinRelationLimit;
        public override int MaxNeutralRelationLimit => _diplomacyModel.MaxNeutralRelationLimit;
        public override int MinNeutralRelationLimit => _diplomacyModel.MinNeutralRelationLimit;
        public override int MinimumRelationWithConversationCharacterToJoinKingdom => _diplomacyModel.MinimumRelationWithConversationCharacterToJoinKingdom;
        public override int GiftingTownRelationshipBonus => _diplomacyModel.GiftingTownRelationshipBonus;
        public override int GiftingCastleRelationshipBonus => _diplomacyModel.GiftingCastleRelationshipBonus;
        public override float WarDeclarationScorePenaltyAgainstAllies => _diplomacyModel.WarDeclarationScorePenaltyAgainstAllies;
        public override float WarDeclarationScoreBonusAgainstEnemiesOfAllies => _diplomacyModel.WarDeclarationScoreBonusAgainstEnemiesOfAllies;
    }
}