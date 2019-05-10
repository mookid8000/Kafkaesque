using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
// ReSharper disable ArgumentsStyleLiteral

// ReSharper disable ObjectCreationAsStatement
// ReSharper disable UnusedVariable

namespace Kafkaesque.Tests
{
    [TestFixture]
    public class TestLogDirectory : KafkaesqueFixtureBase
    {
        [Test]
        public void AutomaticallyCreatesDirectory()
        {
            var logDirectoryPath = GetLogDirectoryPath();

            new LogDirectory(logDirectoryPath);

            Assert.That(Directory.Exists(logDirectoryPath),
                $"Did not find the directory '{logDirectoryPath}' as expected");
        }

        [Test]
        public void StartsOutEmpty()
        {
            var logDirectoryPath = GetLogDirectoryPath();
            var log = new LogDirectory(logDirectoryPath);
            var reader = log.GetReader();

            var result = reader.Read(cancellationToken: CancelAfter(TimeSpan.FromSeconds(3))).ToList();

            Assert.That(result.Count(), Is.EqualTo(0));
        }

        [Test]
        public void CannotCreateMoreThanOneWriter()
        {
            var logDirectoryPath = GetLogDirectoryPath();
            var log = new LogDirectory(logDirectoryPath, new Settings(writeLockAcquisitionTimeoutSeconds: 3));
            var writer = log.GetWriter();

            Using(writer);

            var exception = Assert.Throws<TimeoutException>(() => log.GetWriter());

            Console.WriteLine(exception);
        }
    }
}
