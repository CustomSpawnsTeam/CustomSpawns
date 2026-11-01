using System.Reflection;
using System.Runtime.Serialization;
using CustomSpawns.Invasions.Adapters;
using CustomSpawns.Spawn.PartyComponents;
using NUnit.Framework;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace CustomSpawns.Tests.Invasions
{
    public class BannerlordCapitalObjectivePartyEligibilityTests
    {
        private readonly BannerlordCapitalObjectivePartyEligibility _eligibility = new();

        [Test]
        public void LeaderlessCustomInvasionEscortsMayParticipateButCannotCommand()
        {
            MobileParty escort = CustomParty();

            Assert.That(escort.IsLordParty, Is.False);
            Assert.That(_eligibility.IsCombatParty(escort), Is.True);
            Assert.That(_eligibility.CanCommand(escort), Is.False);
        }

        [Test]
        public void SpawnedCommanderDoesNotNeedVanillasLordOnlyRecruitmentSelector()
        {
            MobileParty commander = CustomParty();
            Hero leader = GiveLeader(commander, Hero.CharacterStates.Active);

            Assert.That(commander.IsLordParty, Is.False);
            Assert.That(leader.PartyBelongedTo, Is.SameAs(commander));
            Assert.That(_eligibility.CanCommand(commander), Is.True);
        }

        [TestCase(Hero.CharacterStates.Prisoner)]
        [TestCase(Hero.CharacterStates.Fugitive)]
        [TestCase(Hero.CharacterStates.Dead)]
        public void UnavailableLeaderCannotCommand(Hero.CharacterStates state)
        {
            MobileParty party = CustomParty();
            GiveLeader(party, state);

            Assert.That(_eligibility.CanCommand(party), Is.False);
        }

        [Test]
        public void HeroOwningAnotherPartyCannotCommandThisOne()
        {
            MobileParty party = CustomParty();
            Hero leader = GiveLeader(party, Hero.CharacterStates.Active);
            SetField(leader, "_partyBelongedTo", CustomParty());

            Assert.That(_eligibility.CanCommand(party), Is.False);
        }

        [Test]
        public void PartiesAtSeaCannotCreateACapitalArmy()
        {
            MobileParty party = CustomParty();
            GiveLeader(party, Hero.CharacterStates.Active);
            SetField(party, "_isCurrentlyAtSea", true);

            Assert.That(_eligibility.CanCommand(party), Is.False);
        }

        [Test]
        public void OrdinaryLordPartiesRemainCombatPartiesAndOtherComponentsAreExcluded()
        {
            var party = (MobileParty)FormatterServices.GetUninitializedObject(typeof(MobileParty));
            Assert.That(_eligibility.IsCombatParty(party), Is.False);

            typeof(MobileParty).GetProperty(nameof(MobileParty.IsLordParty))!.SetValue(party, true);
            Assert.That(_eligibility.IsCombatParty(party), Is.True);
        }

        private static MobileParty CustomParty()
        {
            var party = (MobileParty)FormatterServices.GetUninitializedObject(typeof(MobileParty));
            SetField(party, "_partyComponent", new CustomSpawnsPartyComponent(null!, null!, null!));
            return party;
        }

        private static Hero GiveLeader(MobileParty party, Hero.CharacterStates state)
        {
            var hero = (Hero)FormatterServices.GetUninitializedObject(typeof(Hero));
            SetField(hero, "_heroState", state);
            SetField(hero, "_partyBelongedTo", party);
            SetField(party.PartyComponent, "_leader", hero);
            return hero;
        }

        private static void SetField(object instance, string name, object value) =>
            instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(instance, value);
    }
}
