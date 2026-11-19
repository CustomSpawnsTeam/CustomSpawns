using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using CustomSpawns.Spawn;
using NUnit.Framework;
using TaleWorlds.CampaignSystem.Party;

namespace CustomSpawns.Tests.Spawn
{
    public class DynamicSpawnDataAccompanyingIndexTests
    {
        [Test]
        public void EmptyRegistryYieldsAnEmptyIndex()
        {
            Assert.That(DynamicSpawnData.BuildAccompanyingIndex(
                new Dictionary<MobileParty, CsPartyData>()), Is.Empty);
        }

        [Test]
        public void SupportingPartiesAreGroupedUnderThePartyTheySupport()
        {
            MobileParty mainParty = DummyParty();
            MobileParty supportingParty = DummyParty();
            var registry = new Dictionary<MobileParty, CsPartyData>
            {
                [mainParty] = PartyData(null),
                [supportingParty] = PartyData(mainParty)
            };

            ILookup<MobileParty, MobileParty> index =
                DynamicSpawnData.BuildAccompanyingIndex(registry);

            Assert.That(index.Select(group => group.Key), Is.EqualTo(new[] { mainParty }));
            Assert.That(index[mainParty], Is.EqualTo(new[] { supportingParty }));
        }

        [Test]
        public void EntriesWithoutASupportedPartyAreIgnored()
        {
            var registry = new Dictionary<MobileParty, CsPartyData>
            {
                [DummyParty()] = PartyData(null),
                [DummyParty()] = PartyData(null)
            };

            Assert.That(DynamicSpawnData.BuildAccompanyingIndex(registry), Is.Empty);
        }

        [Test]
        public void SeveralSupportingPartiesShareTheirMainPartyEntry()
        {
            MobileParty mainParty = DummyParty();
            MobileParty firstSupportingParty = DummyParty();
            MobileParty secondSupportingParty = DummyParty();
            var registry = new Dictionary<MobileParty, CsPartyData>
            {
                [mainParty] = PartyData(null),
                [firstSupportingParty] = PartyData(mainParty),
                [secondSupportingParty] = PartyData(mainParty)
            };

            ILookup<MobileParty, MobileParty> index =
                DynamicSpawnData.BuildAccompanyingIndex(registry);

            Assert.That(index.Count, Is.EqualTo(1));
            Assert.That(index[mainParty],
                Is.EquivalentTo(new[] { firstSupportingParty, secondSupportingParty }));
        }

        [Test]
        public void EachMainPartyKeepsItsOwnSupportingParties()
        {
            MobileParty firstMainParty = DummyParty();
            MobileParty secondMainParty = DummyParty();
            MobileParty firstSupportingParty = DummyParty();
            MobileParty secondSupportingParty = DummyParty();
            var registry = new Dictionary<MobileParty, CsPartyData>
            {
                [firstSupportingParty] = PartyData(firstMainParty),
                [secondSupportingParty] = PartyData(secondMainParty)
            };

            ILookup<MobileParty, MobileParty> index =
                DynamicSpawnData.BuildAccompanyingIndex(registry);

            Assert.That(index[firstMainParty], Is.EqualTo(new[] { firstSupportingParty }));
            Assert.That(index[secondMainParty], Is.EqualTo(new[] { secondSupportingParty }));
        }

        private static CsPartyData PartyData(MobileParty? supportedParty)
        {
            return new CsPartyData("party_template", "spawn_template", supportedParty is null, null,
                supportedParty);
        }

        private static MobileParty DummyParty()
        {
            return (MobileParty)FormatterServices.GetUninitializedObject(typeof(MobileParty));
        }
    }
}
