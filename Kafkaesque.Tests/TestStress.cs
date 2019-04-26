using System;
using System.Collections.Concurrent;
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
        [TestCase(10, 1)]
        [TestCase(100, 1)]
        [TestCase(1000, 1)]
        [TestCase(10000, 1)]
        [TestCase(100000, 1)]
        [TestCase(1000000, 1)]
        [TestCase(1, 2)]
        [TestCase(10, 2)]
        [TestCase(100, 2)]
        [TestCase(1000, 2)]
        [TestCase(10000, 2)]
        [TestCase(100000, 2)]
        [TestCase(1000000, 2)]
        [TestCase(1000000, 3)]
        [TestCase(1000000, 4)]
        [TestCase(1000000, 5)]
        [TestCase(1000000, 6)]
        [TestCase(1000000, 7)]
        [TestCase(1000000, 8)]
        [TestCase(1000000, 9)]
        [TestCase(1000000, 10)]
        [TestCase(1000000, 20)]
        [TestCase(1000000, 30)]
        [TestCase(1000000, 40)]
        [TestCase(1000000, 50)]
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
                .Select(n =>
                {
                    var logReader = logDirectory.GetReader();
                    var readMessages = new ConcurrentQueue<string>();

                    return new
                    {
                        Thread = new Thread(() =>
                        {
                            try
                            {
                                foreach (var logEvent in logReader.Read(cancellationToken: cancellationToken))
                                {
                                    readMessages.Enqueue(Encoding.UTF8.GetString(logEvent.Data));
                                }
                            }
                            catch (Exception exception)
                            {
                                Console.WriteLine(exception);
                            }
                        })
                        {
                            IsBackground = true,
                            Name = $"Reader thread {n}"
                        },

                        Messages = readMessages
                    };
                })
                .ToList();

            readerThreads.ForEach(a => a.Thread.Start());

            var writerThreads = Enumerable.Range(0, 10)
                .Select(n => new Thread(() =>
                {
                    while (messagesToWrite.TryDequeue(out var message))
                    {
                        writer.WriteAsync(Encoding.UTF8.GetBytes(message), cancellationToken);
                    }
                })
                {
                    Name = $"Writer thread {n}"
                })
                .ToList();

            writerThreads.ForEach(t => t.Start());

            //await writer.WriteManyAsync(messagesToWrite.Select(Encoding.UTF8.GetBytes), cancellationToken);

            try
            {
                await Task.WhenAll(readerThreads.Select(async r =>
                {
                    try
                    {
                        await r.Messages.WaitFor(
                            completionExpression: q => q.Count == count,
                            invariantExpression: q => q.Count <= count,
                            timeoutSeconds: 45
                        );
                    }
                    catch (Exception exception)
                    {
                        throw new ApplicationException(
                            $"Completion criteria not satisfied for thread with name '{r.Thread.Name}'", exception);
                    }
                }));
            }
            catch
            {
                cancellationTokenSource.Cancel();
                throw;
            }

            writerThreads.ForEach(t =>
            {
                if (t.Join(TimeSpan.FromSeconds(2))) return;

                throw new TimeoutException($"Writer thread named '{t.Name}' did not finish writing withing 20 s timeout");
            });
        }
    }
}