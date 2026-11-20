using System.Collections.Generic;
using CustomSpawns.Data.Reader;
using CustomSpawns.Diplomacy;
using Moq;
using NUnit.Framework;
using TaleWorlds.CampaignSystem;

namespace CustomSpawns.Tests.Diplomacy
{
    public class CustomSpawnsDiplomacyProviderTests
    {
        private const string INVADER_CLAN_ID = "cs_humar";
        private const string INVADER_KINGDOM_ID = "new_kingdom";
        private const string VANILLA_KINGDOM_ID = "vlandia";

        private readonly IFaction _invaderClan = Faction(INVADER_CLAN_ID, isClan: true);
        private readonly IFaction _invaderKingdom = Faction(INVADER_KINGDOM_ID, isClan: false);
        private readonly IFaction _vanillaClan = Faction("clan_vlandia_1", isClan: true);
        private readonly IFaction _vanillaKingdom = Faction(VANILLA_KINGDOM_ID, isClan: false);
        private readonly IFaction _otherVanillaClan = Faction("clan_battania_1", isClan: true);
        private readonly IFaction _otherVanillaKingdom = Faction("battania", isClan: false);

        private static IFaction Faction(string id, bool isClan)
        {
            var faction = new Mock<IFaction>();
            faction.Setup(f => f.IsEliminated).Returns(false);
            faction.Setup(f => f.IsClan).Returns(isClan);
            faction.Setup(f => f.IsKingdomFaction).Returns(!isClan);
            faction.Setup(f => f.StringId).Returns(id);
            return faction.Object;
        }

        private CustomSpawnsDiplomacyProvider InitProvider(Data.Model.Diplomacy.ForcedWarPeaceData invaderPolicy)
        {
            var data = new Dictionary<string, Data.Model.Diplomacy>
            {
                { INVADER_CLAN_ID, new Data.Model.Diplomacy { ForcedWarPeaceDataInstance = invaderPolicy } }
            };
            var dataReader = new Mock<IDataReader<Dictionary<string, Data.Model.Diplomacy>>>();
            dataReader.Setup(reader => reader.Data).Returns(data);

            // Mirrors SuzerainProvider: every faction whose map faction is a kingdom is a vassal of it, and a
            // kingdom's map faction is itself.
            var suzerain = new Mock<ISuzerainProvider>();
            foreach (var (member, kingdom) in new[]
                     {
                         (_invaderClan, _invaderKingdom), (_invaderKingdom, _invaderKingdom),
                         (_vanillaClan, _vanillaKingdom), (_vanillaKingdom, _vanillaKingdom),
                         (_otherVanillaClan, _otherVanillaKingdom), (_otherVanillaKingdom, _otherVanillaKingdom)
                     })
            {
                suzerain.Setup(s => s.IsVassal(member)).Returns(true);
                suzerain.Setup(s => s.GetSuzerain(member)).Returns(kingdom);
            }
            suzerain.Setup(s => s.GetRulingClan(_invaderKingdom)).Returns(_invaderClan);
            suzerain.Setup(s => s.GetRulingClan(_vanillaKingdom)).Returns(_vanillaClan);
            suzerain.Setup(s => s.GetRulingClan(_otherVanillaKingdom)).Returns(_otherVanillaClan);

            return new CustomSpawnsDiplomacyProvider(
                new CustomSpawnsClanDiplomacyProvider(suzerain.Object, dataReader.Object), suzerain.Object);
        }

        [Test]
        public void ShouldBeAtConstantWar_WhenVanillaKingdomFacesKingdomFoundedByCustomSpawnsClan_ReturnsTrue()
        {
            var provider = InitProvider(new Data.Model.Diplomacy.ForcedWarPeaceData());

            Assert.That(provider.ShouldBeAtConstantWar(_vanillaKingdom, _invaderKingdom), Is.True);
            Assert.That(provider.ShouldBeAtConstantWar(_invaderKingdom, _vanillaKingdom), Is.True);
            Assert.That(provider.ShouldBeAtPeace(_vanillaKingdom, _invaderKingdom), Is.False);
        }

        [Test]
        public void ShouldBeAtPeace_WhenKingdomFoundedByCustomSpawnsClanHasVanillaKingdomAsException_ReturnsTrue()
        {
            var policy = new Data.Model.Diplomacy.ForcedWarPeaceData();
            policy.ExceptionKingdoms.Add(VANILLA_KINGDOM_ID);
            var provider = InitProvider(policy);

            Assert.That(provider.ShouldBeAtConstantWar(_vanillaKingdom, _invaderKingdom), Is.False);
            Assert.That(provider.ShouldBeAtPeace(_vanillaKingdom, _invaderKingdom), Is.True);
        }

        [Test]
        public void ShouldBeAtConstantWar_WhenNeitherKingdomIsFoundedByCustomSpawnsClan_ReturnsFalse()
        {
            var provider = InitProvider(new Data.Model.Diplomacy.ForcedWarPeaceData());

            Assert.That(provider.ShouldBeAtConstantWar(_vanillaKingdom, _otherVanillaKingdom), Is.False);
            Assert.That(provider.ShouldBeAtPeace(_vanillaKingdom, _otherVanillaKingdom), Is.False);
        }
    }
}
