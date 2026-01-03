using System.Collections.Generic;
using System.Linq;
using CustomSpawns.Data.Reader;
using CustomSpawns.Diplomacy;
using Moq;
using NUnit.Framework;
using TaleWorlds.CampaignSystem;

namespace CustomSpawns.Tests.Diplomacy
{
    public class CustomSpawnsDiplomacyModelTests
    {
        private readonly string ATTACKER_CLAN_ID = "attackerClanId";
        private readonly string WAR_TARGET_CLAN_ID = "warTargetClanId";
        private readonly string PLAYER_FACTION_ID = "player_faction";

        private Dictionary<string, Data.Model.Diplomacy> InitAttackerAsCustomSpawnsClan()
        {
            var customSpawnData = new Dictionary<string, Data.Model.Diplomacy>();
            var attackerDiplomacyData = new Data.Model.Diplomacy();
            attackerDiplomacyData.ForcedWarPeaceDataInstance = new Data.Model.Diplomacy.ForcedWarPeaceData();
            var warTargetDiplomacyData = new Data.Model.Diplomacy();
            warTargetDiplomacyData.ForcedWarPeaceDataInstance = new Data.Model.Diplomacy.ForcedWarPeaceData();
            customSpawnData.Add(ATTACKER_CLAN_ID, attackerDiplomacyData);
            return customSpawnData;
        }
        
        private Dictionary<string, Data.Model.Diplomacy> InitAttackerAndWarTargetAsCustomSpawnsClans()
        {
            var customSpawnData = new Dictionary<string, Data.Model.Diplomacy>();
            var attackerDiplomacyData = new Data.Model.Diplomacy();
            attackerDiplomacyData.ForcedWarPeaceDataInstance = new Data.Model.Diplomacy.ForcedWarPeaceData();
            var warTargetDiplomacyData = new Data.Model.Diplomacy();
            warTargetDiplomacyData.ForcedWarPeaceDataInstance = new Data.Model.Diplomacy.ForcedWarPeaceData();
            customSpawnData.Add(ATTACKER_CLAN_ID, attackerDiplomacyData);
            customSpawnData.Add(WAR_TARGET_CLAN_ID, warTargetDiplomacyData);
            return customSpawnData;
        }
        
        private IFaction ValidClan(string id)
        {
            var attacker = new Mock<IFaction>();
            attacker.Setup(faction => faction.IsEliminated).Returns(false);
            attacker.Setup(faction => faction.IsClan).Returns(true);
            attacker.Setup(faction => faction.IsKingdomFaction).Returns(false);
            attacker.Setup(faction => faction.StringId).Returns(id);
            return attacker.Object;
        }
        
        private IFaction EliminatedClan(string id)
        {
            var attacker = new Mock<IFaction>();
            attacker.Setup(faction => faction.IsEliminated).Returns(true);
            attacker.Setup(faction => faction.IsClan).Returns(true);
            attacker.Setup(faction => faction.IsKingdomFaction).Returns(false);
            attacker.Setup(faction => faction.StringId).Returns(id);
            return attacker.Object;
        }
        
        private IFaction ValidKingdom(string id)
        {
            var attacker = new Mock<IFaction>();
            attacker.Setup(faction => faction.IsEliminated).Returns(false);
            attacker.Setup(faction => faction.IsClan).Returns(false);
            attacker.Setup(faction => faction.IsKingdomFaction).Returns(true);
            attacker.Setup(faction => faction.StringId).Returns(id);
            return attacker.Object;
        }
        
        private CustomSpawnsClanDiplomacyProvider InitModel(Dictionary<string, Data.Model.Diplomacy> data) 
        {
            var clanKingdomTrackable = new Mock<ISuzerainProvider>();
            // var diplomacyModel = new Mock<IFactionDiplomacyProvider>();
            var dataManager = new Mock<IDataReader<Dictionary<string,Data.Model.Diplomacy>>>();
            dataManager.Setup(manager => manager.Data).Returns(data);
            // return new CustomSpawnsClanDiplomacyProvider(clanKingdomTrackable.Object, diplomacyModel.Object, dataManager.Object);
            return new CustomSpawnsClanDiplomacyProvider(clanKingdomTrackable.Object, dataManager.Object);
        }
        
        private CustomSpawnsClanDiplomacyProvider InitModel(IFactionDiplomacyProvider model, Dictionary<string, Data.Model.Diplomacy> data)
        {
            var clanKingdomTrackable = new Mock<ISuzerainProvider>();
            var dataManager = new Mock<IDataReader<Dictionary<string,Data.Model.Diplomacy>>>();
            dataManager.Setup(manager => manager.Data).Returns(data);
            // return new CustomSpawnsClanDiplomacyProvider(clanKingdomTrackable.Object, model, dataManager.Object);
            return new CustomSpawnsClanDiplomacyProvider(clanKingdomTrackable.Object, dataManager.Object);
        }
        
        private CustomSpawnsClanDiplomacyProvider InitModel(ISuzerainProvider suzerainProvider, IFactionDiplomacyProvider factionDiplomacyAction, Dictionary<string, Data.Model.Diplomacy> data)
        {
            var dataManager = new Mock<IDataReader<Dictionary<string,Data.Model.Diplomacy>>>();
            dataManager.Setup(manager => manager.Data).Returns(data);
            // return new CustomSpawnsClanDiplomacyProvider(suzerainProvider, factionDiplomacyAction, dataManager.Object);
            return new CustomSpawnsClanDiplomacyProvider(suzerainProvider, dataManager.Object);
        }
        
        private CustomSpawnsClanDiplomacyProvider InitModelWithInitialFactionDiplomacy(Dictionary<string, Data.Model.Diplomacy> data, bool atWar)
        {
            var diplomacyActionModel = new Mock<IFactionDiplomacyProvider>();
            diplomacyActionModel.Setup(diplomacyModel => diplomacyModel.IsAtWar(It.IsAny<IFaction>(), It.IsAny<IFaction>())).Returns(atWar);
            return InitModel(diplomacyActionModel.Object, data);
        }

        private CustomSpawnsClanDiplomacyProvider InitModelWithTogglableWarAndClansNotPartOfAKingdom(Dictionary<string, Data.Model.Diplomacy> data, bool atWar)
        {
            var diplomacyActionModel = new Mock<IFactionDiplomacyProvider>();
            diplomacyActionModel.Setup(diplomacyModel => diplomacyModel.IsAtWar(It.IsAny<IFaction>(), It.IsAny<IFaction>())).Returns(atWar);
            var clanKingdomTrackable = new Mock<ISuzerainProvider>();
            clanKingdomTrackable.Setup(model => model.IsVassal(It.IsAny<IFaction>())).Returns(false);
            return InitModel(clanKingdomTrackable.Object, diplomacyActionModel.Object, data);
        }
        
