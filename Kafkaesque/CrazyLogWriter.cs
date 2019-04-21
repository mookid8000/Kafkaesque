using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Kafkaesque
{
    class CrazyLogWriter : LogWriter
    {
        readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        readonly Task _task;

        public CrazyLogWriter(string directoryPath, CancellationToken cancellationToken)
        {
            _task = Task.Run(async () => await Run());
        }

        public override Task WriteAsync(byte[] data, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public override Task WriteManyAsync(IEnumerable<byte[]> dataSequence, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        async Task Run()
        {
            var token = _cancellationTokenSource.Token;

            try
            {
                Console.WriteLine("Working");
                if (token.IsCancellationRequested)
                {
                    Console.WriteLine("CR before start");
                }
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        Console.WriteLine(".");
                        await Task.Delay(TimeSpan.FromSeconds(1), token);
                    }
                    catch (OperationCanceledException) when (token.IsCancellationRequested)
                    {
                        // it's alright
                        Console.WriteLine("OCE (inner)");
                    }
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                // we're exiting
                Console.WriteLine("OCE (outer)");
            }
            finally
            {
                Console.WriteLine("DONE!");
            }
        }

        public override void Dispose()
        {
            using (_cancellationTokenSource)
            {
                _cancellationTokenSource.Cancel();

                if (!_task.Wait(TimeSpan.FromSeconds(3)))
                {
                    Console.WriteLine("WTF?!?!? Task did not finish working withing 3 s timeout!");
                }
            }
        }
    }
}