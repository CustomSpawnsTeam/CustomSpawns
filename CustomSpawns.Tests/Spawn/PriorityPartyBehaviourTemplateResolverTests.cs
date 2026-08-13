using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using CustomSpawns.Data.Adapter;
using CustomSpawns.Data.Dto;
using CustomSpawns.Data.Model;
using CustomSpawns.Data.Reader.Impl;
using NUnit.Framework;

namespace CustomSpawns.Tests.Spawn
{
    public class PriorityPartyBehaviourTemplateResolverTests
    {
        [Test]
        public void Resolve_PreservesOldInlineAndLegacyConfigurationsWhenNoReferenceExists()
        {
            Spawns document = Deserialize(
                "<RegularBanditDailySpawnData party_template=\"inline\">" +
                "<PriorityPartyBehaviours><RaidClosestVillage/></PriorityPartyBehaviours>" +
                "</RegularBanditDailySpawnData>" +
                "<RegularBanditDailySpawnData party_template=\"legacy\">" +
                "<PatrolAroundClosestLestInterruptedAndSwitch>true</PatrolAroundClosestLestInterruptedAndSwitch>" +
                "</RegularBanditDailySpawnData>");
            List<PriorityPartyBehaviourConfig>? inlineBehaviours = document.AllSpawns[0].PriorityPartyBehaviours;

            PriorityPartyBehaviourTemplateResolver.Resolve(document);

            Assert.Multiple(() =>
            {
                Assert.That(document.AllSpawns[0].PriorityPartyBehaviours, Is.SameAs(inlineBehaviours));
                Assert.That(SpawnDtoAdapter.AdaptPriorityPartyBehaviours(document.AllSpawns[0]).Single(),
                    Is.TypeOf<RaidClosestVillageData>());
                Assert.That(document.AllSpawns[1].PriorityPartyBehavioursSpecified, Is.False);
                Assert.That(SpawnDtoAdapter.AdaptPriorityPartyBehaviours(document.AllSpawns[1]).Single(),
                    Is.TypeOf<PatrolAroundClosestLestInterruptedAndSwitchData>());
            });
        }

        [Test]
        public void Resolve_SupportsForwardReferenceReuseAndPreservesAdapterOrder()
        {
            Spawns document = Deserialize(
                "<RegularBanditDailySpawnData party_template=\"first\">" +
                "<PriorityPartyBehaviourTemplateId>raid-and-patrol</PriorityPartyBehaviourTemplateId>" +
                "</RegularBanditDailySpawnData>" +
                "<RegularBanditDailySpawnData party_template=\"second\">" +
                "<PriorityPartyBehaviourTemplateId>raid-and-patrol</PriorityPartyBehaviourTemplateId>" +
                "</RegularBanditDailySpawnData>" +
                "<PriorityPartyBehaviourTemplates><PriorityPartyBehaviourTemplate>" +
                "<Id>raid-and-patrol</Id><PriorityPartyBehaviours>" +
                "<RaidClosestVillage/><PatrolAroundClosestLestInterruptedAndSwitch/>" +
                "</PriorityPartyBehaviours></PriorityPartyBehaviourTemplate></PriorityPartyBehaviourTemplates>");

            PriorityPartyBehaviourTemplateResolver.Resolve(document);

            Assert.Multiple(() =>
            {
                Assert.That(document.AllSpawns[0].PriorityPartyBehaviours,
                    Is.Not.SameAs(document.AllSpawns[1].PriorityPartyBehaviours));
                Assert.That(SpawnDtoAdapter.AdaptPriorityPartyBehaviours(document.AllSpawns[0])
                    .Select(behaviour => behaviour.GetType()), Is.EqualTo(new[]
                    {
                        typeof(RaidClosestVillageData),
                        typeof(PatrolAroundClosestLestInterruptedAndSwitchData)
                    }));
            });

            document.AllSpawns[0].PriorityPartyBehaviours!.RemoveAt(0);
            Assert.That(document.AllSpawns[1].PriorityPartyBehaviours, Has.Count.EqualTo(2));
        }

        [Test]
        public void Resolve_ReferencedExplicitEmptyTemplateSuppressesLegacyPatrol()
        {
            Spawns document = Deserialize(
                "<PriorityPartyBehaviourTemplates><PriorityPartyBehaviourTemplate>" +
                "<Id>empty</Id><PriorityPartyBehaviours/>" +
                "</PriorityPartyBehaviourTemplate></PriorityPartyBehaviourTemplates>" +
                "<RegularBanditDailySpawnData party_template=\"legacy\">" +
                "<PatrolAroundClosestLestInterruptedAndSwitch>true</PatrolAroundClosestLestInterruptedAndSwitch>" +
                "<PriorityPartyBehaviourTemplateId>empty</PriorityPartyBehaviourTemplateId>" +
                "</RegularBanditDailySpawnData>");

            PriorityPartyBehaviourTemplateResolver.Resolve(document);

            Assert.Multiple(() =>
            {
                Assert.That(document.AllSpawns[0].PriorityPartyBehavioursSpecified, Is.True);
                Assert.That(SpawnDtoAdapter.AdaptPriorityPartyBehaviours(document.AllSpawns[0]), Is.Empty);
            });
        }

