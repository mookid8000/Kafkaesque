using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Kafkaesque.Internals;
// ReSharper disable InvertIf
// ReSharper disable MethodSupportsCancellation
// ReSharper disable ArgumentsStyleLiteral
#pragma warning disable 1998
#pragma warning disable 4014

namespace Kafkaesque
{
    /// <summary>
    /// Log writer for writing logs :)
    /// </summary>
    public class LogWriter : IDisposable
    {
        const string LineTerminator = "#";

        readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        readonly ConcurrentQueue<WriteTask> _buffer = new ConcurrentQueue<WriteTask>();
        readonly string _directoryPath;
        readonly Settings _settings;
        readonly Task _workerTask;
        readonly Task _cleanerTask;
        readonly ILogger _logger;

        StreamWriter _currentWriter;
        FileStream _lockFileHandle;

        bool _disposed;
        long _approxBytesWritten;

        internal LogWriter(string directoryPath, CancellationToken cancellationToken, Settings settings)
        {
            _directoryPath = directoryPath ?? throw new ArgumentNullException(nameof(directoryPath));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _logger = settings.Logger;

            AcquireLockFile(directoryPath, cancellationToken, settings.WriteLockAcquisitionTimeoutSeconds);

            _workerTask = Task.Run(RunWorker);
            _cleanerTask = Task.Run(RunCleaner);
        }

        /// <summary>
        /// Enqueues a write operation for the given <paramref name="data"/>, returning a <see cref="Task"/> for its completion.
        /// </summary>
        public Task WriteAsync(byte[] data, CancellationToken cancellationToken = default)
        {
            EnsureIsNotDisposing();

            var writeTask = new WriteTask(new[] { data }, cancellationToken);
            _buffer.Enqueue(writeTask);
            return writeTask.Task;
        }

        /// <summary>
        /// Enqueues write operations for the given <paramref name="dataSequence"/>, returning a <see cref="Task"/> for its completion.
        /// </summary>
        public Task WriteManyAsync(IEnumerable<byte[]> dataSequence, CancellationToken cancellationToken = default)
        {
            EnsureIsNotDisposing();

            var tasks = new List<Task>();

            foreach (var batch in dataSequence.Batch(100))
            {
                var writeTask = new WriteTask(batch.ToArray(), cancellationToken);
                _buffer.Enqueue(writeTask);
                tasks.Add(writeTask.Task);
            }

            return Task.WhenAll(tasks);
        }

        void EnsureIsNotDisposing()
        {
            if (!_cancellationTokenSource.IsCancellationRequested) return;

            var message = _disposed
                ? "Cannot write anymore, because the log writer is disposed"
                : "Cannot write anymore, because the log writer is in the process of being disposed";

            throw new InvalidOperationException(message);
        }

        async Task RunCleaner()
        {
            // skip this for now
            return;
            //var cancellationToken = _cancellationTokenSource.Token;

            //try
            //{
            //    while (!cancellationToken.IsCancellationRequested)
            //    {
            //        await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);

            //        try
            //        {
            //            var dirSnap = new DirSnap(_directoryPath);
            //            var filesToRemove = dirSnap.GetFiles().Skip(_settings.NumberOfFilesToKeep).ToList();

            //            foreach (var file in filesToRemove)
            //            {
            //                await DeleteFile(file, cancellationToken);
            //            }
            //        }
            //        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            //        {
            //            // ok
            //        }
            //        catch (Exception exception)
            //        {
            //            _logger.Warning(exception, "Error when cleaning up old files");
            //        }
            //    }
            //}
            //catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            //{
            //    // ok
            //}
            //catch (Exception exception)
            //{
            //    _logger.Error(exception, "Unhandled exception, cleaner loop failed");
            //}
        }

        async Task RunWorker()
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
                        await WriteBufferedTasks(dirSnap, cancellationToken);
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

                    await WriteBufferedTasks(dirSnap, cancellationToken);
                }

                _logger.Verbose("Write task buffer empty");
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
                _logger.Verbose("Cleaning up");

                _currentWriter?.Dispose();
                _currentWriter = null;

