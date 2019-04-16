using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Testy;
using Testy.Files;
// ReSharper disable ObjectCreationAsStatement
// ReSharper disable UnusedVariable

namespace Kafkaesque.Tests
{
    [TestFixture]
    public class TestLogDirectory : FixtureBase
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

            var result = reader.Read();

            Assert.That(result.Count(), Is.EqualTo(0));
        }

        [Test]
        public void CannotCreateMoreThanOneWriter()
        {
            var logDirectoryPath = GetLogDirectoryPath();
            var log = new LogDirectory(logDirectoryPath);
            var writer = log.GetWriter();

            Assert.Throws<InvalidOperationException>(() => log.GetWriter());
        }

        string GetLogDirectoryPath()
        {
            var tempDirectory = new TemporaryTestDirectory();

            Using(tempDirectory);

            return Path.Combine(tempDirectory, "log");
        }
    }
}
