using System;
using System.Collections.Concurrent;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Kafkaesque.Tests.Extensions;
using NUnit.Framework;
// ReSharper disable ArgumentsStyleAnonymousFunction

namespace Kafkaesque.Tests
{
    [TestFixture]
    public class SlowWriterTest : KafkaesqueFixtureBase
    {
        [Test]
        public async Task CheckBehaviorWhenWriterIsSlow()
        {
            var logDirectoryPath = GetLogDirectoryPath();
            var logDirectory = new LogDirectory(logDirectoryPath);

            var writer = Using(logDirectory.GetWriter());

            var readEvents = new ConcurrentQueue<string>();

            ThreadPool.QueueUserWorkItem(_ =>
            {
                var cancellationToken = CancelOnDisposal();

                try
                {
                    var reader = logDirectory.GetReader();

                    foreach (var evt in reader.Read(cancellationToken: cancellationToken, throwWhenCancelled: true))
                    {
                        var text = Encoding.UTF8.GetString(evt.Data);
                        Console.WriteLine($"Reader loop read text: {text}");
                        readEvents.Enqueue(text);
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    Console.WriteLine("Reader loop exited");
                }
            });

            async Task Write(string text)
            {
                Console.WriteLine($"Writing text: {text}");
                await writer.WriteAsync(Encoding.UTF8.GetBytes(text));
            }

            await Task.Run(async () =>
            {
                await Write("HEJ");
                await Task.Delay(TimeSpan.FromSeconds(1));
                await Write("MED");
                await Task.Delay(TimeSpan.FromSeconds(1));
                await Write("DIG");
                await Task.Delay(TimeSpan.FromSeconds(1));
                await Write("MIN");
                await Task.Delay(TimeSpan.FromSeconds(1));
                await Write("VÆÆÆÆN");
            });

            await readEvents.WaitFor(q => q.Count == 5, invariantExpression: q => q.Count >= 0 && q.Count <= 5, timeoutSeconds: 50);
        }
    }
}