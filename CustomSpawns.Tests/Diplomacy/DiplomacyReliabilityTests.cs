using System.Collections.Generic;
using CustomSpawns.Data.Reader;
using CustomSpawns.Diplomacy;
using CustomSpawns.Invasions;
using Moq;
using NUnit.Framework;
using TaleWorlds.CampaignSystem;

namespace CustomSpawns.Tests.Diplomacy
{
    public class DiplomacyReliabilityTests
    {
        [Test]
        public void ShouldBeAtPeace_UsesPeacePredicate()
        {
            var attacker = Faction("attacker", true, false);
            var target = Faction("target", true, false);
            var data = new Dictionary<string, Data.Model.Diplomacy>
            {
                ["attacker"] = new Data.Model.Diplomacy
                {
                    ForcedWarPeaceDataInstance = new Data.Model.Diplomacy.ForcedWarPeaceData
                    {
                        AtPeaceWithClans = new List<string> { "target" }
                    }
                }
            };
            var suzerainProvider = new Mock<ISuzerainProvider>();
            suzerainProvider.Setup(provider => provider.IsVassal(It.IsAny<IFaction>())).Returns(false);
            var reader = new Mock<IDataReader<Dictionary<string, Data.Model.Diplomacy>>>();
            reader.Setup(provider => provider.Data).Returns(data);

            var provider = new CustomSpawnsDiplomacyProvider(
                new CustomSpawnsClanDiplomacyProvider(suzerainProvider.Object, reader.Object), suzerainProvider.Object);

            Assert.True(provider.ShouldBeAtPeace(attacker, target));
        }

        [Test]
        public void KingdomAttackerWithoutClanPolicy_IsNotForcedIntoWarOrPeace()
        {
            var attacker = Faction("custom_kingdom", false, true);
            var target = Faction("target", true, false);
            var data = new Dictionary<string, Data.Model.Diplomacy>
            {
                ["custom_kingdom"] = new Data.Model.Diplomacy
                {
                    ForcedWarPeaceDataInstance = new Data.Model.Diplomacy.ForcedWarPeaceData()
                }
            };
            var suzerainProvider = new Mock<ISuzerainProvider>();
            var reader = new Mock<IDataReader<Dictionary<string, Data.Model.Diplomacy>>>();
            reader.Setup(provider => provider.Data).Returns(data);
            var provider = new CustomSpawnsClanDiplomacyProvider(suzerainProvider.Object, reader.Object);

            Assert.False(provider.IsWarDeclarationPossible(attacker, target));
            Assert.False(provider.IsPeaceDeclarationPossible(attacker, target));
        }

        [Test]
        public void ForceNoKingdomScores_BlockJoiningAndEncourageLeaving()
        {
            var scores = ScoreProvider("force_no_kingdom");

            float joinScore = scores.GetJoinScore("force_no_kingdom", () => 1f);
            float leaveScore = scores.GetLeaveScore("force_no_kingdom", () => -1f);

            Assert.AreEqual(ForceNoKingdomScoreProvider.ForcedJoinScore, joinScore);
            Assert.AreEqual(ForceNoKingdomScoreProvider.ForcedLeaveScore, leaveScore);
            Assert.AreEqual(-1_000_000_000, (int)joinScore);
            Assert.AreEqual(1_000_000_000, (int)leaveScore);
            Assert.False(float.IsInfinity(leaveScore - joinScore));
        }

        [Test]
        public void ReconciliationScheduler_DefersAndCoalescesRequestsWithoutLosingLaterRequests()
        {
            var scheduler = new DiplomacyReconciliationScheduler();

            scheduler.Request();
            scheduler.Request();

            Assert.True(scheduler.TakePendingRequest());
            Assert.False(scheduler.TakePendingRequest());

            scheduler.Request();
            Assert.True(scheduler.TakePendingRequest());
        }

        [Test]
        public void ForceNoKingdomMembership_WhenAlreadyInKingdom_SelectsCorrectLeaveAction()
        {
            var membership = new ForceNoKingdomMembershipProvider(ForceNoKingdomProvider("force_no_kingdom"));

            Assert.AreEqual(ForceNoKingdomMembershipAction.LeaveKingdom,
                membership.GetAction("force_no_kingdom", hasKingdom: true, isMercenary: false,
                    rulesOwnKingdom: false));
            Assert.AreEqual(ForceNoKingdomMembershipAction.LeaveKingdomAsMercenary,
                membership.GetAction("force_no_kingdom", hasKingdom: true, isMercenary: true,
                    rulesOwnKingdom: false));
            Assert.AreEqual(ForceNoKingdomMembershipAction.None,
                membership.GetAction("force_no_kingdom", hasKingdom: false, isMercenary: false,
                    rulesOwnKingdom: false));
        }

        [Test]
        public void ForceNoKingdomMembership_WhenRulingItsOwnKingdom_StaysInIt()
        {
            var membership = new ForceNoKingdomMembershipProvider(ForceNoKingdomProvider("force_no_kingdom"));

            Assert.AreEqual(ForceNoKingdomMembershipAction.None,
                membership.GetAction("force_no_kingdom", hasKingdom: true, isMercenary: false,
                    rulesOwnKingdom: true));
        }

