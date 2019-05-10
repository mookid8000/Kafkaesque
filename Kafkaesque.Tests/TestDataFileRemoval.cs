using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Kafkaesque.Tests.Extensions;
using NUnit.Framework;

namespace Kafkaesque.Tests
{
    [TestFixture]
    public class TestDataFileRemoval : KafkaesqueFixtureBase
    {
        [Test]
        public async Task CanDeleteOldFiles()
        {
            var directoryInfo = new DirectoryInfo(GetLogDirectoryPath());
            var settings = new Settings(numberOfFilesToKeep: 10, approximateMaximumFileLength: 32768);
            var logDirectory = new LogDirectory(directoryInfo, settings);

            using (var writer = logDirectory.GetWriter())
            {
                var data = Enumerable.Range(0, 10000)
                    .Select(n => $"THIS IS LINE NUMBER {n} OUT OF QUITE A FEW")
                    .Select(Encoding.UTF8.GetBytes);

                await writer.WriteManyAsync(data);
            }

            directoryInfo.DumpDirectoryContentsToConsole();

            var dataFiles = directoryInfo.GetFiles("*.dat").ToList();

            Assert.That(dataFiles.Count, Is.EqualTo(10));
        }
    }
}