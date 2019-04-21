using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Kafkaesque.Internals;
using Serilog;
// ReSharper disable InvertIf

namespace Kafkaesque
{
    class ThreadLogWriter : LogWriter
    {
        const string LineTerminator = "#";

        readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        readonly ConcurrentQueue<WriteTask> _buffer = new ConcurrentQueue<WriteTask>();
        readonly string _directoryPath;
        readonly Thread _workerThread;
        readonly ILogger _logger;

        StreamWriter _currentWriter;
        FileStream _lockFileHandle;

        bool _disposed;
        long _approxBytesWritten;

        internal ThreadLogWriter(string directoryPath, CancellationToken cancellationToken)
        {
            _directoryPath = directoryPath;
            _logger = Log.ForContext<ThreadLogWriter>().ForContext("dir", directoryPath);

            AcquireLockFile(directoryPath, cancellationToken);

            _workerThread = new Thread(Run) { IsBackground = true };
            _workerThread.Start();
        }

        public override Task WriteAsync(byte[] data, CancellationToken cancellationToken = default)
        {
            var writeTask = new WriteTask(data, cancellationToken);
            _buffer.Enqueue(writeTask);
            return writeTask.Task;
        }

        public override Task WriteManyAsync(IEnumerable<byte[]> dataSequence, CancellationToken cancellationToken = default)
        {
            var tasks = new List<Task>();

            foreach (var data in dataSequence)
            {
                var writeTask = new WriteTask(data, cancellationToken);
                _buffer.Enqueue(writeTask);
                tasks.Add(writeTask.Task);
            }

            return Task.WhenAll(tasks);
        }

        void Run()
        {
            var cancellationToken = _cancellationTokenSource.Token;

            _logger.Information("Starting writer worker loop");

            var dirSnap = new DirSnap(_directoryPath);

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        WriteBufferedTasks(dirSnap, cancellationToken);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        _logger.Verbose("Cancellation detected");
                        break;
                    }
                    catch (Exception exception)
                    {
                        _logger.Warning(exception, "Exception in worker loop");
                    }
                }

                _logger.Verbose("Exited inner worker loop");

                if (_buffer.Count > 0)
                {
                    _logger.Verbose("Emptying write task buffer");

                    WriteBufferedTasks(dirSnap, cancellationToken);
                }

                _logger.Verbose("Write task buffer empty");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // ok
            }
            catch (ThreadAbortException)
            {
                _logger.Warning("Worker thread abortion detected!");
            }
            catch (Exception exception)
            {
                _logger.Error(exception, "Unhandled exception, worker loop failed");
            }
            finally
            {
                _logger.Verbose("Cleaning up");

                _currentWriter?.Dispose();
                _currentWriter = null;

                _logger.Information("Writer worker loop stopped");
            }
        }

        void WriteBufferedTasks(DirSnap dirSnap, CancellationToken cancellationToken)
        {
            // todo: pre-allocate array of fixed size and use it to store dequeued write tasks
            var writeTasks = new List<WriteTask>(_buffer.Count);

            while (_buffer.TryDequeue(out var writeTask))
            {
                if (writeTask.IsCancelled) continue;

                writeTasks.Add(writeTask);
            }

            if (writeTasks.Count == 0)
            {
                Task.Delay(37, cancellationToken).Wait(cancellationToken);
                return;
            }

            try
            {
                WriteTasksAsync(writeTasks, dirSnap);

                writeTasks.ForEach(task => task.Complete());

                _logger.Verbose("Successfully wrote batch of {count} messages", writeTasks.Count);
            }
            catch (Exception exception)
            {
                writeTasks.ForEach(task => task.Fail(exception));
            }
        }

        void WriteTasksAsync(IEnumerable<WriteTask> writeTasks, DirSnap dirSnap)
        {
            if (_currentWriter == null)
            {
                string filePath;
                if (dirSnap.IsEmpty)
                {
                    filePath = dirSnap.GetFilePath(0);
                    dirSnap.RegisterFile(filePath);
                }
                else
                {
                    filePath = dirSnap.LastFile().FilePath;
                }
                var fileInfo = new FileInfo(filePath);

                _currentWriter = GetWriter(filePath);
                _approxBytesWritten = fileInfo.Exists ? fileInfo.Length : 0;
            }

            var flushNeeded = false;

            foreach (var task in writeTasks)
            {
                var data = task.Data;
                var line = Convert.ToBase64String(data);
                _currentWriter.Write(line);
                _currentWriter.WriteLine(LineTerminator);
                _approxBytesWritten += line.Length + 1;
                flushNeeded = true;

                if (_approxBytesWritten > FileSnap.ApproxMaxFileLength)
                {
                    _currentWriter.Flush();
                    flushNeeded = false;
                    _currentWriter.Dispose();

                    var nextFileNumber = dirSnap.LastFile().FileNumber + 1;
                    var nextFilePath = dirSnap.GetFilePath(nextFileNumber);
                    dirSnap.RegisterFile(nextFilePath);

                    _approxBytesWritten = 0;
                    _currentWriter = GetWriter(nextFilePath);
                }
            }

            if (flushNeeded)
            {
                _currentWriter.Flush();
            }
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

        StreamWriter GetWriter(string filePath)
        {
            var stream = File.Open(filePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);

            _logger.Verbose("Created new writer for file {filePath}", filePath);

            return new StreamWriter(stream, Encoding.UTF8);
        }

        public override void Dispose()
        {
            if (_disposed) return;

            try
            {
                using (_cancellationTokenSource)
                {
                    _logger.Verbose("Requesting cancellation");
                    _cancellationTokenSource.Cancel();

                    var timeout = TimeSpan.FromSeconds(3);

                    _logger.Verbose("Waiting for worker loop to exit");

                    if (!_workerThread.Join(timeout))
                    {
                        _logger.Warning("Worker loop did not finish working within {timeout} timeout", timeout);

                        _workerThread.Abort();
                    }
                }
            }
            finally
            {
                try
                {
                    if (_lockFileHandle != null)
                    {
                        _lockFileHandle.Dispose();
                        _logger.Verbose("Lock file released");
                    }
                }
                catch
                {
                }

                _disposed = true;
            }
        }
    }
}