        [Test]
        public void OrdinaryMercenaryLeaveScore_DelegatesToWrappedScore()
        {
            var scores = ScoreProvider("force_no_kingdom");
            var called = false;

            float score = scores.GetLeaveScore("ordinary", () =>
            {
                called = true;
                return 37f;
            });

            Assert.True(called);
            Assert.AreEqual(37f, score);
        }

        [Test]
        public void Reconciliation_WhenConfiguredPeaceIsViolated_RequestsPeace()
        {
            var attacker = Faction("attacker", true, false);
            var target = Faction("target", true, false);
            var data = new Dictionary<string, Data.Model.Diplomacy>
            {
                ["attacker"] = new Data.Model.Diplomacy
                {
                    ForcedWarPeaceDataInstance = new Data.Model.Diplomacy.ForcedWarPeaceData
                    {
                        AtPeaceWithClans = new List<string> { "target" }
                    }
                }
            };
            var suzerainProvider = new Mock<ISuzerainProvider>();
            suzerainProvider.Setup(provider => provider.IsVassal(It.IsAny<IFaction>())).Returns(false);
            var reader = new Mock<IDataReader<Dictionary<string, Data.Model.Diplomacy>>>();
            reader.Setup(provider => provider.Data).Returns(data);
            var reconciliation = new CustomSpawnsDiplomacyReconciliationProvider(
                new CustomSpawnsClanDiplomacyProvider(suzerainProvider.Object, reader.Object));

            Assert.AreEqual(DiplomacyReconciliationAction.MakePeace,
                reconciliation.GetAction(attacker, target, isAtWar: true));
        }

        [Test]
        public void Reconciliation_WhenMapFactionPoliciesConflict_PrefersConstantWarDeterministically()
        {
            DiplomacyReconciliationAction action = CustomSpawnsDiplomacyReconciliationProvider.Aggregate(new List<DiplomacyReconciliationAction>
            {
                DiplomacyReconciliationAction.MakePeace,
                DiplomacyReconciliationAction.DeclareWar
            }, out bool hasConflict);

            Assert.True(hasConflict);
            Assert.AreEqual(DiplomacyReconciliationAction.DeclareWar, action);
        }

        [TestCase(InvasionWarStance.War, DiplomacyReconciliationAction.MakePeace, DiplomacyReconciliationAction.DeclareWar)]
        [TestCase(InvasionWarStance.Open, DiplomacyReconciliationAction.DeclareWar, DiplomacyReconciliationAction.None)]
        [TestCase(InvasionWarStance.Peace, DiplomacyReconciliationAction.DeclareWar, DiplomacyReconciliationAction.MakePeace)]
        [TestCase(InvasionWarStance.Policy, DiplomacyReconciliationAction.DeclareWar, DiplomacyReconciliationAction.DeclareWar)]
        [TestCase(InvasionWarStance.Policy, DiplomacyReconciliationAction.None, DiplomacyReconciliationAction.None)]
        public void Reconciliation_AConquestsStanceOutranksTheConfiguredPolicy(InvasionWarStance stance,
            DiplomacyReconciliationAction configured, DiplomacyReconciliationAction expected)
        {
            var reconciliation = new CustomSpawnsDiplomacyReconciliationProvider(
                new CustomSpawnsClanDiplomacyProvider(new Mock<ISuzerainProvider>().Object,
                    new Mock<IDataReader<Dictionary<string, Data.Model.Diplomacy>>>().Object));

            Assert.AreEqual(expected, reconciliation.ApplyStance(configured, stance));
        }

        [Test]
        public void MissingConfiguredClanIds_AreCollectedWithoutFirstFailure()
        {
            var reader = new Mock<IDataReader<Dictionary<string, Data.Model.Diplomacy>>>();
            reader.Setup(provider => provider.Data).Returns(new Dictionary<string, Data.Model.Diplomacy>
            {
                ["not_loaded"] = new Data.Model.Diplomacy()
            });
            var provider = new CustomSpawnsDiplomacyFactionsProvider(reader.Object);

            IList<Clan> clans = provider.GetConfiguredClans(out IList<string> missingClanIds);

            Assert.That(clans, Is.Empty);
            Assert.That(missingClanIds, Is.EqualTo(new[] { "not_loaded" }));
        }

        private static ForceNoKingdomScoreProvider ScoreProvider(string forceNoKingdomClanId)
        {
            return new ForceNoKingdomScoreProvider(ForceNoKingdomProvider(forceNoKingdomClanId));
        }

        private static NonMercenaryCustomSpawnsFactionsProvider ForceNoKingdomProvider(string forceNoKingdomClanId)
        {
            var reader = new Mock<IDataReader<Dictionary<string, Data.Model.Diplomacy>>>();
            reader.Setup(provider => provider.Data).Returns(new Dictionary<string, Data.Model.Diplomacy>
            {
                [forceNoKingdomClanId] = new Data.Model.Diplomacy { ForceNoKingdom = true }
            });
            return new NonMercenaryCustomSpawnsFactionsProvider(reader.Object);
        }

        private static IFaction Faction(string id, bool isClan, bool isKingdom)
        {
            var faction = new Mock<IFaction>();
            faction.Setup(value => value.StringId).Returns(id);
            faction.Setup(value => value.IsClan).Returns(isClan);
            faction.Setup(value => value.IsKingdomFaction).Returns(isKingdom);
            faction.Setup(value => value.IsEliminated).Returns(false);
            faction.Setup(value => value.IsBanditFaction).Returns(false);
            return faction.Object;
        }
    }
}
