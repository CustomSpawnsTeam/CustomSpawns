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
        public void Resolve_DeepClonesAllBehaviourConfigurationGraphsForEachSpawn()
        {
            Spawns document = Deserialize(
                Template("all", "<BesiegeClosestSettlement>" +
                                "<TargetTypes><TargetType>Town</TargetType></TargetTypes>" +
                                "<StrengthAssessment><MinimumAttackerToTargetRatio>2</MinimumAttackerToTargetRatio>" +
                                "</StrengthAssessment>" +
                                "<Reconnaissance><RequiredClearTimeInDays><Minimum>1</Minimum>" +
                                "</RequiredClearTimeInDays></Reconnaissance>" +
                                "<OperationWindowInDays><Maximum>8</Maximum></OperationWindowInDays>" +
                                "<Recovery><GracePeriodInDays>3</GracePeriodInDays></Recovery>" +
                                "</BesiegeClosestSettlement>" +
                                "<RaidClosestVillage>" +
                                "<StrengthAssessment><MinimumAttackerToRoamingHostilesRatio>2</MinimumAttackerToRoamingHostilesRatio>" +
                                "</StrengthAssessment>" +
                                "<Reconnaissance><RequiredClearTimeInDays><Maximum>2</Maximum>" +
                                "</RequiredClearTimeInDays></Reconnaissance>" +
                                "<OperationWindowInDays><Minimum>4</Minimum></OperationWindowInDays>" +
                                "<Recovery><MaximumFailedAttempts>4</MaximumFailedAttempts></Recovery>" +
                                "</RaidClosestVillage>" +
                                "<PatrolAroundClosestLestInterruptedAndSwitch>" +
                                "<TargetTypes/><StableTimeInDays><Minimum>2</Minimum></StableTimeInDays>" +
                                "</PatrolAroundClosestLestInterruptedAndSwitch>") +
                ReferencingSpawn("first", "all") + ReferencingSpawn("second", "all"));

            PriorityPartyBehaviourTemplateResolver.Resolve(document);

            BesiegeClosestSettlementConfig firstBesiege =
                (BesiegeClosestSettlementConfig)document.AllSpawns[0].PriorityPartyBehaviours![0];
            BesiegeClosestSettlementConfig secondBesiege =
                (BesiegeClosestSettlementConfig)document.AllSpawns[1].PriorityPartyBehaviours![0];
            RaidClosestVillageConfig firstRaid =
                (RaidClosestVillageConfig)document.AllSpawns[0].PriorityPartyBehaviours![1];
            RaidClosestVillageConfig secondRaid =
                (RaidClosestVillageConfig)document.AllSpawns[1].PriorityPartyBehaviours![1];
            PatrolAroundClosestLestInterruptedAndSwitchConfig firstPatrol =
                (PatrolAroundClosestLestInterruptedAndSwitchConfig)document.AllSpawns[0].PriorityPartyBehaviours![2];
            PatrolAroundClosestLestInterruptedAndSwitchConfig secondPatrol =
                (PatrolAroundClosestLestInterruptedAndSwitchConfig)document.AllSpawns[1].PriorityPartyBehaviours![2];

            Assert.Multiple(() =>
            {
                Assert.That(firstBesiege, Is.Not.SameAs(secondBesiege));
                Assert.That(firstBesiege.TargetTypes, Is.Not.SameAs(secondBesiege.TargetTypes));
                Assert.That(firstBesiege.StrengthAssessment, Is.Not.SameAs(secondBesiege.StrengthAssessment));
                Assert.That(firstBesiege.Reconnaissance, Is.Not.SameAs(secondBesiege.Reconnaissance));
                Assert.That(firstBesiege.Reconnaissance.RequiredClearTimeInDays,
                    Is.Not.SameAs(secondBesiege.Reconnaissance.RequiredClearTimeInDays));
                Assert.That(firstBesiege.OperationWindowInDays, Is.Not.SameAs(secondBesiege.OperationWindowInDays));
                Assert.That(firstBesiege.Recovery, Is.Not.SameAs(secondBesiege.Recovery));
                Assert.That(firstRaid, Is.Not.SameAs(secondRaid));
                Assert.That(firstRaid.StrengthAssessment, Is.Not.SameAs(secondRaid.StrengthAssessment));
                Assert.That(firstRaid.Reconnaissance, Is.Not.SameAs(secondRaid.Reconnaissance));
                Assert.That(firstRaid.Reconnaissance.RequiredClearTimeInDays,
                    Is.Not.SameAs(secondRaid.Reconnaissance.RequiredClearTimeInDays));
                Assert.That(firstRaid.OperationWindowInDays, Is.Not.SameAs(secondRaid.OperationWindowInDays));
                Assert.That(firstRaid.Recovery, Is.Not.SameAs(secondRaid.Recovery));
                Assert.That(firstPatrol, Is.Not.SameAs(secondPatrol));
                Assert.That(firstPatrol.TargetTypes, Is.Not.SameAs(secondPatrol.TargetTypes));
                Assert.That(firstPatrol.StableTimeInDays, Is.Not.SameAs(secondPatrol.StableTimeInDays));
                Assert.That(new[]
                {
                    secondBesiege.TargetTypesSpecified,
                    secondBesiege.StrengthAssessment.MinimumAttackerToTargetRatioSpecified,
                    secondBesiege.StrengthAssessment.MinimumAttackerToRoamingHostilesRatioSpecified,
                    secondBesiege.Reconnaissance.RequiredClearTimeInDays.MinimumSpecified,
                    secondBesiege.Reconnaissance.RequiredClearTimeInDays.MaximumSpecified,
                    secondBesiege.OperationWindowInDays.MinimumSpecified,
                    secondBesiege.OperationWindowInDays.MaximumSpecified,
                    secondBesiege.Recovery.GracePeriodInDaysSpecified,
                    secondBesiege.Recovery.MaximumFailedAttemptsSpecified
                }, Is.EqualTo(new[] { true, true, false, true, false, false, true, true, false }));
                Assert.That(new[]
                {
                    secondRaid.StrengthAssessment.MinimumAttackerToTargetRatioSpecified,
                    secondRaid.StrengthAssessment.MinimumAttackerToRoamingHostilesRatioSpecified,
                    secondRaid.Reconnaissance.RequiredClearTimeInDays.MinimumSpecified,
                    secondRaid.Reconnaissance.RequiredClearTimeInDays.MaximumSpecified,
                    secondRaid.OperationWindowInDays.MinimumSpecified,
                    secondRaid.OperationWindowInDays.MaximumSpecified,
                    secondRaid.Recovery.GracePeriodInDaysSpecified,
                    secondRaid.Recovery.MaximumFailedAttemptsSpecified
                }, Is.EqualTo(new[] { false, true, false, true, true, false, false, true }));
                Assert.That(new[]
                {
                    secondPatrol.TargetTypesSpecified,
                    secondPatrol.StableTimeInDays.MinimumSpecified,
                    secondPatrol.StableTimeInDays.MaximumSpecified
                }, Is.EqualTo(new[] { true, true, false }));
            });

            firstBesiege.TargetTypes!.Add(SpawnSettlementType.Castle);
            firstBesiege.StrengthAssessment.MinimumAttackerToTargetRatio = 9;
            firstBesiege.Reconnaissance.RequiredClearTimeInDays.Minimum = 9;
            firstBesiege.OperationWindowInDays.Maximum = 9;
            firstBesiege.Recovery.GracePeriodInDays = 9;
            firstRaid.StrengthAssessment.MinimumAttackerToRoamingHostilesRatio = 9;
            firstRaid.Reconnaissance.RequiredClearTimeInDays.Maximum = 9;
            firstRaid.OperationWindowInDays.Minimum = 9;
            firstRaid.Recovery.MaximumFailedAttempts = 9;
            firstPatrol.TargetTypes!.Add(SpawnSettlementType.Village);
            firstPatrol.StableTimeInDays.Minimum = 9;

            Assert.Multiple(() =>
            {
                Assert.That(secondBesiege.TargetTypes, Is.EqualTo(new[] { SpawnSettlementType.Town }));
                Assert.That(secondBesiege.StrengthAssessment.MinimumAttackerToTargetRatio, Is.EqualTo(2));
                Assert.That(secondBesiege.Reconnaissance.RequiredClearTimeInDays.Minimum, Is.EqualTo(1));
                Assert.That(secondBesiege.OperationWindowInDays.Maximum, Is.EqualTo(8));
                Assert.That(secondBesiege.Recovery.GracePeriodInDays, Is.EqualTo(3));
                Assert.That(secondRaid.StrengthAssessment.MinimumAttackerToRoamingHostilesRatio, Is.EqualTo(2));
                Assert.That(secondRaid.Reconnaissance.RequiredClearTimeInDays.Maximum, Is.EqualTo(2));
                Assert.That(secondRaid.OperationWindowInDays.Minimum, Is.EqualTo(4));
                Assert.That(secondRaid.Recovery.MaximumFailedAttempts, Is.EqualTo(4));
                Assert.That(secondPatrol.TargetTypes, Is.Empty);
                Assert.That(secondPatrol.StableTimeInDays.Minimum, Is.EqualTo(2));
            });
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
        public void SpawnDataReader_NilRootUsesReaderErrorPathOnce()
        {
            using MemoryStream stream = new(Encoding.UTF8.GetBytes(
                "<root xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xsi:nil=\"true\"/>"));
            List<System.Exception> captured = new();

            Spawns result = SpawnDataReader.DeserializeAndResolve(stream, error => captured.Add(error));

            Assert.Multiple(() =>
            {
                Assert.That(captured, Has.Count.EqualTo(1));
                Assert.That(captured[0], Is.TypeOf<InvalidOperationException>());
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
