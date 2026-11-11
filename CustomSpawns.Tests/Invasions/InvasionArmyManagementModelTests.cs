using CustomSpawns.Invasions;
using System;
using System.Reflection;
using System.Runtime.Serialization;
using CustomSpawns.Invasions.Adapters;
using CustomSpawns.Invasions.CapitalObjectives;
using Moq;
using NUnit.Framework;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.Library;

namespace CustomSpawns.Tests.Invasions
{
    public class InvasionArmyManagementModelTests
    {
        private const string InvaderClanId = "humar_clan";
        private const string CalradianClanId = "vlandia_clan";

        private static readonly FieldInfo InfluenceField =
            typeof(Clan).GetField("_influence", BindingFlags.Instance | BindingFlags.NonPublic)!;

        private delegate bool CanLordCreateArmyCall(MobileParty leaderParty, out MBList<MobileParty> members);

        [Test]
        public void AnInvaderClanWithNoInfluenceMayRaiseAnArmy()
        {
            MobileParty leaderParty = LedBy(InvaderClanId, 0f);
            float influenceSeen = -1f;
            InvasionArmyManagementModel model = Model(VanillaInfluenceRule(seen => influenceSeen = seen));

            bool canCreate = model.CanLordCreateArmy(leaderParty, out MBList<MobileParty> members);

            Assert.True(canCreate);
            Assert.That(influenceSeen, Is.GreaterThan(100f));
            Assert.That(members, Has.Count.EqualTo(1));
        }

        [Test]
        public void TheLiftedInfluenceIsPutBackAfterTheCall()
        {
            MobileParty leaderParty = LedBy(InvaderClanId, 12.5f);
            InvasionArmyManagementModel model = Model(VanillaInfluenceRule(_ => { }));

            model.CanLordCreateArmy(leaderParty, out _);

            Assert.AreEqual(12.5f, leaderParty.LeaderHero.Clan.Influence);
        }

        [Test]
        public void TheLiftedInfluenceIsPutBackWhenTheDecoratedModelThrows()
        {
            MobileParty leaderParty = LedBy(InvaderClanId, 12.5f);
            var decorated = new Mock<ArmyManagementCalculationModel>();
            MBList<MobileParty> members;
            decorated.Setup(model => model.CanLordCreateArmy(It.IsAny<MobileParty>(), out members))
                .Throws(new InvalidOperationException());

            Assert.Throws<InvalidOperationException>(() => Model(decorated).CanLordCreateArmy(leaderParty, out _));
            Assert.AreEqual(12.5f, leaderParty.LeaderHero.Clan.Influence);
        }

        [Test]
        public void ACalradianClanKeepsVanillasInfluenceRule()
        {
            MobileParty leaderParty = LedBy(CalradianClanId, 0f);
            float influenceSeen = -1f;
            InvasionArmyManagementModel model = Model(VanillaInfluenceRule(seen => influenceSeen = seen));

            bool canCreate = model.CanLordCreateArmy(leaderParty, out _);

            Assert.False(canCreate);
            Assert.AreEqual(0f, influenceSeen);
        }

        [Test]
        public void ARichInvaderClansInfluenceIsLeftAlone()
        {
            MobileParty leaderParty = LedBy(InvaderClanId, 500f);
            float influenceSeen = -1f;
            InvasionArmyManagementModel model = Model(VanillaInfluenceRule(seen => influenceSeen = seen));

            model.CanLordCreateArmy(leaderParty, out _);

            Assert.AreEqual(500f, influenceSeen);
        }

        [Test]
        public void CallingALordToAnInvadersArmyCostsNoInfluence()
        {
            var decorated = new Mock<ArmyManagementCalculationModel>();
            decorated.Setup(model => model.CalculatePartyInfluenceCost(It.IsAny<MobileParty>(), It.IsAny<MobileParty>()))
                .Returns(40);

            Assert.AreEqual(0, Model(decorated).CalculatePartyInfluenceCost(LedBy(InvaderClanId, 0f), Party()));
        }

        [Test]
        public void CallingALordToACalradianArmyCostsWhatVanillaAsks()
        {
            var decorated = new Mock<ArmyManagementCalculationModel>();
            decorated.Setup(model => model.CalculatePartyInfluenceCost(It.IsAny<MobileParty>(), It.IsAny<MobileParty>()))
                .Returns(40);

            Assert.AreEqual(40, Model(decorated).CalculatePartyInfluenceCost(LedBy(CalradianClanId, 0f), Party()));
        }

