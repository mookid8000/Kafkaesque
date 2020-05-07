using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Kafkaesque.Tests
{
    [TestFixture]
    public class TestReadEof : KafkaesqueFixtureBase
    {
        [Test]
        public async Task ReadingInitiallyReturnsPrettyQuickly()
        {
            var logDirectoryPath = GetLogDirectoryPath();

            var log = new LogDirectory(logDirectoryPath);
            var reader = log.GetReader();

            var emptyList = reader.ReadEof()
                .TakeWhile(e => e != LogReader.EOF)
                .ToList();

            Assert.That(emptyList.Count, Is.EqualTo(0));
        }

        [Test]
        public async Task CanReadSomeEvents()
        {
            var logDirectoryPath = GetLogDirectoryPath();

            var log = new LogDirectory(logDirectoryPath);
            var reader = log.GetReader();

            using var writer = log.GetWriter();

            await writer.WriteAsync(new byte[] {1, 2, 3});
            await writer.WriteAsync(new byte[] {1, 2, 3});
            await writer.WriteAsync(new byte[] {1, 2, 3});

            var list = reader.ReadEof().TakeWhile(e => e != LogReader.EOF).ToList();

            Assert.That(list.Count, Is.EqualTo(3));
        }

        [Test]
        public async Task CanReadAndResumeAfterExperiencingEof()
        {
            var logDirectoryPath = GetLogDirectoryPath();

            var log = new LogDirectory(logDirectoryPath);
            var reader = log.GetReader();

            using var writer = log.GetWriter();

            await writer.WriteAsync(new byte[] {1, 2, 3});
            await writer.WriteAsync(new byte[] {1, 2, 3});
            await writer.WriteAsync(new byte[] {1, 2, 3});

            var firstList = reader.ReadEof().TakeWhile(e => e != LogReader.EOF).Cast<LogEvent>().ToList();

            await writer.WriteAsync(new byte[] {1, 2, 3});
            await writer.WriteAsync(new byte[] {1, 2, 3});
            await writer.WriteAsync(new byte[] {1, 2, 3});
            await writer.WriteAsync(new byte[] {1, 2, 3});
            await writer.WriteAsync(new byte[] {1, 2, 3});

            var (fileNumber, bytePosition) = (firstList.Last().FileNumber, firstList.Last().BytePosition);

            var secondList = reader.ReadEof(fileNumber,bytePosition)
                .TakeWhile(e => e != LogReader.EOF)
                .ToList();

            Assert.That(firstList.Count, Is.EqualTo(3));
            Assert.That(secondList.Count, Is.EqualTo(5));
        }
    }
}