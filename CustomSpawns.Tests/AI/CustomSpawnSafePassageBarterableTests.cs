using System.Collections.Generic;
using CustomSpawns.AI.Barterables;
using NUnit.Framework;

namespace CustomSpawns.Tests.AI
{
    public class CustomSpawnSafePassageBarterableTests
    {
        [Test]
        public void IsCustomSpawnsParty_UsesTheIsolatedPartyTemplateId()
        {
            bool isCustomSpawnsParty = CustomSpawnSafePassageBarterable.IsCustomSpawnsParty("custom_party_42",
                new HashSet<string> { "custom_party" }, new HashSet<string>());

            Assert.True(isCustomSpawnsParty);
        }

        [Test]
        public void IsCustomSpawnsParty_RecognizesSupportingPartyTemplates()
        {
            bool isCustomSpawnsParty = CustomSpawnSafePassageBarterable.IsCustomSpawnsParty("supporting_party_42",
                new HashSet<string>(), new HashSet<string> { "supporting_party" });

            Assert.True(isCustomSpawnsParty);
        }

        [Test]
        public void ApplyCustomSpawnsPriceAdjustment_OnlyDiscountsCustomSpawnsParties()
        {
            Assert.AreEqual(100, CustomSpawnSafePassageBarterable.ApplyCustomSpawnsPriceAdjustment(800, true));
            Assert.AreEqual(800, CustomSpawnSafePassageBarterable.ApplyCustomSpawnsPriceAdjustment(800, false));
        }
    }
}
