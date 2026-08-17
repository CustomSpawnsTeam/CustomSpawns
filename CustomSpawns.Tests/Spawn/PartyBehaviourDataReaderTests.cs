using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CustomSpawns.Data.Adapter;
using CustomSpawns.Data.Dto;
using CustomSpawns.Data.Model;
using CustomSpawns.Data.Reader.Impl;
using NUnit.Framework;

namespace CustomSpawns.Tests.Spawn
{
    public class PartyBehaviourDataReaderTests
    {
        [Test]
        public void DeserializeInto_KeepsTemplateOrderAndBehaviourOrder()
        {
            List<PriorityPartyBehaviourTemplate> templates = new();
            List<System.Exception> captured = new();

            Read(Document(Template("raider", "<RaidClosestVillage/>" +
                                             "<PatrolAroundClosestLestInterruptedAndSwitch/>") +
                          Template("besieger", "<BesiegeClosestSettlement/>")), templates, captured);

            Assert.Multiple(() =>
            {
                Assert.That(captured, Is.Empty);
                Assert.That(templates.Select(template => template.Id), Is.EqualTo(new[] { "raider", "besieger" }));
                Assert.That(Adapt(templates[0]).PriorityPartyBehaviours.Select(behaviour => behaviour.GetType()),
                    Is.EqualTo(new[]
                    {
                        typeof(RaidClosestVillageData),
                        typeof(PatrolAroundClosestLestInterruptedAndSwitchData)
                    }));
            });
        }

        [Test]
        public void DeserializeInto_EmptyBehaviourWrapperIsValidAndAdaptsToNoBehaviour()
        {
            List<PriorityPartyBehaviourTemplate> templates = new();
            List<System.Exception> captured = new();

            Read(Document(Template("empty", "")), templates, captured);

            Assert.Multiple(() =>
            {
                Assert.That(captured, Is.Empty);
                Assert.That(Adapt(templates.Single()).PriorityPartyBehaviours, Is.Empty);
            });
        }

        [TestCase("<Id> </Id><PriorityPartyBehaviours><RaidClosestVillage/></PriorityPartyBehaviours>", "blank Id")]
        [TestCase("<Id>no-wrapper</Id>", "missing its PriorityPartyBehaviours wrapper")]
        public void DeserializeInto_ReportsAndDropsInvalidTemplateButKeepsTheRest(string templateBody,
            string expectedText)
        {
            List<PriorityPartyBehaviourTemplate> templates = new();
            List<System.Exception> captured = new();

            Read(Document("<PriorityPartyBehaviourTemplate>" + templateBody + "</PriorityPartyBehaviourTemplate>" +
                          Template("valid", "<RaidClosestVillage/>")), templates, captured);

            Assert.Multiple(() =>
            {
                Assert.That(captured.Single(), Is.TypeOf<ArgumentException>());
                Assert.That(captured.Single().Message, Does.Contain(expectedText));
                Assert.That(templates.Single().Id, Is.EqualTo("valid"));
            });
        }

        [Test]
        public void DeserializeInto_ReportsAndDropsDuplicateIdWithinOneFile()
        {
            List<PriorityPartyBehaviourTemplate> templates = new();
            List<System.Exception> captured = new();

            Read(Document(Template("same", "<RaidClosestVillage/>") +
                          Template("same", "<PatrolAroundClosestLestInterruptedAndSwitch/>")), templates, captured);

            Assert.Multiple(() =>
            {
                Assert.That(captured.Single().Message, Does.Contain("Duplicate"));
                Assert.That(Adapt(templates.Single()).PriorityPartyBehaviours.Single(),
                    Is.TypeOf<RaidClosestVillageData>());
            });
        }

        [Test]
        public void DeserializeInto_TemplateIdsAreUniqueAcrossFilesAndCompareOrdinally()
        {
            List<PriorityPartyBehaviourTemplate> templates = new();
            List<System.Exception> captured = new();

            Read(Document(Template("id", "<RaidClosestVillage/>")), templates, captured);
            Read(Document(Template("ID", "<RaidClosestVillage/>") +
                          Template("id", "<PatrolAroundClosestLestInterruptedAndSwitch/>")), templates, captured);

            Assert.Multiple(() =>
            {
                Assert.That(captured.Single().Message, Does.Contain("Duplicate"));
                Assert.That(templates.Select(template => template.Id), Is.EqualTo(new[] { "id", "ID" }));
            });
        }

        [Test]
        public void DeserializeInto_MalformedXmlIsReportedAndAddsNothing()
        {
            List<PriorityPartyBehaviourTemplate> templates = new();
            List<System.Exception> captured = new();

            Read("<PriorityPartyBehaviourTemplates><PriorityPartyBehaviourTemplate>", templates, captured);

            Assert.Multiple(() =>
            {
                Assert.That(captured.Single(), Is.TypeOf<InvalidOperationException>());
                Assert.That(templates, Is.Empty);
            });
        }

        [Test]
        public void DeserializeInto_NilRootIsReportedOnce()
        {
            List<PriorityPartyBehaviourTemplate> templates = new();
            List<System.Exception> captured = new();

            Read("<PriorityPartyBehaviourTemplates xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" " +
                 "xsi:nil=\"true\"/>", templates, captured);

            Assert.Multiple(() =>
            {
                Assert.That(captured, Has.Count.EqualTo(1));
                Assert.That(captured[0], Is.TypeOf<InvalidOperationException>());
                Assert.That(templates, Is.Empty);
            });
        }

        private static void Read(string xml, IList<PriorityPartyBehaviourTemplate> templates,
            ICollection<System.Exception> captured)
        {
            using MemoryStream stream = new(Encoding.UTF8.GetBytes(xml));
            PartyBehaviourDataReader.DeserializeInto(stream, templates, error => captured.Add(error));
        }

        private static PartyBehaviourDto Adapt(PriorityPartyBehaviourTemplate template)
        {
            return new PartyBehaviourDtoAdapter().Adapt(template);
        }

        private static string Document(string templates)
        {
            return "<PriorityPartyBehaviourTemplates>" + templates + "</PriorityPartyBehaviourTemplates>";
        }

        private static string Template(string id, string behaviours)
        {
            return "<PriorityPartyBehaviourTemplate><Id>" + id + "</Id><PriorityPartyBehaviours>" + behaviours +
                   "</PriorityPartyBehaviours></PriorityPartyBehaviourTemplate>";
        }
    }
}
