using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Kafkaesque.Tests.Extensions;
using NUnit.Framework;
using Serilog.Events;

namespace Kafkaesque.Tests
{
    [TestFixture]
    public class TestLongHistory : KafkaesqueFixtureBase
    {
        [TestCase(10)]
        [TestCase(100000)]
        public async Task CanReadBackEventsSpreadOverMultipleFiles(int count)
        {
            SetLogLevel(LogEventLevel.Verbose);

            var messages = Enumerable.Range(0, count)
                .Select(n => $"{n}/This is a pretty long string message, whose purpose is solely to take up a lot of space, meaning that the events will eventually need to be placed in more than one file.");

            var logDirectoryPath = GetLogDirectoryPath();
            var directoryInfo = new DirectoryInfo(logDirectoryPath);
            var logDirectory = new LogDirectory(directoryInfo);

            var writer = logDirectory.GetWriter();

            Using(writer);

            await writer.WriteManyAsync(messages.Select(Encoding.UTF8.GetBytes));

            directoryInfo.DumpDirectoryContentsToConsole();

            var reader = logDirectory.GetReader();

            var expectedMessageNumber = 0;

            while (expectedMessageNumber < count)
            {
                var fileNumber = -1;
                var bytePosition = -1;

                foreach (var message in reader.Read(fileNumber: fileNumber, bytePosition: bytePosition))
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
                        throw new AssertionException($"Error processing event with fileNumber = {message.FileNumber}, bytePosition = {message.BytePosition}", exception);
                    }

                    expectedMessageNumber++;
                    fileNumber = message.FileNumber;
                    bytePosition = message.BytePosition;

                    if (expectedMessageNumber == count)
                    {
                        Console.WriteLine($"Reached {expectedMessageNumber}, which was the expected number!");
                        break;
                    }
                }
            }
        }
    }
}