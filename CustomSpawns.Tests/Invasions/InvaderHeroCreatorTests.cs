using System.Reflection;
using System.Runtime.Serialization;
using CustomSpawns.Invasions;
using CustomSpawns.Invasions.Adapters;
using Moq;
using NUnit.Framework;
using TaleWorlds.CampaignSystem;

namespace CustomSpawns.Tests.Invasions
{
    public class InvaderHeroCreatorTests
    {
        [TestCase(Hero.CharacterStates.Active, true)]
        [TestCase(Hero.CharacterStates.NotSpawned, false)]
        [TestCase(Hero.CharacterStates.Fugitive, false)]
        [TestCase(Hero.CharacterStates.Prisoner, false)]
        [TestCase(Hero.CharacterStates.Released, false)]
        [TestCase(Hero.CharacterStates.Dead, false)]
        [TestCase(Hero.CharacterStates.Disabled, false)]
        [TestCase(Hero.CharacterStates.Traveling, false)]
        public void ReusesOnlyAnActiveClanLeader(Hero.CharacterStates state, bool reuse)
        {
            var leader = (Hero)FormatterServices.GetUninitializedObject(typeof(Hero));
            typeof(Hero).GetField("_heroState", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(leader, state);
            var clan = (Clan)FormatterServices.GetUninitializedObject(typeof(Clan));
            typeof(Clan).GetField("_leader", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(clan, leader);
            var creator = new InvaderHeroCreator(Mock.Of<IInvasionClanRepository>(),
                Mock.Of<IInvasionGarrisonArmyLeaders>());

            // No culture means there is no replacement template if the leader is unavailable.
            Hero? selected = creator.CreateLord(new Invasion("invasion", "clan"), clan, null!);

            Assert.That(ReferenceEquals(selected, leader), Is.EqualTo(reuse));
        }
    }
}
