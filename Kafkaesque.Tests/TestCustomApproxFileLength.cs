using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Kafkaesque.Tests.Extensions;
using NUnit.Framework;

namespace Kafkaesque.Tests
{
    [TestFixture]
    public class TestCustomApproxFileLength : KafkaesqueFixtureBase
    {
        [TestCase(2048)]
        [TestCase(8192)]
        public async Task CanCreateFilesOfDifferentSize(int approxFileLength)
        {
            var logDirectoryPath = GetLogDirectoryPath();
            var logDirectory = new LogDirectory(logDirectoryPath, new Settings(approximateMaximumFileLength: approxFileLength));

            using (var writer = logDirectory.GetWriter())
            {
                var data = Enumerable.Range(0, 1000)
                    .Select(n => $"THIS IS LINE NUMBER {n} OUT OF A LOT")
                    .Select(Encoding.UTF8.GetBytes);

                await writer.WriteManyAsync(data);
            }

            var directory = new DirectoryInfo(logDirectoryPath);

            directory.DumpDirectoryContentsToConsole();

            var dataFiles = directory.GetFiles("*.dat").OrderBy(f => f.FullName);

            foreach (var dataFile in dataFiles)
            {
                Assert.That(dataFile.Length, Is.LessThan(1.1 * approxFileLength));
            }
        }
    }
}