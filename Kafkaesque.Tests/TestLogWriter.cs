using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Testy.Files;

namespace Kafkaesque.Tests
{
    [TestFixture]
    public class TestLogWriter : KafkaesqueFixtureBase
    {
        [Test]
        public async Task CanWriteAndReadItBack()
        {
            var logDirectoryPath = GetLogDirectoryPath();
            var logDirectory = new LogDirectory(logDirectoryPath);

            var logWriter = logDirectory.GetWriter();

            Using(logWriter);

            await logWriter.WriteAsync(new byte[] { 1, 2, 3 }, CancelAfter(TimeSpan.FromSeconds(3)));

            var reader = logDirectory.GetReader();

            Using(reader);

            var logEvents = reader.Read().ToList();

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