        private CustomSpawnsClanDiplomacyProvider InitModelWithTogglableWarAndAttackerNotInAKingdomAndWarTargetInAKingdom(Dictionary<string, Data.Model.Diplomacy> data, bool atWar)
        {
            var diplomacyActionModel = new Mock<IFactionDiplomacyProvider>();
            diplomacyActionModel.Setup(diplomacyModel => diplomacyModel.IsAtWar(It.IsAny<IFaction>(), It.IsAny<IFaction>())).Returns(atWar);
            var clanKingdomTrackable = new Mock<ISuzerainProvider>();
            clanKingdomTrackable.Setup(model => model.IsVassal(It.Is<IFaction>(faction => faction.StringId.Equals(ATTACKER_CLAN_ID)))).Returns(false);
            clanKingdomTrackable.Setup(model => model.IsVassal(It.Is<IFaction>(faction => faction.StringId.Equals(WAR_TARGET_CLAN_ID)))).Returns(true);
            clanKingdomTrackable.Setup(model => model.GetSuzerain(It.Is<IFaction>(faction => faction.StringId.Equals(WAR_TARGET_CLAN_ID)))).Returns(ValidKingdom("vlandia"));
            return InitModel(clanKingdomTrackable.Object, diplomacyActionModel.Object, data);
        }

        private CustomSpawnsClanDiplomacyProvider InitModelWithTogglableWarAndAttackerAndWarTargetInADifferentKingdom(Dictionary<string, Data.Model.Diplomacy> data, bool atWar)
        {
            var diplomacyActionModel = new Mock<IFactionDiplomacyProvider>();
            diplomacyActionModel.Setup(diplomacyModel => diplomacyModel.IsAtWar(It.IsAny<IFaction>(), It.IsAny<IFaction>())).Returns(atWar);
            var clanKingdomTrackable = new Mock<ISuzerainProvider>();
            clanKingdomTrackable.Setup(model => model.IsVassal(It.Is<IFaction>(faction => faction.StringId.Equals(ATTACKER_CLAN_ID)))).Returns(true);
            clanKingdomTrackable.Setup(model => model.IsVassal(It.Is<IFaction>(faction => faction.StringId.Equals(WAR_TARGET_CLAN_ID)))).Returns(true);
            clanKingdomTrackable.Setup(model => model.GetSuzerain(It.Is<IFaction>(faction => faction.StringId.Equals(ATTACKER_CLAN_ID)))).Returns(ValidKingdom("aserai"));
            clanKingdomTrackable.Setup(model => model.GetSuzerain(It.Is<IFaction>(faction => faction.StringId.Equals(WAR_TARGET_CLAN_ID)))).Returns(ValidKingdom("vlandia"));
            return InitModel(clanKingdomTrackable.Object, diplomacyActionModel.Object, data);
        }
        
        private CustomSpawnsClanDiplomacyProvider InitModelWithTogglableWarAndAttackerAndWarTargetInSameKingdom(Dictionary<string, Data.Model.Diplomacy> data, bool atWar)
        {
            var diplomacyActionModel = new Mock<IFactionDiplomacyProvider>();
            diplomacyActionModel.Setup(diplomacyModel => diplomacyModel.IsAtWar(It.IsAny<IFaction>(), It.IsAny<IFaction>())).Returns(atWar);
            var clanKingdomTrackable = new Mock<ISuzerainProvider>();
            clanKingdomTrackable.Setup(model => model.IsVassal(It.Is<IFaction>(faction => faction.StringId.Equals(ATTACKER_CLAN_ID)))).Returns(true);
            clanKingdomTrackable.Setup(model => model.IsVassal(It.Is<IFaction>(faction => faction.StringId.Equals(WAR_TARGET_CLAN_ID)))).Returns(true);
            clanKingdomTrackable.Setup(model => model.GetSuzerain(It.Is<IFaction>(faction => faction.StringId.Equals(ATTACKER_CLAN_ID)))).Returns(ValidKingdom("vlandia"));
            clanKingdomTrackable.Setup(model => model.GetSuzerain(It.Is<IFaction>(faction => faction.StringId.Equals(WAR_TARGET_CLAN_ID)))).Returns(ValidKingdom("vlandia"));
            return InitModel(clanKingdomTrackable.Object, diplomacyActionModel.Object, data);
        }
        
        private CustomSpawnsClanDiplomacyProvider InitModelWithTogglableWarAndAttackerInAKingdomAndWarTargetNotInAKingdom(Dictionary<string, Data.Model.Diplomacy> data, bool atWar)
        {
            var diplomacyActionModel = new Mock<IFactionDiplomacyProvider>();
            diplomacyActionModel.Setup(diplomacyModel => diplomacyModel.IsAtWar(It.IsAny<IFaction>(), It.IsAny<IFaction>())).Returns(atWar);
            var clanKingdomTrackable = new Mock<ISuzerainProvider>();
            clanKingdomTrackable.Setup(model => model.IsVassal(It.Is<IFaction>(faction => faction.StringId.Equals(ATTACKER_CLAN_ID)))).Returns(true);
            clanKingdomTrackable.Setup(model => model.IsVassal(It.Is<IFaction>(faction => faction.StringId.Equals(WAR_TARGET_CLAN_ID)))).Returns(false);
            clanKingdomTrackable.Setup(model => model.GetSuzerain(It.Is<IFaction>(faction => faction.StringId.Equals(ATTACKER_CLAN_ID)))).Returns(ValidKingdom("vlandia"));
            return InitModel(clanKingdomTrackable.Object, diplomacyActionModel.Object, data);
        }

        [Test]
        public void IsWarDeclarationPossible_WhenAttackerIsNull_ReturnsFalse()
        {
            bool isWarDeclarationPossible = InitModel(new Dictionary<string, Data.Model.Diplomacy>()).IsWarDeclarationPossible(null, ValidClan(WAR_TARGET_CLAN_ID));

            Assert.False(isWarDeclarationPossible);
        }
        
        [Test]
        public void IsWarDeclarationPossible_WhenWarTargetAttackerIsNull_ReturnsFalse()
        {
            bool isWarDeclarationPossible = InitModel(new Dictionary<string, Data.Model.Diplomacy>()).IsWarDeclarationPossible(ValidClan(ATTACKER_CLAN_ID), null);

            Assert.False(isWarDeclarationPossible);
        }
        
        [Test]
        public void IsWarDeclarationPossible_WhenAttackerFactionIsEliminated_ReturnsFalse()
        {
            bool isWarDeclarationPossible = InitModel(new Dictionary<string, Data.Model.Diplomacy>()).IsWarDeclarationPossible(EliminatedClan(ATTACKER_CLAN_ID), ValidClan(WAR_TARGET_CLAN_ID));

            Assert.False(isWarDeclarationPossible);
        }

