using System;
using System.Reflection;
using System.Runtime.Serialization;
using CustomSpawns.Invasions.Adapters;
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

        private static InvasionArmyManagementModel Model(Mock<ArmyManagementCalculationModel> decorated)
        {
            var invaderClanQuery = new Mock<IInvaderClanQuery>();
            invaderClanQuery.Setup(query => query.IsInvaderClan(InvaderClanId)).Returns(true);
            return new InvasionArmyManagementModel(decorated.Object, invaderClanQuery.Object, new ClanInfluenceLift());
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
