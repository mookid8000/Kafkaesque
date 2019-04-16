using System.IO;
using NUnit.Framework;
using Testy;
using Testy.Files;

namespace Kafkaesque.Tests
{
    [TestFixture]
    public class TestLogDirectory : FixtureBase
    {
        [Test]
        public void AutomaticallyCreatesDirectory()
        {
            var tempDirectory = new TemporaryTestDirectory();

            Using(tempDirectory);

            var logDirectoryPath = Path.Combine(tempDirectory, "log");

            var log = new LogDirectory(logDirectoryPath);

            Assert.That(Directory.Exists(logDirectoryPath), 
                $"Did not find the directory '{logDirectoryPath}' as expected");
        }
    }
}