        [Test]
        public void IsWarDeclarationPossible_WhenWarTargetIsEliminated_ReturnsFalse()
        {
            bool isWarDeclarationPossible = InitModel(new Dictionary<string, Data.Model.Diplomacy>()).IsWarDeclarationPossible(ValidClan(ATTACKER_CLAN_ID), EliminatedClan(WAR_TARGET_CLAN_ID));

            Assert.False(isWarDeclarationPossible);
        }

        [Test]
        public void IsWarDeclarationPossible_WhenBothFactionsAreTheSame_ReturnsFalse()
        {
            var attacker = ValidClan(ATTACKER_CLAN_ID);

            bool isWarDeclarationPossible = InitModel(new Dictionary<string, Data.Model.Diplomacy>()).IsWarDeclarationPossible(attacker, attacker);

            Assert.False(isWarDeclarationPossible);
        }

        [Test]
        public void IsWarDeclarationPossible_WhenAttackerAndWarTargetAreAtWar_ReturnsFalse()
        {
            var customSpawnData = new Dictionary<string, Data.Model.Diplomacy>();
            var model = InitModelWithInitialFactionDiplomacy(customSpawnData, true);
            
            bool isWarDeclarationPossible = model.IsWarDeclarationPossible(ValidClan(ATTACKER_CLAN_ID), ValidClan(WAR_TARGET_CLAN_ID));

            Assert.False(isWarDeclarationPossible);
        }

        [Test]
        public void IsWarDeclarationPossible_WhenAttackerAndWarTargetAreNotCustomSpawnFactions_ReturnsFalse()
        {
            var customSpawnData = new Dictionary<string, Data.Model.Diplomacy>();
            var model = InitModelWithInitialFactionDiplomacy(customSpawnData, false);
            
            bool isWarDeclarationPossible = model.IsWarDeclarationPossible(ValidClan(ATTACKER_CLAN_ID), ValidClan(WAR_TARGET_CLAN_ID));

            Assert.False(isWarDeclarationPossible);
        }

        [Test]
        public void IsWarDeclarationPossible_WhenAttackerAndWarTargetHaveNoForcedWarPeaceBehaviour_ReturnsFalse()
        {
            var customSpawnData = InitAttackerAndWarTargetAsCustomSpawnsClans();
            customSpawnData.First(pair => pair.Key.Equals(ATTACKER_CLAN_ID)).Value.ForcedWarPeaceDataInstance = null;
            customSpawnData.First(pair => pair.Key.Equals(WAR_TARGET_CLAN_ID)).Value.ForcedWarPeaceDataInstance = null;
            var model = InitModelWithInitialFactionDiplomacy(customSpawnData, false);
            
            bool isWarDeclarationPossible = model.IsWarDeclarationPossible(ValidClan(ATTACKER_CLAN_ID), ValidClan(WAR_TARGET_CLAN_ID));

            Assert.False(isWarDeclarationPossible);
        }
        
        [Test]
        public void IsWarDeclarationPossible_WhenAttackerAndWarTargetAreNotPartOfAKingdomAndWarTargetIsNotAnException_ReturnsTrue()
        {
            var customSpawnData = InitAttackerAsCustomSpawnsClan();
            customSpawnData.First(pair => pair.Key.Equals(ATTACKER_CLAN_ID)).Value.ForcedWarPeaceDataInstance.ExceptionKingdoms.Add("vlandia");
            var model = InitModelWithTogglableWarAndClansNotPartOfAKingdom(customSpawnData, false);
            
            bool isWarDeclarationPossible = model.IsWarDeclarationPossible(ValidClan(ATTACKER_CLAN_ID), ValidClan(WAR_TARGET_CLAN_ID));

            Assert.True(isWarDeclarationPossible);
        }
        
        [Test]
        public void IsWarDeclarationPossible_WhenAttackerAndWarTargetAreNotPartOfAKingdomAndWarTargetIsForcedPeace_ReturnsFalse()
        {
            var customSpawnData = InitAttackerAsCustomSpawnsClan();
            customSpawnData.First(pair => pair.Key.Equals(ATTACKER_CLAN_ID)).Value.ForcedWarPeaceDataInstance.ExceptionKingdoms.Add("vlandia");
            customSpawnData.First(pair => pair.Key.Equals(ATTACKER_CLAN_ID)).Value.ForcedWarPeaceDataInstance.AtPeaceWithClans.Add(WAR_TARGET_CLAN_ID);
            var model = InitModelWithTogglableWarAndClansNotPartOfAKingdom(customSpawnData, false);
            
            bool isWarDeclarationPossible = model.IsWarDeclarationPossible(ValidClan(ATTACKER_CLAN_ID), ValidClan(WAR_TARGET_CLAN_ID));

            Assert.False(isWarDeclarationPossible);
        }
        
        [Test]
        public void IsWarDeclarationPossible_WhenAttackerIsNotPartOfAKingdomAndWarTargetIsAKingdomNotInExceptionKingdom_ReturnsTrue()
        {
            var customSpawnData = InitAttackerAsCustomSpawnsClan();
            var model = InitModelWithTogglableWarAndClansNotPartOfAKingdom(customSpawnData, false);
            
            bool isWarDeclarationPossible = model.IsWarDeclarationPossible(ValidClan(ATTACKER_CLAN_ID), ValidKingdom(WAR_TARGET_CLAN_ID));

            Assert.True(isWarDeclarationPossible);
        }
        
        [Test]
        public void IsWarDeclarationPossible_WhenAttackerIsNotPartOfAKingdomAndWarTargetIsAKingdomInExceptionKingdom_ReturnsFalse()
        {
            var customSpawnData = InitAttackerAsCustomSpawnsClan();
            customSpawnData.First(pair => pair.Key.Equals(ATTACKER_CLAN_ID)).Value.ForcedWarPeaceDataInstance.ExceptionKingdoms.Add(WAR_TARGET_CLAN_ID);
            var model = InitModelWithTogglableWarAndClansNotPartOfAKingdom(customSpawnData, false);
            
            bool isWarDeclarationPossible = model.IsWarDeclarationPossible(ValidClan(ATTACKER_CLAN_ID), ValidKingdom(WAR_TARGET_CLAN_ID));

            Assert.False(isWarDeclarationPossible);
        }

        [Test]
        public void IsWarDeclarationPossible_WhenAttackerIsNotPartOfAKingdomAndWarTargetIsAClanInAKingdom_ReturnsTrue()
        {
            var customSpawnData = InitAttackerAsCustomSpawnsClan();
            var model = InitModelWithTogglableWarAndAttackerNotInAKingdomAndWarTargetInAKingdom(customSpawnData, false);
            
            bool isWarDeclarationPossible = model.IsWarDeclarationPossible(ValidClan(ATTACKER_CLAN_ID), ValidClan(WAR_TARGET_CLAN_ID));
        
            Assert.True(isWarDeclarationPossible);
        }
        
