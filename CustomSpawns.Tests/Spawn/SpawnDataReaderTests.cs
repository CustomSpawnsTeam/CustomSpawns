using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using CustomSpawns.Data.Model;
using CustomSpawns.Data.Reader.Impl;
using NUnit.Framework;

namespace CustomSpawns.Tests.Spawn
{
    public class SpawnDataReaderTests
    {
        [Test]
        public void DeserializeAndResolve_MalformedXmlUsesReaderErrorPath()
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
        public void DeserializeAndResolve_NilRootUsesReaderErrorPathOnce()
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
    }
}