        [Test]
        public void Resolve_AllowsSameTemplateIdInDifferentDocuments()
        {
            Spawns first = Deserialize(Template("same", "<RaidClosestVillage/>") + ReferencingSpawn("first", "same"));
            Spawns second = Deserialize(Template("same", "<PatrolAroundClosestLestInterruptedAndSwitch/>") +
                                        ReferencingSpawn("second", "same"));

            PriorityPartyBehaviourTemplateResolver.Resolve(first);
            PriorityPartyBehaviourTemplateResolver.Resolve(second);

            Assert.Multiple(() =>
            {
                Assert.That(first.AllSpawns[0].PriorityPartyBehaviours!.Single(), Is.TypeOf<RaidClosestVillageConfig>());
                Assert.That(second.AllSpawns[0].PriorityPartyBehaviours!.Single(),
                    Is.TypeOf<PatrolAroundClosestLestInterruptedAndSwitchConfig>());
            });
        }

        [TestCase("<Id> </Id><PriorityPartyBehaviours/>", "blank")]
        [TestCase("<Id>duplicate</Id><PriorityPartyBehaviours/></PriorityPartyBehaviourTemplate>" +
                  "<PriorityPartyBehaviourTemplate><Id>duplicate</Id><PriorityPartyBehaviours/>", "duplicate")]
        [TestCase("<Id>missing-wrapper</Id>", "missing-wrapper")]
        public void Resolve_RejectsInvalidTemplateTable(string templateBody, string expectedText)
        {
            Spawns document = Deserialize(
                "<PriorityPartyBehaviourTemplates><PriorityPartyBehaviourTemplate>" + templateBody +
                "</PriorityPartyBehaviourTemplate></PriorityPartyBehaviourTemplates>");

            ArgumentException error = Assert.Throws<ArgumentException>(() =>
                PriorityPartyBehaviourTemplateResolver.Resolve(document))!;

            Assert.That(error.Message, Does.Contain(expectedText));
        }

        [TestCase("<PriorityPartyBehaviourTemplateId/>\n", "blank", "blank-ref")]
        [TestCase("<PriorityPartyBehaviourTemplateId>unknown</PriorityPartyBehaviourTemplateId>", "unknown", "unknown-ref")]
        [TestCase("<PriorityPartyBehaviours/><PriorityPartyBehaviourTemplateId>known</PriorityPartyBehaviourTemplateId>",
            "known", "inline-and-ref")]
        [TestCase("<PriorityPartyBehaviours><RaidClosestVillage/></PriorityPartyBehaviours>" +
                  "<PriorityPartyBehaviourTemplateId>known</PriorityPartyBehaviourTemplateId>",
            "known", "nonempty-inline-and-ref")]
        public void Resolve_RejectsInvalidSpawnReferences(string spawnBody, string expectedReference,
            string partyTemplate)
        {
            Spawns document = Deserialize(Template("known", "") +
                                          "<RegularBanditDailySpawnData party_template=\"" + partyTemplate + "\">" +
                                          spawnBody + "</RegularBanditDailySpawnData>");

            ArgumentException error = Assert.Throws<ArgumentException>(() =>
                PriorityPartyBehaviourTemplateResolver.Resolve(document))!;

            Assert.Multiple(() =>
            {
                Assert.That(error.Message, Does.Contain(expectedReference));
                Assert.That(error.Message, Does.Contain(partyTemplate));
            });
        }

        [Test]
        public void Resolve_TreatsTemplateIdsAsOrdinalAndCaseSensitive()
        {
            Spawns document = Deserialize(Template("id", "") + Template("ID", "") +
                                          ReferencingSpawn("lower", "id") + ReferencingSpawn("upper", "ID"));

            Assert.DoesNotThrow(() => PriorityPartyBehaviourTemplateResolver.Resolve(document));
        }

        [Test]
        public void SpawnDataReader_MalformedXmlUsesReaderErrorPath()
        {
            using MemoryStream stream = new(Encoding.UTF8.GetBytes("<root><RegularBanditDailySpawnData></root>"));
            System.Exception? captured = null;

            Spawns result = SpawnDataReader.DeserializeAndResolve(stream, error => captured = error);

            Assert.Multiple(() =>
            {
                Assert.That(captured, Is.TypeOf<InvalidOperationException>());
                Assert.That(result.AllSpawns, Is.Empty);
            });
        }

        [Test]
        public void SpawnDataReader_StructuralTemplateErrorRejectsWholeFile()
        {
            Spawns invalid = Deserialize(Template("known", "") + ReferencingSpawn("valid", "known") +
                                         ReferencingSpawn("invalid", "unknown"));
            XmlSerializer serializer = new(typeof(Spawns));
            using MemoryStream stream = new();
            serializer.Serialize(stream, invalid);
            stream.Position = 0;
            System.Exception? captured = null;

            Spawns result = SpawnDataReader.DeserializeAndResolve(stream, error => captured = error);

            Assert.Multiple(() =>
            {
                Assert.That(captured, Is.TypeOf<ArgumentException>());
                Assert.That(result.AllSpawns, Is.Empty);
            });
        }

        private static Spawns Deserialize(string contents)
        {
            XmlSerializer serializer = new(typeof(Spawns));
            using StringReader reader = new("<root>" + contents + "</root>");
            return (Spawns)serializer.Deserialize(reader);
        }

        private static string Template(string id, string behaviours)
        {
            return "<PriorityPartyBehaviourTemplates><PriorityPartyBehaviourTemplate><Id>" + id +
                   "</Id><PriorityPartyBehaviours>" + behaviours +
                   "</PriorityPartyBehaviours></PriorityPartyBehaviourTemplate></PriorityPartyBehaviourTemplates>";
        }

        private static string ReferencingSpawn(string partyTemplate, string templateId)
        {
            return "<RegularBanditDailySpawnData party_template=\"" + partyTemplate + "\">" +
                   "<PriorityPartyBehaviourTemplateId>" + templateId + "</PriorityPartyBehaviourTemplateId>" +
                   "</RegularBanditDailySpawnData>";
        }
    }
}