        [Test]
        public void IsWarDeclarationPossible_WhenAttackerIsNotPartOfAKingdomAndWarTargetIsAClanInAKingdomInExceptionKingdom_ReturnsFalse()
        {
            var customSpawnData = InitAttackerAsCustomSpawnsClan();
            customSpawnData.First(pair => pair.Key.Equals(ATTACKER_CLAN_ID)).Value.ForcedWarPeaceDataInstance.ExceptionKingdoms.Add("vlandia");
            var model = InitModelWithTogglableWarAndAttackerNotInAKingdomAndWarTargetInAKingdom(customSpawnData, false);
            
            bool isWarDeclarationPossible = model.IsWarDeclarationPossible(ValidClan(ATTACKER_CLAN_ID), ValidClan(WAR_TARGET_CLAN_ID));
        
            Assert.False(isWarDeclarationPossible);
        }
        
        [Test]
        public void IsWarDeclarationPossible_WhenAttackerIsNotPartOfAKingdomAndWarTargetIsAClanWithAtPeaceFlagInAKingdomInExceptionKingdom_ReturnsFalse()
        {
            var customSpawnData = InitAttackerAsCustomSpawnsClan();
            customSpawnData.First(pair => pair.Key.Equals(ATTACKER_CLAN_ID)).Value.ForcedWarPeaceDataInstance.ExceptionKingdoms.Add("vlandia");
            customSpawnData.First(pair => pair.Key.Equals(ATTACKER_CLAN_ID)).Value.ForcedWarPeaceDataInstance.AtPeaceWithClans.Add(WAR_TARGET_CLAN_ID);
            var model = InitModelWithTogglableWarAndAttackerNotInAKingdomAndWarTargetInAKingdom(customSpawnData, false);

            bool isWarDeclarationPossible = model.IsWarDeclarationPossible(ValidClan(ATTACKER_CLAN_ID), ValidClan(WAR_TARGET_CLAN_ID));
        
            Assert.False(isWarDeclarationPossible);
        }

        [Test]
        public void IsWarDeclarationPossible_WhenAttackerIsNotPartOfAKingdomAndWarTargetIsAClanWithAtPeaceFlagInAKingdom_ReturnsTrue()
        {
            var customSpawnData = InitAttackerAsCustomSpawnsClan();
            customSpawnData.First(pair => pair.Key.Equals(ATTACKER_CLAN_ID)).Value.ForcedWarPeaceDataInstance.AtPeaceWithClans.Add(WAR_TARGET_CLAN_ID);
            // customSpawnData.First(pair => pair.Key.Equals(WAR_TARGET_CLAN_ID)).Value.ForcedWarPeaceDataInstance.AtPeaceWithClans.Add(ATTACKER_CLAN_ID);
            var model = InitModelWithTogglableWarAndAttackerNotInAKingdomAndWarTargetInAKingdom(customSpawnData, false);

            bool isWarDeclarationPossible = model.IsWarDeclarationPossible(ValidClan(ATTACKER_CLAN_ID), ValidClan(WAR_TARGET_CLAN_ID));
        
            Assert.True(isWarDeclarationPossible);
        }
        
        [Test]
        public void IsWarDeclarationPossible_WhenAttackerAndWarTargetArePartOfAKingdom_ReturnsTrue()
        {
            var customSpawnData = InitAttackerAsCustomSpawnsClan();
            var model = InitModelWithTogglableWarAndAttackerAndWarTargetInADifferentKingdom(customSpawnData, false);

            bool isWarDeclarationPossible = model.IsWarDeclarationPossible(ValidClan(ATTACKER_CLAN_ID), ValidClan(WAR_TARGET_CLAN_ID));
        
            Assert.True(isWarDeclarationPossible);
        }
        
        [Test]
        public void IsWarDeclarationPossible_WhenAttackerAndWarTargetArePartOfAKingdomWithWarTargetKingdomInExceptionKingdom_ReturnsFalse()
        {
            var customSpawnData = InitAttackerAsCustomSpawnsClan();
            customSpawnData.First(pair => pair.Key.Equals(ATTACKER_CLAN_ID)).Value.ForcedWarPeaceDataInstance.ExceptionKingdoms.Add("vlandia");
            var model = InitModelWithTogglableWarAndAttackerAndWarTargetInADifferentKingdom(customSpawnData, false);

            bool isWarDeclarationPossible = model.IsWarDeclarationPossible(ValidClan(ATTACKER_CLAN_ID), ValidClan(WAR_TARGET_CLAN_ID));
        
            Assert.False(isWarDeclarationPossible);
        }
        
        [Test]
        public void IsWarDeclarationPossible_WhenAttackerAndWarTargetArePartOfAKingdomWithForcedPeace_ReturnsTrue()
        {
            var customSpawnData = InitAttackerAsCustomSpawnsClan();
            customSpawnData.First(pair => pair.Key.Equals(ATTACKER_CLAN_ID)).Value.ForcedWarPeaceDataInstance.AtPeaceWithClans.Add(WAR_TARGET_CLAN_ID);
            var model = InitModelWithTogglableWarAndAttackerAndWarTargetInADifferentKingdom(customSpawnData, false);

            bool isWarDeclarationPossible = model.IsWarDeclarationPossible(ValidClan(ATTACKER_CLAN_ID), ValidClan(WAR_TARGET_CLAN_ID));
        
            Assert.True(isWarDeclarationPossible);
        }
        
        [Test]
        public void IsWarDeclarationPossible_WhenAttackerAndWarTargetArePartOfAKingdomWithForcedPeaceAndWarTargetInExceptionKingdom_ReturnsFalse()
        {
            var customSpawnData = InitAttackerAsCustomSpawnsClan();
            customSpawnData.First(pair => pair.Key.Equals(ATTACKER_CLAN_ID)).Value.ForcedWarPeaceDataInstance.ExceptionKingdoms.Add("vlandia");
            customSpawnData.First(pair => pair.Key.Equals(ATTACKER_CLAN_ID)).Value.ForcedWarPeaceDataInstance.AtPeaceWithClans.Add(WAR_TARGET_CLAN_ID);
            var model = InitModelWithTogglableWarAndAttackerAndWarTargetInADifferentKingdom(customSpawnData, false);

            bool isWarDeclarationPossible = model.IsWarDeclarationPossible(ValidClan(ATTACKER_CLAN_ID), ValidClan(WAR_TARGET_CLAN_ID));
        
            Assert.False(isWarDeclarationPossible);
        }
        
