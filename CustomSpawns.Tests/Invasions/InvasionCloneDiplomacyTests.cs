using System.Collections.Generic;
using CustomSpawns.Invasions;
using CustomSpawns.Invasions.Adapters;
using NUnit.Framework;
using DiplomacyData = CustomSpawns.Data.Model.Diplomacy;

namespace CustomSpawns.Tests.Invasions
{
    public class InvasionCloneDiplomacyTests
    {
        private Dictionary<string, DiplomacyData> _diplomacy = new();
        private InvasionCloneDiplomacy _invasionCloneDiplomacy = null!;

        [SetUp]
        public void SetUp()
        {
            _diplomacy = new Dictionary<string, DiplomacyData>
            {
                {
                    "cs_humar", new DiplomacyData
                    {
                        clanString = "cs_humar", ForceNoKingdom = true,
                        ForcedWarPeaceDataInstance = new DiplomacyData.ForcedWarPeaceData
                        {
                            AtPeaceWithClans = new List<string> { "cs_friend" },
                            ExceptionKingdoms = new List<string> { "empire" }
                        }
                    }
                }
            };
            _invasionCloneDiplomacy = new InvasionCloneDiplomacy(_diplomacy);
        }

        [Test]
        public void ACopyInheritsItsModelsDiplomacyUnderItsOwnId()
        {
            _invasionCloneDiplomacy.Inherit("cs_humar_line_1", "cs_humar");

            DiplomacyData copy = _diplomacy["cs_humar_line_1"];
            Assert.That(copy.clanString, Is.EqualTo("cs_humar_line_1"));
            Assert.That(copy.ForceNoKingdom, Is.True);
            Assert.That(copy.ForcedWarPeaceDataInstance!.AtPeaceWithClans, Is.EqualTo(new[] { "cs_friend" }));
            Assert.That(copy.ForcedWarPeaceDataInstance.ExceptionKingdoms, Is.EqualTo(new[] { "empire" }));
        }

        [Test]
        public void ACopyDoesNotShareItsModelsLists()
        {
            _invasionCloneDiplomacy.Inherit("cs_humar_line_1", "cs_humar");

            _diplomacy["cs_humar_line_1"].ForcedWarPeaceDataInstance!.AtPeaceWithClans.Add("cs_other");

            Assert.That(_diplomacy["cs_humar"].ForcedWarPeaceDataInstance!.AtPeaceWithClans,
                Is.EqualTo(new[] { "cs_friend" }));
        }

        [Test]
        public void AModelWithoutDiplomacyGivesNone()
        {
            _invasionCloneDiplomacy.Inherit("cs_other_line_1", "cs_other");

            Assert.That(_diplomacy.ContainsKey("cs_other_line_1"), Is.False);
        }

        [Test]
        public void ACopyWithDiplomacyOfItsOwnKeepsIt()
        {
            DiplomacyData own = new() { clanString = "cs_humar_line_1" };
            _diplomacy["cs_humar_line_1"] = own;

            _invasionCloneDiplomacy.Inherit("cs_humar_line_1", "cs_humar");

            Assert.That(_diplomacy["cs_humar_line_1"], Is.SameAs(own));
        }

        [Test]
        public void EverySavedCopyInheritsAgainOnLoad()
        {
            InvasionLineClans invasionLineClans = new();
            invasionLineClans.Register("cs_humar_line_1", "cs_humar", "cs_humar");
            invasionLineClans.Register("cs_humar_line_2", "cs_humar", "cs_humar");

            _invasionCloneDiplomacy.InheritAll(invasionLineClans);

            Assert.That(_diplomacy.ContainsKey("cs_humar_line_1"), Is.True);
            Assert.That(_diplomacy.ContainsKey("cs_humar_line_2"), Is.True);
        }
    }
}
