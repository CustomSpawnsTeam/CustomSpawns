using System.IO;
using System.Xml.Serialization;
using NUnit.Framework;
using SpawnModel = CustomSpawns.Data.Model.Spawn;

namespace CustomSpawns.Tests.Spawn
{
    public class SpawnPatrolConfigTests
    {
        [Test]
        public void Spawn_DeserializesPatrolAroundClosestLestInterruptedAndSwitch()
        {
            const string xml = "<RegularBanditDailySpawnData party_template=\"PartyTemplate.test\">" +
                               "<PatrolAroundClosestLestInterruptedAndSwitch min_stable_days=\"2\" " +
                               "try_patrol_around=\"Town|Castle\">true" +
                               "</PatrolAroundClosestLestInterruptedAndSwitch>" +
                               "</RegularBanditDailySpawnData>";
            XmlSerializer serializer = new XmlSerializer(typeof(SpawnModel));

            using StringReader reader = new StringReader(xml);
            SpawnModel spawn = (SpawnModel)serializer.Deserialize(reader);

            Assert.That(spawn.PatrolAroundConfig, Is.Not.Null);
            Assert.That(spawn.PatrolAroundConfig!.IsEnabled, Is.True);
            Assert.That(spawn.PatrolAroundConfig.MinStableDays, Is.EqualTo(2f));
            Assert.That(spawn.PatrolAroundConfig.MaxStableDays, Is.EqualTo(10f));
            Assert.That(spawn.PatrolAroundConfig.PatrolLocationType, Is.EqualTo("Town|Castle"));
        }
    }
}