        [Test]
        public void IsWarDeclarationPossible_WhenAttackerAndWarTargetArePartOfTheSameKingdom_ReturnsFalse()
        {
            var customSpawnData = InitAttackerAsCustomSpawnsClan();
            var model = InitModelWithTogglableWarAndAttackerAndWarTargetInSameKingdom(customSpawnData, false);

            bool isWarDeclarationPossible = model.IsWarDeclarationPossible(ValidClan(ATTACKER_CLAN_ID), ValidClan(WAR_TARGET_CLAN_ID));
        
            Assert.False(isWarDeclarationPossible);
        }
        
        [Test]
        public void IsWarDeclarationPossible_WhenAttackerIsPartOfADifferentKingdomAndWarTargetIsNotInAKingdomWithAtPeaceFlag_ReturnsFalse()
        {
            var customSpawnData = InitAttackerAsCustomSpawnsClan();
            customSpawnData.First(pair => pair.Key.Equals(ATTACKER_CLAN_ID)).Value.ForcedWarPeaceDataInstance.AtPeaceWithClans.Add(WAR_TARGET_CLAN_ID);
            var model = InitModelWithTogglableWarAndAttackerInAKingdomAndWarTargetNotInAKingdom(customSpawnData, false);

            bool isWarDeclarationPossible = model.IsWarDeclarationPossible(ValidClan(ATTACKER_CLAN_ID), ValidClan(WAR_TARGET_CLAN_ID));
        
            Assert.False(isWarDeclarationPossible);
        }
        
        [Test]
        public void IsWarDeclarationPossible_WhenAttackerIsPartOfADifferentKingdomAndWarTargetIsNotInAKingdom_ReturnsTrue()
        {
            var customSpawnData = InitAttackerAsCustomSpawnsClan();
            var model = InitModelWithTogglableWarAndAttackerInAKingdomAndWarTargetNotInAKingdom(customSpawnData, false);

            bool isWarDeclarationPossible = model.IsWarDeclarationPossible(ValidClan(ATTACKER_CLAN_ID), ValidClan(WAR_TARGET_CLAN_ID));
        
            Assert.True(isWarDeclarationPossible);
        }
        
        [Test]
        public void IsWarDeclarationPossible_WhenAttackerAndWarTargetAreCustomSpawnClansAndAttackerAndWarTargetWithWarData_ReturnsTrue()
        {
            var customSpawnData = InitAttackerAndWarTargetAsCustomSpawnsClans();
            var model = InitModelWithTogglableWarAndClansNotPartOfAKingdom(customSpawnData, false);
            
            bool isWarDeclarationPossible = model.IsWarDeclarationPossible(ValidClan(ATTACKER_CLAN_ID), ValidClan(WAR_TARGET_CLAN_ID));
        
            Assert.True(isWarDeclarationPossible);
        }
        
        [Test]
        public void IsWarDeclarationPossible_WhenAttackerAndWarTargetAreCustomSpawnClansWithAttackerWithPeaceData_ReturnsFalse()
        {
            var customSpawnData = InitAttackerAndWarTargetAsCustomSpawnsClans();
            customSpawnData.First(pair => pair.Key.Equals(ATTACKER_CLAN_ID)).Value.ForcedWarPeaceDataInstance.AtPeaceWithClans.Add(WAR_TARGET_CLAN_ID);
            var model = InitModelWithTogglableWarAndClansNotPartOfAKingdom(customSpawnData, false);
            
            bool isWarDeclarationPossible = model.IsWarDeclarationPossible(ValidClan(ATTACKER_CLAN_ID), ValidClan(WAR_TARGET_CLAN_ID));
        
            Assert.False(isWarDeclarationPossible);
        }
        
        [Test]
        public void IsWarDeclarationPossible_WhenAttackerAndWarTargetAreCustomSpawnClansWithWarTargetWithPeaceData_ReturnsFalse()
        {
            var customSpawnData = InitAttackerAndWarTargetAsCustomSpawnsClans();
            customSpawnData.First(pair => pair.Key.Equals(WAR_TARGET_CLAN_ID)).Value.ForcedWarPeaceDataInstance.AtPeaceWithClans.Add(ATTACKER_CLAN_ID);
            var model = InitModelWithTogglableWarAndClansNotPartOfAKingdom(customSpawnData, false);
            
            bool isWarDeclarationPossible = model.IsWarDeclarationPossible(ValidClan(ATTACKER_CLAN_ID), ValidClan(WAR_TARGET_CLAN_ID));
        
            Assert.False(isWarDeclarationPossible);
        }
        
        [Test]
        public void IsWarDeclarationPossible_WhenAttackerAndWarTargetAreCustomSpawnClansWithAttackerAndWarTargetWithPeaceData_ReturnsFalse()
        {
            var customSpawnData = InitAttackerAndWarTargetAsCustomSpawnsClans();
            customSpawnData.First(pair => pair.Key.Equals(ATTACKER_CLAN_ID)).Value.ForcedWarPeaceDataInstance.AtPeaceWithClans.Add(WAR_TARGET_CLAN_ID);
            customSpawnData.First(pair => pair.Key.Equals(WAR_TARGET_CLAN_ID)).Value.ForcedWarPeaceDataInstance.AtPeaceWithClans.Add(ATTACKER_CLAN_ID);
            var model = InitModelWithTogglableWarAndClansNotPartOfAKingdom(customSpawnData, false);
            
            bool isWarDeclarationPossible = model.IsWarDeclarationPossible(ValidClan(ATTACKER_CLAN_ID), ValidClan(WAR_TARGET_CLAN_ID));
        
            Assert.False(isWarDeclarationPossible);
        }
        
        [Test]
        public void IsWarDeclarationPossible_WhenAttackerIsPlayerFactionAndWarTargetIsCustomSpawnClan_ReturnsFalse()
        {
            var customSpawnData = InitAttackerAndWarTargetAsCustomSpawnsClans();
            var model = InitModelWithTogglableWarAndClansNotPartOfAKingdom(customSpawnData, false);
            
            bool isWarDeclarationPossible = model.IsWarDeclarationPossible(ValidClan(PLAYER_FACTION_ID), ValidClan(WAR_TARGET_CLAN_ID));
        
            Assert.False(isWarDeclarationPossible);
        }

















        [Test]
        public void IsPeaceDeclarationPossible_WhenAttackerIsNull_ReturnsFalse()
        {
            bool isWarDeclarationPossible = InitModel(new Dictionary<string, Data.Model.Diplomacy>()).IsPeaceDeclarationPossible(null, ValidClan(WAR_TARGET_CLAN_ID));

            Assert.False(isWarDeclarationPossible);
        }
        
        [Test]
        public void IsPeaceDeclarationPossible_WhenWarTargetAttackerIsNull_ReturnsFalse()
        {
            var attacker = ValidClan(ATTACKER_CLAN_ID);

            bool isWarDeclarationPossible = InitModel(new Dictionary<string, Data.Model.Diplomacy>()).IsPeaceDeclarationPossible(attacker, null);

            Assert.False(isWarDeclarationPossible);
        }
        