        [Test]
        public void ALeaderlessArmyCostsWhatVanillaAsks()
        {
            var decorated = new Mock<ArmyManagementCalculationModel>();
            decorated.Setup(model => model.CalculatePartyInfluenceCost(It.IsAny<MobileParty>(), It.IsAny<MobileParty>()))
                .Returns(40);

            Assert.AreEqual(40, Model(decorated).CalculatePartyInfluenceCost(Party(), Party()));
        }

        [Test]
        public void AnInvadersArmyCostsNoInfluenceToGatherOrHoldTogether()
        {
            var decorated = new Mock<ArmyManagementCalculationModel>();
            decorated.Setup(model => model.CalculateTotalInfluenceCost(It.IsAny<Army>(), It.IsAny<float>())).Returns(40);
            decorated.Setup(model => model.GetCohesionBoostInfluenceCost(It.IsAny<Army>(), It.IsAny<int>())).Returns(30);
            Army army = ArmyLedBy(LedBy(InvaderClanId, 0f));

            Assert.AreEqual(0, Model(decorated).CalculateTotalInfluenceCost(army, 1f));
            Assert.AreEqual(0, Model(decorated).GetCohesionBoostInfluenceCost(army, 100));
        }

        [Test]
        public void ACalradianArmyCostsWhatVanillaAsksToGatherOrHoldTogether()
        {
            var decorated = new Mock<ArmyManagementCalculationModel>();
            decorated.Setup(model => model.CalculateTotalInfluenceCost(It.IsAny<Army>(), It.IsAny<float>())).Returns(40);
            decorated.Setup(model => model.GetCohesionBoostInfluenceCost(It.IsAny<Army>(), It.IsAny<int>())).Returns(30);
            Army army = ArmyLedBy(LedBy(CalradianClanId, 0f));

            Assert.AreEqual(40, Model(decorated).CalculateTotalInfluenceCost(army, 1f));
            Assert.AreEqual(30, Model(decorated).GetCohesionBoostInfluenceCost(army, 100));
        }

        [TestCase(InvasionCapitalObjectiveKind.Defence, InvasionCapitalObjectivePhase.Gathering, true, true)]
        [TestCase(InvasionCapitalObjectiveKind.Defence, InvasionCapitalObjectivePhase.Gathering, false, false)]
        [TestCase(InvasionCapitalObjectiveKind.Defence, InvasionCapitalObjectivePhase.Mobilizing, true, false)]
        [TestCase(InvasionCapitalObjectiveKind.Defence, InvasionCapitalObjectivePhase.Advancing, true, false)]
        [TestCase(InvasionCapitalObjectiveKind.Defence, InvasionCapitalObjectivePhase.Besieging, true, false)]
        [TestCase(InvasionCapitalObjectiveKind.Defence, InvasionCapitalObjectivePhase.Dormant, true, false)]
        [TestCase(InvasionCapitalObjectiveKind.Attack, InvasionCapitalObjectivePhase.Gathering, true, false)]
        public void OnlyTheGatheringReliefArmyIsExemptFromCohesion(
            InvasionCapitalObjectiveKind kind, InvasionCapitalObjectivePhase phase,
            bool matchesCommander, bool exempt)
        {
            MobileParty commander = LedBy(InvaderClanId, 0f);
            commander.StringId = "commander";
            Army army = ArmyLedBy(commander);
            army.Cohesion = 15f;
            var decorated = new Mock<ArmyManagementCalculationModel>();
            decorated.Setup(model => model.CalculateDailyCohesionChange(army, true))
                .Returns(new ExplainedNumber(-5f));
            decorated.Setup(model => model.CalculateNewCohesion(army, null!, 15, -1)).Returns(12);
            var directive = new InvasionCapitalObjectiveDirective(InvaderClanId,
                matchesCommander ? "commander" : "other-commander", "capital", kind, phase);
            InvasionArmyManagementModel model = Model(decorated, directive);

            Assert.That(model.CalculateDailyCohesionChange(army, true).ResultNumber,
                Is.EqualTo(exempt ? 0f : -5f));
            Assert.That(model.CalculateNewCohesion(army, null!, 15, -1),
                Is.EqualTo(exempt ? 100 : 12));
            decorated.Verify(value => value.CalculateDailyCohesionChange(army, true),
                exempt ? Times.Never() : Times.Once());
            decorated.Verify(value => value.CalculateNewCohesion(army, null!, 15, -1),
                exempt ? Times.Never() : Times.Once());
        }

