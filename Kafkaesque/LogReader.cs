using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using Kafkaesque.Internals;

namespace Kafkaesque
{
    /// <summary>
    /// Log reader for reading logs :)
    /// </summary>
    public class LogReader
    {
        readonly string _directoryPath;
        readonly Settings _settings;
        readonly ILogger _logger;

        internal LogReader(string directoryPath, Settings settings)
        {
            _directoryPath = directoryPath ?? throw new ArgumentNullException(nameof(directoryPath));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _logger = settings.Logger;
        }

        /// <summary>
        /// Initiates a read operation. Optionally resumes from a specific file number/byte position.
        /// Reading is cancelled when the <paramref name="cancellationToken"/> is signaled. If <paramref name="throwWhenCancelled"/> is true,
        /// an <see cref="OperationCanceledException"/> is thrown upon cancellation - if it's false, then the iterator simply breaks.
        /// </summary>
        public IEnumerable<LogEvent> Read(int fileNumber = -1, int bytePosition = -1, CancellationToken cancellationToken = default, bool throwWhenCancelled = false)
        {
            if (fileNumber < -1) throw new ArgumentOutOfRangeException(nameof(fileNumber), fileNumber, "Please pass either -1 (to start reading from the beginning) or an actual file number");
            if (bytePosition < -1) throw new ArgumentOutOfRangeException(nameof(bytePosition), bytePosition, "Please pass either -1 (to start reading from the beginning) or an actual byte position");
            if (bytePosition >= 0 && fileNumber == -1) throw new ArgumentException($"Cannot start reading from byte position {bytePosition} when file number is -1, because that doesn't make sense");

            _logger.Verbose($"Initiating read from file {fileNumber} position {bytePosition}");

            while (true)
            {
                if (throwWhenCancelled)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }
                else if (cancellationToken.IsCancellationRequested)
                {
                    yield break;
                }

                var (reader, filePath, canRead) = fileNumber == -1
                    ? GetStreamReader(0, -1)
                    : GetStreamReader(fileNumber, bytePosition);

                if (!canRead)
                {
                    Thread.Sleep(1000);
                    continue;
                }

                var didReadEvents = false;

                foreach (var message in ReadUsing(reader, filePath, cancellationToken, throwWhenCancelled))
                {
                    fileNumber = message.FileNumber;
                    bytePosition = message.BytePosition;

                    yield return message;

                    didReadEvents = true;
                }

                if (didReadEvents)
                {
                    Thread.Sleep(300);
                    continue;
                }

                var (nextReader, nextFilePath, nextCanRead) = GetStreamReader(fileNumber + 1, -1);

                if (!nextCanRead)
                {
                    Thread.Sleep(1000);
                    continue;
                }

                _logger.Verbose($"Next file {nextFilePath} seems to be ready for reading - ensuring that previous file has been fully read");

                // first: be absolutely sure that the previous reader does not have eny more events for us
                var (prevReader, prevFilePath, prevCanRead) = GetStreamReader(fileNumber, bytePosition);

                if (prevCanRead)
                {
                    _logger.Verbose($"Reading the last of the previous file {prevFilePath}");

                    foreach (var message in ReadUsing(prevReader, prevFilePath, cancellationToken, throwWhenCancelled))
                    {
                        fileNumber = message.FileNumber;
                        bytePosition = message.BytePosition;

                        yield return message;
                    }
                }
                else
                {
                    _logger.Verbose($"Could not read more from the previous file {prevFilePath}");
                }

                foreach (var message in ReadUsing(nextReader, nextFilePath, cancellationToken, throwWhenCancelled))
                {
                    fileNumber = message.FileNumber;
                    bytePosition = message.BytePosition;

                    yield return message;
                }
            }
        }


        IEnumerable<LogEvent> ReadUsing(StreamReader reader, string filePath, CancellationToken cancellationToken, bool throwWhenCancelled)
        {
            var fileNumber = FileSnap.Create(filePath).FileNumber;
            var lineCounter = 0;

            try
            {
                using (reader)
                {
                    string line;

                    var firstIteration = true;

                    while ((line = reader.ReadLine()) != null)
                    {
                        if (throwWhenCancelled)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                        }
                        else if (cancellationToken.IsCancellationRequested)
                        {
                            yield break;
                        }

                        if (!line.EndsWith("#"))
                        {
                            _logger.Verbose($"Line {line} did not end with #");
                            yield break;
                        }

                        lineCounter++;

                        if (firstIteration)
                        {
                            _logger.Verbose($"Successfully initiated read operation from file {filePath}");
                            firstIteration = false;
                        }

                        var position = reader.GetBytePosition();
                        var data = Convert.FromBase64String(line.Substring(0, line.Length - 1));

                        yield return new LogEvent(data, fileNumber, position);
                    }
                }
            }
            finally
            {
                if (lineCounter > 0)
                {
                    _logger.Verbose($"Successfully read {lineCounter} lines from file {filePath}");
                }
            }
        }

        (StreamReader reader, string filePath, bool canRead) GetStreamReader(int fileNumber, int bytePosition)
        {
            var dirSnap = new DirSnap(_directoryPath);

            if (fileNumber == -1 && dirSnap.IsEmpty) return (null, null, false);

            var filePath = fileNumber == -1
                ? dirSnap.FirstFile().FilePath
                : dirSnap.GetFilePath(fileNumber);

            FileStream GetStreamOrNull()
            {
                try
                {
                    return File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                }
                catch (FileNotFoundException)
                {
                }
                catch (Exception exception)
                {
                    _logger.Verbose(exception, $"Got exception when trying to open {filePath}");
                }
                return null;
            }

            var stream = GetStreamOrNull();

            if (stream == null) return (null, null, false);

            if (bytePosition != -1)
            {
                try
                {
                    stream.Position = bytePosition;
                }
                catch (Exception exception)
                {
                    stream.Dispose();
                    _logger.Verbose(exception, $"Got exception when trying to file {filePath} stream to position {bytePosition}");
                    return (null, null, false);
                }
            }

            return (new StreamReader(stream, Encoding.UTF8), filePath, true);
        }
    }
}