        [Test]
        public void IsPeaceDeclarationPossible_WhenAttackerFactionIsEliminated_ReturnsFalse()
        {
            bool isWarDeclarationPossible = InitModel(new Dictionary<string, Data.Model.Diplomacy>()).IsPeaceDeclarationPossible(EliminatedClan(ATTACKER_CLAN_ID), ValidClan(WAR_TARGET_CLAN_ID));

            Assert.False(isWarDeclarationPossible);
        }

        [Test]
        public void IsPeaceDeclarationPossible_WhenWarTargetIsEliminated_ReturnsFalse()
        {
            bool isWarDeclarationPossible = InitModel(new Dictionary<string, Data.Model.Diplomacy>()).IsPeaceDeclarationPossible(ValidClan(ATTACKER_CLAN_ID), EliminatedClan(WAR_TARGET_CLAN_ID));

            Assert.False(isWarDeclarationPossible);
        }

        [Test]
        public void IsPeaceDeclarationPossible_WhenBothFactionsAreTheSame_ReturnsFalse()
        {
            var attacker = ValidClan(ATTACKER_CLAN_ID);

            bool isWarDeclarationPossible = InitModel(new Dictionary<string, Data.Model.Diplomacy>()).IsPeaceDeclarationPossible(attacker, attacker);

            Assert.False(isWarDeclarationPossible);
        }

        [Test]
        public void IsPeaceDeclarationPossible_WhenAttackerAndWarTargetAreAtPeace_ReturnsFalse()
        {
            var customSpawnData = new Dictionary<string, Data.Model.Diplomacy>();
            var model = InitModelWithInitialFactionDiplomacy(customSpawnData, false);
            
            bool isWarDeclarationPossible = model.IsPeaceDeclarationPossible(ValidClan(ATTACKER_CLAN_ID), ValidClan(WAR_TARGET_CLAN_ID));

            Assert.False(isWarDeclarationPossible);
        }

        [Test]
        public void IsPeaceDeclarationPossible_WhenAttackerAndWarTargetAreNotCustomSpawnFactions_ReturnsFalse()
        {
            var customSpawnData = new Dictionary<string, Data.Model.Diplomacy>();
            var model = InitModelWithInitialFactionDiplomacy(customSpawnData, true);
            
            bool isWarDeclarationPossible = model.IsPeaceDeclarationPossible(ValidClan(ATTACKER_CLAN_ID), ValidClan(WAR_TARGET_CLAN_ID));

            Assert.False(isWarDeclarationPossible);
        }

        [Test]
        public void IsPeaceDeclarationPossible_WhenAttackerAndWarTargetHaveNoForcedWarPeaceBehaviour_ReturnsFalse()
        {
            var customSpawnData = InitAttackerAndWarTargetAsCustomSpawnsClans();
            customSpawnData.First(pair => pair.Key.Equals(ATTACKER_CLAN_ID)).Value.ForcedWarPeaceDataInstance = null;
            customSpawnData.First(pair => pair.Key.Equals(WAR_TARGET_CLAN_ID)).Value.ForcedWarPeaceDataInstance = null;
            var model = InitModelWithInitialFactionDiplomacy(customSpawnData, true);
            
            bool isWarDeclarationPossible = model.IsPeaceDeclarationPossible(ValidClan(ATTACKER_CLAN_ID), ValidClan(WAR_TARGET_CLAN_ID));

            Assert.False(isWarDeclarationPossible);
        }
        
        [Test]
        public void IsPeaceDeclarationPossible_WhenAttackerAndWarTargetAreNotPartOfAKingdomAndWarTargetIsNotAnException_ReturnsFalse()
        {
            var customSpawnData = InitAttackerAsCustomSpawnsClan();
            customSpawnData.First(pair => pair.Key.Equals(ATTACKER_CLAN_ID)).Value.ForcedWarPeaceDataInstance.ExceptionKingdoms.Add("vlandia");
            var model = InitModelWithTogglableWarAndClansNotPartOfAKingdom(customSpawnData, true);
            
            bool isWarDeclarationPossible = model.IsPeaceDeclarationPossible(ValidClan(ATTACKER_CLAN_ID), ValidClan(WAR_TARGET_CLAN_ID));

            Assert.False(isWarDeclarationPossible);
        }
        
        [Test]
        public void IsPeaceDeclarationPossible_WhenAttackerAndWarTargetAreNotPartOfAKingdomAndWarTargetIsForcedPeace_ReturnsTrue()
        {
            var customSpawnData = InitAttackerAsCustomSpawnsClan();
            customSpawnData.First(pair => pair.Key.Equals(ATTACKER_CLAN_ID)).Value.ForcedWarPeaceDataInstance.ExceptionKingdoms.Add("vlandia");
            customSpawnData.First(pair => pair.Key.Equals(ATTACKER_CLAN_ID)).Value.ForcedWarPeaceDataInstance.AtPeaceWithClans.Add(WAR_TARGET_CLAN_ID);
            var model = InitModelWithTogglableWarAndClansNotPartOfAKingdom(customSpawnData, true);
            
            bool isWarDeclarationPossible = model.IsPeaceDeclarationPossible(ValidClan(ATTACKER_CLAN_ID), ValidClan(WAR_TARGET_CLAN_ID));

            Assert.True(isWarDeclarationPossible);
        }
        
        [Test]
        public void IsPeaceDeclarationPossible_WhenAttackerIsNotPartOfAKingdomAndWarTargetIsAKingdomNotInExceptionKingdom_ReturnsFalse()
        {
            var customSpawnData = InitAttackerAsCustomSpawnsClan();
            var model = InitModelWithTogglableWarAndClansNotPartOfAKingdom(customSpawnData, true);
            
            bool isWarDeclarationPossible = model.IsPeaceDeclarationPossible(ValidClan(ATTACKER_CLAN_ID), ValidKingdom(WAR_TARGET_CLAN_ID));

            Assert.False(isWarDeclarationPossible);
        }
        
        [Test]
        public void IsPeaceDeclarationPossible_WhenAttackerIsNotPartOfAKingdomAndWarTargetIsAKingdomInExceptionKingdom_ReturnsTrue()
        {
            var customSpawnData = InitAttackerAsCustomSpawnsClan();
            customSpawnData.First(pair => pair.Key.Equals(ATTACKER_CLAN_ID)).Value.ForcedWarPeaceDataInstance.ExceptionKingdoms.Add(WAR_TARGET_CLAN_ID);
            var model = InitModelWithTogglableWarAndClansNotPartOfAKingdom(customSpawnData, true);
            
            bool isWarDeclarationPossible = model.IsPeaceDeclarationPossible(ValidClan(ATTACKER_CLAN_ID), ValidKingdom(WAR_TARGET_CLAN_ID));

            Assert.True(isWarDeclarationPossible);
        }

