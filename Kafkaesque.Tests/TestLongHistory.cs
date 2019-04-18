using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using Serilog.Events;

namespace Kafkaesque.Tests
{
    [TestFixture]
    public class TestLongHistory : KafkaesqueFixtureBase
    {
        [TestCase(100)]
        public async Task CanReadBackEventsSpreadOverMultipleFiles(int count)
        {
            SetLogLevel(LogEventLevel.Verbose);

            var messages = Enumerable.Range(0, count).Select(n => $"THIS IS A STRING MESSAGE EVENT/{n}").ToList();

            var logDirectory = new LogDirectory(GetLogDirectoryPath());

            using (var writer = logDirectory.GetWriter())
            {
                foreach (var message in messages)
                {
                    await writer.WriteAsync(Encoding.UTF8.GetBytes(message));
                }
            }
        }
    }
}