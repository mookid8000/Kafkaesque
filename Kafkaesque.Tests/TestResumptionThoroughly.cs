using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Kafkaesque.Tests.Extensions;
using NUnit.Framework;

namespace Kafkaesque.Tests
{
    [TestFixture]
    public class TestResumptionThoroughly : KafkaesqueFixtureBase
    {
        [TestCase(10)]
        [TestCase(100)]
        [TestCase(1000)]
        [TestCase(100000)]
        public async Task CheckResumption(int count)
        {
            var directoryPath = GetLogDirectoryPath();
            var logDirectory = new LogDirectory(directoryPath);

            using (var writer = logDirectory.GetWriter())
            {
                var data = Enumerable.Range(0, count)
                    .Select(n => $"THIS IS LINE NUMBER {n}")
                    .Select(Encoding.UTF8.GetBytes);

                await writer.WriteManyAsync(data);
            }

            new DirectoryInfo(directoryPath).DumpDirectoryContentsToConsole();

            // read events in a very inefficient way, checking that we can resume at each single line
            var fileNumber = -1;
            var bytePosition = -1;

            for (var counter = 0; counter < count; counter++)
            {
                var expectedText = $"THIS IS LINE NUMBER {counter}";
                var eventData = logDirectory.GetReader().Read(fileNumber, bytePosition).FirstOrDefault();

                var actualText = Encoding.UTF8.GetString(eventData.Data);

                Assert.That(actualText, Is.EqualTo(expectedText));

                fileNumber = eventData.FileNumber;
                bytePosition = eventData.BytePosition;
            }
        }
    }
}