        [Test]
        public void IsPeaceDeclarationPossible_WhenAttackerIsNotPartOfAKingdomAndWarTargetIsAClanInAKingdom_ReturnsFalse()
        {
            var customSpawnData = InitAttackerAsCustomSpawnsClan();
            var model = InitModelWithTogglableWarAndAttackerNotInAKingdomAndWarTargetInAKingdom(customSpawnData, true);
            
            bool isWarDeclarationPossible = model.IsPeaceDeclarationPossible(ValidClan(ATTACKER_CLAN_ID), ValidClan(WAR_TARGET_CLAN_ID));
        
            Assert.False(isWarDeclarationPossible);
        }
        
        [Test]
        public void IsPeaceDeclarationPossible_WhenAttackerIsNotPartOfAKingdomAndWarTargetIsAClanInAKingdomInExceptionKingdom_ReturnsTrue()
        {
            var customSpawnData = InitAttackerAsCustomSpawnsClan();
            customSpawnData.First(pair => pair.Key.Equals(ATTACKER_CLAN_ID)).Value.ForcedWarPeaceDataInstance.ExceptionKingdoms.Add("vlandia");
            var model = InitModelWithTogglableWarAndAttackerNotInAKingdomAndWarTargetInAKingdom(customSpawnData, true);
            
            bool isWarDeclarationPossible = model.IsPeaceDeclarationPossible(ValidClan(ATTACKER_CLAN_ID), ValidClan(WAR_TARGET_CLAN_ID));
        
            Assert.True(isWarDeclarationPossible);
        }
        
        [Test]
        public void IsPeaceDeclarationPossible_WhenAttackerIsNotPartOfAKingdomAndWarTargetIsAClanWithAtPeaceFlagInAKingdomInExceptionKingdom_ReturnsTrue()
        {
            var customSpawnData = InitAttackerAsCustomSpawnsClan();
            customSpawnData.First(pair => pair.Key.Equals(ATTACKER_CLAN_ID)).Value.ForcedWarPeaceDataInstance.ExceptionKingdoms.Add("vlandia");
            customSpawnData.First(pair => pair.Key.Equals(ATTACKER_CLAN_ID)).Value.ForcedWarPeaceDataInstance.AtPeaceWithClans.Add(WAR_TARGET_CLAN_ID);
            var model = InitModelWithTogglableWarAndAttackerNotInAKingdomAndWarTargetInAKingdom(customSpawnData, true);

            bool isWarDeclarationPossible = model.IsPeaceDeclarationPossible(ValidClan(ATTACKER_CLAN_ID), ValidClan(WAR_TARGET_CLAN_ID));
        
            Assert.True(isWarDeclarationPossible);
        }

        [Test]
        public void IsPeaceDeclarationPossible_WhenAttackerIsNotPartOfAKingdomAndWarTargetIsAClanWithAtPeaceFlagInAKingdom_ReturnsFalse()
        {
            var customSpawnData = InitAttackerAsCustomSpawnsClan();
            customSpawnData.First(pair => pair.Key.Equals(ATTACKER_CLAN_ID)).Value.ForcedWarPeaceDataInstance.AtPeaceWithClans.Add(WAR_TARGET_CLAN_ID);
            // customSpawnData.First(pair => pair.Key.Equals(WAR_TARGET_CLAN_ID)).Value.ForcedWarPeaceDataInstance.AtPeaceWithClans.Add(ATTACKER_CLAN_ID);
            var model = InitModelWithTogglableWarAndAttackerNotInAKingdomAndWarTargetInAKingdom(customSpawnData, true);

            bool isWarDeclarationPossible = model.IsPeaceDeclarationPossible(ValidClan(ATTACKER_CLAN_ID), ValidClan(WAR_TARGET_CLAN_ID));
        
            Assert.False(isWarDeclarationPossible);
        }
        
        [Test]
        public void IsPeaceDeclarationPossible_WhenAttackerAndWarTargetArePartOfAKingdom_ReturnsFalse()
        {
            var customSpawnData = InitAttackerAsCustomSpawnsClan();
            var model = InitModelWithTogglableWarAndAttackerAndWarTargetInADifferentKingdom(customSpawnData, true);

            bool isWarDeclarationPossible = model.IsPeaceDeclarationPossible(ValidClan(ATTACKER_CLAN_ID), ValidClan(WAR_TARGET_CLAN_ID));
        
            Assert.False(isWarDeclarationPossible);
        }
        
        [Test]
        public void IsPeaceDeclarationPossible_WhenAttackerAndWarTargetArePartOfAKingdomWithWarTargetKingdomInExceptionKingdom_ReturnsTrue()
        {
            var customSpawnData = InitAttackerAsCustomSpawnsClan();
            customSpawnData.First(pair => pair.Key.Equals(ATTACKER_CLAN_ID)).Value.ForcedWarPeaceDataInstance.ExceptionKingdoms.Add("vlandia");
            var model = InitModelWithTogglableWarAndAttackerAndWarTargetInADifferentKingdom(customSpawnData, true);

            bool isWarDeclarationPossible = model.IsPeaceDeclarationPossible(ValidClan(ATTACKER_CLAN_ID), ValidClan(WAR_TARGET_CLAN_ID));
        
            Assert.True(isWarDeclarationPossible);
        }
        
        [Test]
        public void IsPeaceDeclarationPossible_WhenAttackerAndWarTargetArePartOfAKingdomWithForcedPeace_ReturnsFalse()
        {
            var customSpawnData = InitAttackerAsCustomSpawnsClan();
            customSpawnData.First(pair => pair.Key.Equals(ATTACKER_CLAN_ID)).Value.ForcedWarPeaceDataInstance.AtPeaceWithClans.Add(WAR_TARGET_CLAN_ID);
            var model = InitModelWithTogglableWarAndAttackerAndWarTargetInADifferentKingdom(customSpawnData, true);

            bool isWarDeclarationPossible = model.IsPeaceDeclarationPossible(ValidClan(ATTACKER_CLAN_ID), ValidClan(WAR_TARGET_CLAN_ID));
        
            Assert.False(isWarDeclarationPossible);
        }
        
        [Test]
        public void IsPeaceDeclarationPossible_WhenAttackerAndWarTargetArePartOfAKingdomWithForcedPeaceAndWarTargetInExceptionKingdom_ReturnsTrue()
        {
            var customSpawnData = InitAttackerAsCustomSpawnsClan();
            customSpawnData.First(pair => pair.Key.Equals(ATTACKER_CLAN_ID)).Value.ForcedWarPeaceDataInstance.ExceptionKingdoms.Add("vlandia");
            customSpawnData.First(pair => pair.Key.Equals(ATTACKER_CLAN_ID)).Value.ForcedWarPeaceDataInstance.AtPeaceWithClans.Add(WAR_TARGET_CLAN_ID);
            var model = InitModelWithTogglableWarAndAttackerAndWarTargetInADifferentKingdom(customSpawnData, true);

            bool isWarDeclarationPossible = model.IsPeaceDeclarationPossible(ValidClan(ATTACKER_CLAN_ID), ValidClan(WAR_TARGET_CLAN_ID));
        
            Assert.True(isWarDeclarationPossible);
        }
        
