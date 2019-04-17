﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Kafkaesque.Internals;
using Serilog;

namespace Kafkaesque
{
    public class LogWriter : IDisposable
    {
        const string LineTerminator = "#";

        readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        readonly ConcurrentQueue<WriteTask> _buffer = new ConcurrentQueue<WriteTask>();
        readonly ManualResetEvent _workerLoopExited = new ManualResetEvent(false);
        readonly ILogger _logger;

        StreamWriter _currentWriter;
        FileStream _lockFileHandle;
        bool _disposed;

        internal LogWriter(string directoryPath, CancellationToken cancellationToken)
        {
            _logger = Log.ForContext<LogWriter>().ForContext("dir", directoryPath);

            AcquireLockFile(directoryPath, cancellationToken);

            Task.Run(async () => await Run(directoryPath));
        }

        void AcquireLockFile(string directoryPath, CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();
            var lockFilePath = Path.Combine(directoryPath, "kafkaesque.lockfile");
            var timeout = TimeSpan.FromSeconds(20);
            var lockFileTimeoutMessageLogged = false;

            _logger.Verbose("Acquiring lock file {lockFilePath}", lockFilePath);

            while (stopwatch.Elapsed < timeout)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    _lockFileHandle = File.Open(lockFilePath, FileMode.OpenOrCreate);
                    _logger.Verbose("Lock file successfully acquired");
                    return;
                }
                catch
                {
                    Thread.Sleep(200);

                    if (stopwatch.Elapsed <= TimeSpan.FromSeconds(1) || lockFileTimeoutMessageLogged) continue;

                    _logger.Verbose("Will wait up to {lockFileTimeout} for lock file to be acquired", timeout);
                    
                    lockFileTimeoutMessageLogged = true;
                }
            }

            throw new IOException($"Could not acquire lock file '{lockFilePath}' within {timeout} timeout");
        }

        public Task WriteAsync(byte[] bytes, CancellationToken cancellationToken = default(CancellationToken))
        {
            var writeTask = new WriteTask(bytes, cancellationToken);
            _buffer.Enqueue(writeTask);
            return writeTask.Task;
        }

        async Task DoRun(DirSnap dirSnap)
        {
            if (_buffer.Count == 0)
            {
                await Task.Delay(100);
                return;
            }

            var writeTasks = new List<WriteTask>(_buffer.Count);

            while (_buffer.TryDequeue(out var writeTask))
            {
                writeTasks.Add(writeTask);
            }

            try
            {
                await WriteAsync(writeTasks, dirSnap);

                writeTasks.ForEach(task => task.Complete());
            }
            catch (Exception exception)
            {
                writeTasks.ForEach(task => task.Fail(exception));
            }
        }

        async Task WriteAsync(List<WriteTask> writeTask, DirSnap dirSnap)
        {
            string GetCurrentFilePath()
            {
                if (dirSnap.IsEmpty) return dirSnap.GetFilePath(0);

                var lastFile = dirSnap.LastFile();

                return lastFile.IsTooBig
                    ? dirSnap.GetFilePath(lastFile.FileNumber + 1)
                    : lastFile.FilePath;
            }

            var writer = _currentWriter ?? (_currentWriter = GetWriter(GetCurrentFilePath()));

            foreach (var task in writeTask)
            {
                writer.WriteLine(string.Concat(Convert.ToBase64String(task.Data), LineTerminator));
            }

            await writer.FlushAsync();
        }

        static StreamWriter GetWriter(string filePath)
        {
            var stream = File.Open(filePath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite);

            return new StreamWriter(stream, Encoding.UTF8);
        }

        async Task Run(string directoryPath)
        {
            var cancellationToken = _cancellationTokenSource.Token;

            _logger.Information("Starting writer worker loop");

            var dirSnap = new DirSnap(directoryPath);

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        await DoRun(dirSnap);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        // ok
                    }
                    catch (Exception exception)
                    {
                        _logger.Warning(exception, "Exception in worker loop");
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // ok
            }
            catch (Exception exception)
            {
                _logger.Error(exception, "Unhandled exception, worker loop failed");
            }
            finally
            {
                _currentWriter?.Dispose();

                _logger.Information("Writer worker loop stopped");

                _workerLoopExited.Set();
            }
        }

        public void Dispose()
        {
            if (_disposed) return;

            try
            {
                using (_lockFileHandle)
                using (_cancellationTokenSource)
                using (_workerLoopExited)
                {
                    _cancellationTokenSource.Cancel();

                    if (!_workerLoopExited.WaitOne(TimeSpan.FromSeconds(3)))
                    {
                        _logger.Warning("Worker loop did not finish working within 3 s timeout!");
                    }
                }
            }
            finally
            {
                _disposed = true;
            }
        }
    }
}