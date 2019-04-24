using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Kafkaesque.Tests.Extensions;
using NUnit.Framework;
using Serilog.Events;
// ReSharper disable ArgumentsStyleNamedExpression
// ReSharper disable ArgumentsStyleOther

namespace Kafkaesque.Tests
{
    [TestFixture]
    public class TestLongHistory : KafkaesqueFixtureBase
    {
        [TestCase(10)]
        [TestCase(100)]
        [TestCase(1000)]
        [TestCase(10000)]
        [TestCase(100000)]
        [TestCase(1000000)]
        public async Task CanReadBackEventsSpreadOverMultipleFiles_WritingEverythingInAdvance(int count)
        {
            SetLogLevel(LogEventLevel.Verbose);

            var messages = Enumerable.Range(0, count)
                .Select(n => $"{n}/This is a pretty long string message, whose purpose is solely to take up a lot of space, meaning that the events will eventually need to be placed in more than one file.");

            var logDirectoryPath = GetLogDirectoryPath();
            var directoryInfo = new DirectoryInfo(logDirectoryPath);
            var logDirectory = new LogDirectory(directoryInfo);

            // write everything
            var writer = logDirectory.GetWriter();
            Using(writer);
            await writer.WriteManyAsync(messages.Select(Encoding.UTF8.GetBytes));

            directoryInfo.DumpDirectoryContentsToConsole();

            // read it back
            var reader = logDirectory.GetReader();

            var expectedMessageNumber = 0;

            foreach (var message in reader.Read(cancellationToken: CancelAfter(TimeSpan.FromSeconds(10))).Take(count))
            {
                var text = Encoding.UTF8.GetString(message.Data);
                var parts = text.Split('/');

                try
                {
                    if (parts.Length != 2)
                    {
                        throw new FormatException(
                            $"The text '{text}' could not be parsed - expected a number and a slash, followed by some text");
                    }

                    if (!int.TryParse(parts.First(), out var actualMessageNumber))
                    {
                        throw new FormatException(
                            $"Could not parse the token '{parts.First()}' from the message '{text}' into an integer");
                    }

                    if (actualMessageNumber != expectedMessageNumber)
                    {
                        throw new AssertionException(
                            $"The message number {actualMessageNumber} did not match the expected: {expectedMessageNumber}");
                    }

                }
                catch (Exception exception)
                {
                    throw new ApplicationException($"Error processing event with fileNumber = {message.FileNumber}, bytePosition = {message.BytePosition}", exception);
                }

                expectedMessageNumber++;
            }

            Assert.That(expectedMessageNumber, Is.EqualTo(count));
        }

        [TestCase(10)]
        [TestCase(100)]
        [TestCase(1000)]
        [TestCase(10000)]
        [TestCase(100000)]
        [TestCase(1000000)]
        public async Task CanReadBackEventsSpreadOverMultipleFiles_ReadingWhileWriting(int count)
        {
            SetLogLevel(LogEventLevel.Verbose);

            var messages = Enumerable.Range(0, count)
                .Select(n => $"{n}/This is a pretty long string message, whose purpose is solely to take up a lot of space, meaning that the events will eventually need to be placed in more than one file.");

            var logDirectoryPath = GetLogDirectoryPath();
            var directoryInfo = new DirectoryInfo(logDirectoryPath);
            var logDirectory = new LogDirectory(directoryInfo);

            var writer = logDirectory.GetWriter();

            Using(writer);

            ThreadPool.QueueUserWorkItem(async _ =>
            {
                await writer.WriteManyAsync(messages.Select(Encoding.UTF8.GetBytes));

                directoryInfo.DumpDirectoryContentsToConsole();
            });

            var reader = logDirectory.GetReader();

            var expectedMessageNumber = 0;

            foreach (var message in reader.Read(cancellationToken: CancelAfter(TimeSpan.FromSeconds(10))).Take(count))
            {
                var text = Encoding.UTF8.GetString(message.Data);
                var parts = text.Split('/');

                try
                {
                    if (parts.Length != 2)
                    {
                        throw new FormatException(
                            $"The text '{text}' could not be parsed - expected a number and a slash, followed by some text");
                    }

                    if (!int.TryParse(parts.First(), out var actualMessageNumber))
                    {
                        throw new FormatException(
                            $"Could not parse the token '{parts.First()}' from the message '{text}' into an integer");
                    }

                    if (actualMessageNumber != expectedMessageNumber)
                    {
                        throw new AssertionException(
                            $"The message number {actualMessageNumber} did not match the expected: {expectedMessageNumber}");
                    }

                }
                catch (Exception exception)
                {
                    throw new ApplicationException($"Error processing event with fileNumber = {message.FileNumber}, bytePosition = {message.BytePosition}", exception);
                }

                expectedMessageNumber++;
            }

            Assert.That(expectedMessageNumber, Is.EqualTo(count));
        }
    }
}