using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

namespace Kafkaesque
{
    class CrazyLogWriter : LogWriter
    {
        readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        readonly ConcurrentQueue<WriteTask> _buffer = new ConcurrentQueue<WriteTask>();
        readonly ILogger _logger;
        readonly Task _task;

        public CrazyLogWriter(string directoryPath, CancellationToken cancellationToken)
        {
            _logger = Log.ForContext<CrazyLogWriter>().ForContext("dir", directoryPath);
            _task = Task.Run(async () => await Run());
        }

        public override Task WriteAsync(byte[] data, CancellationToken cancellationToken = default)
        {
            var writeTask = new WriteTask(data, cancellationToken);
            _buffer.Enqueue(writeTask);
            return writeTask.Task;
        }

        public override Task WriteManyAsync(IEnumerable<byte[]> dataSequence, CancellationToken cancellationToken = default)
        {
            return Task.WhenAll(dataSequence.Select(seq => WriteAsync(seq, cancellationToken)));

            //return Task.CompletedTask;
            var tasks = new List<Task>();

            foreach (var data in dataSequence)
            {
                var writeTask = new WriteTask(data, cancellationToken);
                _buffer.Enqueue(writeTask);
                tasks.Add(writeTask.Task);
            }

            return Task.WhenAll(tasks);
        }

        async Task Run()
        {
            var token = _cancellationTokenSource.Token;

            try
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        while (_buffer.TryDequeue(out var writeTask))
                        {
                            _logger.Verbose("Completing a task");
                            writeTask.Complete();
                        }

                        token.ThrowIfCancellationRequested();

                        await Task.Delay(TimeSpan.FromSeconds(1), token);
                    }
                    catch (OperationCanceledException) when (token.IsCancellationRequested)
                    {
                        // it's alright
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
            _logger.Information("Stopping crazy log writer");
            using (_cancellationTokenSource)
            {
                _cancellationTokenSource.Cancel();

                if (!_task.Wait(TimeSpan.FromSeconds(3)))
                {
                    _logger.Warning("WTF?!?!? Task did not finish working withing 3 s timeout!");
                }
            }
        }
    }
}