                _logger.Information("Writer worker loop stopped");
            }
        }

        async Task WriteBufferedTasks(DirSnap dirSnap, CancellationToken cancellationToken)
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
                await Task.Delay(37, cancellationToken);
                return;
            }

            try
            {
                await WriteTasksAsync(writeTasks, dirSnap);

                _logger.Verbose($"Successfully wrote batch of {writeTasks.Count} messages");

                writeTasks.ForEach(task => task.Complete());
            }
            catch (Exception exception)
            {
                writeTasks.ForEach(task => task.Fail(exception));
            }
        }

        async Task WriteTasksAsync(IEnumerable<WriteTask> writeTasks, DirSnap dirSnap)
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
                foreach (var data in task.Data)
                {
                    var line = Convert.ToBase64String(data);
                    _currentWriter.Write(line);
                    _currentWriter.WriteLine(LineTerminator);
                    _approxBytesWritten += line.Length + 1;
                    flushNeeded = true;

                    if (_approxBytesWritten > _settings.ApproximateMaximumFileLength)
                    {
                        await _currentWriter.FlushAsync();
                        flushNeeded = false;
                        _currentWriter.Dispose();

                        var nextFileNumber = dirSnap.LastFile().FileNumber + 1;
                        var nextFilePath = dirSnap.GetFilePath(nextFileNumber);
                        dirSnap.RegisterFile(nextFilePath);

                        _approxBytesWritten = 0;
                        _currentWriter = GetWriter(nextFilePath);

                        var allFiles = dirSnap.GetFiles().ToList();

                        var filesToDelete = allFiles
                            .Take(allFiles.Count - _settings.NumberOfFilesToKeep)
                            .ToList();

                        foreach (var file in filesToDelete)
                        {
                            try
                            {
                                File.Delete(file.FilePath);
                                dirSnap.RemoveFile(file);
                                _logger.Verbose($"Deleted file {file.FilePath}");
                            }
                            catch (Exception exception)
                            {
                                _logger.Information($"Could not delete file {file.FilePath}: {exception.Message}");
                                break;
                            }
                        }
                    }
                }
            }

            if (flushNeeded)
            {
                await _currentWriter.FlushAsync();
            }
        }

        void AcquireLockFile(string directoryPath, CancellationToken cancellationToken, int timeoutSeconds)
        {
            var stopwatch = Stopwatch.StartNew();
            var lockFilePath = Path.Combine(directoryPath, "kafkaesque.lockfile");
            var timeout = TimeSpan.FromSeconds(timeoutSeconds);
            var lockFileTimeoutMessageLogged = false;

            _logger.Verbose($"Acquiring lock file {lockFilePath}");

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

                    _logger.Verbose($"Will wait up to {timeout} for lock file to be acquired");

                    lockFileTimeoutMessageLogged = true;
                }
            }

            throw new TimeoutException($"Could not acquire lock file '{lockFilePath}' within {timeout} timeout");
        }

        async Task DeleteFile(FileSnap file, CancellationToken cancellationToken)
        {
            var filePath = file.FilePath;
            try
            {
                File.Delete(filePath);
                _logger.Verbose($"Deleted file {filePath}");
            }
            catch (Exception exception)
            {
                throw new IOException($"Could not delete file {filePath}", exception);
            }
        }

        StreamWriter GetWriter(string filePath)
        {
            var stream = File.Open(filePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);

            _logger.Verbose($"Created new writer for file {filePath}");

            return new StreamWriter(stream, Encoding.UTF8);
        }

        /// <summary>
        /// Disposes the writer, giving the background writer task up to 3 s to finish writing
        /// </summary>
        public void Dispose()
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

                    if (!_workerTask.Wait(timeout))
                    {
                        _logger.Warning($"Worker loop did not finish working within {timeout} timeout - task state is {_workerTask.Status}");
                    }
                }

                _currentWriter?.Dispose();
                _currentWriter = null;

                if (_lockFileHandle != null)
                {
                    _lockFileHandle.Dispose();
                    _logger.Verbose("Lock file released");
                }
            }
            finally
            {
                _disposed = true;
            }
        }
    }
}