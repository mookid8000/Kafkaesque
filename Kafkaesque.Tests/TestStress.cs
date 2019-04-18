using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Kafkaesque.Tests.Extensions;
using NUnit.Framework;
using Serilog.Events;
// ReSharper disable ArgumentsStyleAnonymousFunction

namespace Kafkaesque.Tests
{
    [TestFixture]
    public class TestStress : KafkaesqueFixtureBase
    {
        [TestCase(1, 1)]
        public async Task ItWorks(int count, int readerCount)
        {
            SetLogLevel(LogEventLevel.Information);

            var logDirectoryPath = GetLogDirectoryPath();
            var logDirectory = new LogDirectory(logDirectoryPath);

            var originalMessages = Enumerable.Range(0, count).Select(n => $"THIS IS A STRING MESSAGE/{n}").ToList();
            var messagesToWrite = new ConcurrentQueue<string>(originalMessages);

            var cancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = cancellationTokenSource.Token;

            var writer = logDirectory.GetWriter();

            Using(writer);

            var readerThreads = Enumerable.Range(0, readerCount)
                .Select(r =>
                {
                    var logReader = logDirectory.GetReader();
                    var readMessages = new ConcurrentQueue<string>();

                    return new
                    {
                        Thread = new Thread(() =>
                        {
                            var fileNumber = -1;
                            var bytePosition = -1;

                            while (!cancellationToken.IsCancellationRequested)
                            {
                                foreach (var logEvent in logReader.Read(fileNumber, bytePosition))
                                {
                                    readMessages.Enqueue(Encoding.UTF8.GetString(logEvent.Data));
                                    fileNumber = logEvent.FileNumber;
                                    bytePosition = logEvent.BytePosition;
                                }
                            }
                        }),

                        Messages = readMessages
                    };
                })
                .ToList();

            readerThreads.ForEach(a => a.Thread.Start());

            while (messagesToWrite.TryDequeue(out var message))
            {
                await writer.WriteAsync(Encoding.UTF8.GetBytes(message), cancellationToken);
            }

            await Task.WhenAll(readerThreads.Select(async r =>
            {
                await r.Messages.WaitFor(q => q.Count == count, invariantExpression: q => q.Count <= count);
            }));
        }
    }
}