        [Test]
        public void CohesionResumesWhenTheReliefObjectiveEnds()
        {
            MobileParty commander = LedBy(InvaderClanId, 0f);
            commander.StringId = "commander";
            Army army = ArmyLedBy(commander);
            var decorated = new Mock<ArmyManagementCalculationModel>();
            decorated.Setup(model => model.CalculateDailyCohesionChange(army, false))
                .Returns(new ExplainedNumber(-5f));
            InvasionCapitalObjectiveDirective? directive = new InvasionCapitalObjectiveDirective(
                InvaderClanId, "commander", "capital", InvasionCapitalObjectiveKind.Defence,
                InvasionCapitalObjectivePhase.Gathering);
            var query = new Mock<IInvasionCapitalObjectiveQuery>();
            query.Setup(value => value.FindForKingdom(InvaderClanId)).Returns(() => directive);
            var model = new InvasionArmyManagementModel(decorated.Object,
                new Mock<IInvaderClanQuery>().Object, query.Object, new ClanInfluenceLift(),
                Mock.Of<IInvasionGarrisonArmyLeaders>());

            Assert.That(model.CalculateDailyCohesionChange(army).ResultNumber, Is.Zero);

            directive = null;

            Assert.That(model.CalculateDailyCohesionChange(army).ResultNumber, Is.EqualTo(-5f));
        }

        [Test]
        public void ArmiesWithoutACapitalObjectiveKeepNativeCohesion()
        {
            Army army = ArmyLedBy(LedBy(CalradianClanId, 0f));
            var decorated = new Mock<ArmyManagementCalculationModel>();
            decorated.Setup(model => model.CalculateDailyCohesionChange(army, false))
                .Returns(new ExplainedNumber(-5f));
            decorated.Setup(model => model.CalculateNewCohesion(army, null!, 15, -1)).Returns(12);
            InvasionArmyManagementModel model = Model(decorated);

            Assert.That(model.CalculateDailyCohesionChange(army).ResultNumber, Is.EqualTo(-5f));
            Assert.That(model.CalculateNewCohesion(army, null!, 15, -1), Is.EqualTo(12));
        }

        private static InvasionArmyManagementModel Model(Mock<ArmyManagementCalculationModel> decorated,
            InvasionCapitalObjectiveDirective? directive = null)
        {
            var invaderClanQuery = new Mock<IInvaderClanQuery>();
            invaderClanQuery.Setup(query => query.IsInvaderClan(InvaderClanId)).Returns(true);
            var capitalObjectiveQuery = new Mock<IInvasionCapitalObjectiveQuery>();
            capitalObjectiveQuery.Setup(query => query.FindForKingdom(InvaderClanId)).Returns(directive);
            return new InvasionArmyManagementModel(decorated.Object, invaderClanQuery.Object,
                capitalObjectiveQuery.Object, new ClanInfluenceLift(), Mock.Of<IInvasionGarrisonArmyLeaders>());
        }

        // Vanilla's own refusal: no army for a clan holding 100 influence or less.
        private static Mock<ArmyManagementCalculationModel> VanillaInfluenceRule(Action<float> influenceSeen)
        {
            var decorated = new Mock<ArmyManagementCalculationModel>();
            MBList<MobileParty> members;
            decorated.Setup(model => model.CanLordCreateArmy(It.IsAny<MobileParty>(), out members))
                .Returns(new CanLordCreateArmyCall((MobileParty leaderParty, out MBList<MobileParty> called) =>
                {
                    float influence = leaderParty.LeaderHero.Clan.Influence;
                    influenceSeen(influence);
                    called = new MBList<MobileParty> { Party() };
                    return influence > 100f;
                }));
            return decorated;
        }

        private static MobileParty LedBy(string clanId, float influence)
        {
            var clan = (Clan)FormatterServices.GetUninitializedObject(typeof(Clan));
            clan.StringId = clanId;
            InfluenceField.SetValue(clan, influence);

            var leader = (Hero)FormatterServices.GetUninitializedObject(typeof(Hero));
            typeof(Hero).GetField("_clan", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(leader, clan);

            var component = new Mock<PartyComponent> { CallBase = true };
            component.Setup(partyComponent => partyComponent.Leader).Returns(leader);

            MobileParty party = Party();
            party.ActualClan = clan;
            typeof(MobileParty).GetField("_partyComponent", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(party, component.Object);
            return party;
        }

        private static Army ArmyLedBy(MobileParty leaderParty)
        {
            var army = (Army)FormatterServices.GetUninitializedObject(typeof(Army));
            typeof(Army).GetProperty(nameof(Army.LeaderParty))!.SetValue(army, leaderParty);
            return army;
        }

        private static MobileParty Party()
        {
            return (MobileParty)FormatterServices.GetUninitializedObject(typeof(MobileParty));
        }
    }
}