        [Test]
        public void IsPeaceDeclarationPossible_WhenAttackerAndWarTargetArePartOfTheSameKingdom_ReturnsTrue()
        {
            var customSpawnData = InitAttackerAsCustomSpawnsClan();
            var model = InitModelWithTogglableWarAndAttackerAndWarTargetInSameKingdom(customSpawnData, true);

            bool isWarDeclarationPossible = model.IsPeaceDeclarationPossible(ValidClan(ATTACKER_CLAN_ID), ValidClan(WAR_TARGET_CLAN_ID));
        
            Assert.True(isWarDeclarationPossible);
        }
        
        [Test]
        public void IsPeaceDeclarationPossible_WhenAttackerIsPartOfAKingdomAndWarTargetIsNotInAKingdomWithAtPeaceFlag_ReturnsTrue()
        {
            var customSpawnData = InitAttackerAsCustomSpawnsClan();
            customSpawnData.First(pair => pair.Key.Equals(ATTACKER_CLAN_ID)).Value.ForcedWarPeaceDataInstance.AtPeaceWithClans.Add(WAR_TARGET_CLAN_ID);
            var model = InitModelWithTogglableWarAndAttackerInAKingdomAndWarTargetNotInAKingdom(customSpawnData, true);

            bool isWarDeclarationPossible = model.IsPeaceDeclarationPossible(ValidClan(ATTACKER_CLAN_ID), ValidClan(WAR_TARGET_CLAN_ID));
        
            Assert.True(isWarDeclarationPossible);
        }
        
        [Test]
        public void IsPeaceDeclarationPossible_WhenAttackerIsPartOfADifferentKingdomAndWarTargetIsNotInAKingdom_ReturnsFalse()
        {
            var customSpawnData = InitAttackerAsCustomSpawnsClan();
            var model = InitModelWithTogglableWarAndAttackerInAKingdomAndWarTargetNotInAKingdom(customSpawnData, true);

            bool isWarDeclarationPossible = model.IsPeaceDeclarationPossible(ValidClan(ATTACKER_CLAN_ID), ValidClan(WAR_TARGET_CLAN_ID));
        
            Assert.False(isWarDeclarationPossible);
        }
        
        
        [Test]
        public void IsPeaceDeclarationPossible_WhenAttackerAndWarTargetAreCustomSpawnClansWithAttackerAndWarTargetAtPeaceData_ReturnsTrue()
        {
            var customSpawnData = InitAttackerAndWarTargetAsCustomSpawnsClans();
            customSpawnData.First(pair => pair.Key.Equals(ATTACKER_CLAN_ID)).Value.ForcedWarPeaceDataInstance.AtPeaceWithClans.Add(WAR_TARGET_CLAN_ID);
            customSpawnData.First(pair => pair.Key.Equals(WAR_TARGET_CLAN_ID)).Value.ForcedWarPeaceDataInstance.AtPeaceWithClans.Add(ATTACKER_CLAN_ID);
            var model = InitModelWithTogglableWarAndClansNotPartOfAKingdom(customSpawnData, true);
            
            bool isWarDeclarationPossible = model.IsPeaceDeclarationPossible(ValidClan(ATTACKER_CLAN_ID), ValidClan(WAR_TARGET_CLAN_ID));
        
            Assert.True(isWarDeclarationPossible);
        }
        
        [Test]
        public void IsPeaceDeclarationPossible_WhenAttackerAndWarTargetAreCustomSpawnClansAndAttackerHasWarData_ReturnsFalse()
        {
            var customSpawnData = InitAttackerAndWarTargetAsCustomSpawnsClans();
            customSpawnData.First(pair => pair.Key.Equals(ATTACKER_CLAN_ID)).Value.ForcedWarPeaceDataInstance.AtPeaceWithClans.Add(WAR_TARGET_CLAN_ID);
            var model = InitModelWithTogglableWarAndClansNotPartOfAKingdom(customSpawnData, true);
            
            bool isWarDeclarationPossible = model.IsPeaceDeclarationPossible(ValidClan(ATTACKER_CLAN_ID), ValidClan(WAR_TARGET_CLAN_ID));
        
            Assert.False(isWarDeclarationPossible);
        }
        
        [Test]
        public void IsPeaceDeclarationPossible_WhenAttackerAndWarTargetAreCustomSpawnClansAndPeaceTargetHasWarData_ReturnsFalse()
        {
            var customSpawnData = InitAttackerAndWarTargetAsCustomSpawnsClans();
            customSpawnData.First(pair => pair.Key.Equals(WAR_TARGET_CLAN_ID)).Value.ForcedWarPeaceDataInstance.AtPeaceWithClans.Add(ATTACKER_CLAN_ID);
            var model = InitModelWithTogglableWarAndClansNotPartOfAKingdom(customSpawnData, true);
            
            bool isWarDeclarationPossible = model.IsPeaceDeclarationPossible(ValidClan(ATTACKER_CLAN_ID), ValidClan(WAR_TARGET_CLAN_ID));
        
            Assert.False(isWarDeclarationPossible);
        }
        
        [Test]
        public void IsPeaceDeclarationPossible_WhenAttackerAndWarTargetAreCustomSpawnClansWithAttackerAndPeaceTargetWithWarData_ReturnsFalse()
        {
            var customSpawnData = InitAttackerAndWarTargetAsCustomSpawnsClans();
            var model = InitModelWithTogglableWarAndClansNotPartOfAKingdom(customSpawnData, true);
            
            bool isWarDeclarationPossible = model.IsPeaceDeclarationPossible(ValidClan(ATTACKER_CLAN_ID), ValidClan(WAR_TARGET_CLAN_ID));
        
            Assert.False(isWarDeclarationPossible);
        }

        [Test]
        public void IsPeaceDeclarationPossible_WhenAttackerIsPlayerFactionAndWarTargetIsCustomSpawnClan_ReturnsFalse()
        {
            var customSpawnData = InitAttackerAndWarTargetAsCustomSpawnsClans();
            var model = InitModelWithTogglableWarAndClansNotPartOfAKingdom(customSpawnData, true);

            bool isPeaceDeclarationPossible = model.IsPeaceDeclarationPossible(ValidClan(PLAYER_FACTION_ID), ValidClan(WAR_TARGET_CLAN_ID));
        
            Assert.False(isPeaceDeclarationPossible);
        }
    }
}