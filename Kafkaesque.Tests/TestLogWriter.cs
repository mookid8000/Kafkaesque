using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Testy;
using Testy.Files;

namespace Kafkaesque.Tests
{
    [TestFixture]
    public class TestLogWriter : FixtureBase
    {
        [Test]
        public async Task CanWriteAndReadItBack()
        {
            var logDirectoryPath = GetLogDirectoryPath();
            var logDirectory = new LogDirectory(logDirectoryPath);

            await logDirectory.GetWriter().WriteAsync(new byte[] { 1, 2, 3 });

            var logEvents = logDirectory.GetReader().Read().ToList();

            Assert.That(logEvents.Count, Is.EqualTo(1));

            var logEvent = logEvents.First();

            Assert.That(logEvent.Data, Is.EqualTo(new byte[] { 1, 2, 3 }));
        }

        string GetLogDirectoryPath()
        {
            var tempDirectory = new TemporaryTestDirectory();

            Using(tempDirectory);

            return Path.Combine(tempDirectory, "log");
        }
    }
}