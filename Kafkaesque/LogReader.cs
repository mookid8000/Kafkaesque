using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using Kafkaesque.Internals;
using Serilog;

namespace Kafkaesque
{
    /// <summary>
    /// Log reader for reading logs :)
    /// </summary>
    public class LogReader
    {
        readonly string _directoryPath;
        readonly ILogger _logger;

        internal LogReader(string directoryPath)
        {
            _directoryPath = directoryPath;
            _logger = Log.ForContext<LogReader>().ForContext("dir", directoryPath);
        }

        /// <summary>
        /// Initiates a read operation. 
        /// </summary>
        /// <param name="fileNumber"></param>
        /// <param name="bytePosition"></param>
        /// <param name="cancellationToken"></param>
        /// <param name="throwWhenCancelled"></param>
        /// <returns></returns>
        public IEnumerable<LogEvent> Read(int fileNumber = -1, int bytePosition = -1, CancellationToken cancellationToken = default, bool throwWhenCancelled = false)
        {
            if (fileNumber < -1) throw new ArgumentOutOfRangeException(nameof(fileNumber), fileNumber, "Please pass either -1 (to start reading from the beginning) or an actual file number");
            if (bytePosition < -1) throw new ArgumentOutOfRangeException(nameof(bytePosition), bytePosition, "Please pass either -1 (to start reading from the beginning) or an actual byte position");
            if (bytePosition >= 0 && fileNumber == -1) throw new ArgumentException($"Cannot start reading from byte position {bytePosition} when file number is -1, because that doesn't make sense");

            _logger.Verbose("Initiating read from file {fileNumber} position {bytePosition}", fileNumber, bytePosition);

            // use these two to remember if we've done an empty read, in which case we might try and advance the file pointer
            var didReadEvents = true;

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

                var (reader, filePath, canRead) = GetStreamReader(fileNumber, bytePosition);

                // if we can't read from here, try the next file
                if (!canRead || !didReadEvents)
                {
                    reader?.Dispose();

                    _logger.Verbose("Could not read from file {fileNumber} position {bytePosition} - trying next file", fileNumber, bytePosition);

                    var (nextReader, nextFilePath, nextCanRead) = GetStreamReader(fileNumber + 1, -1);

                    // if we still can't read, wait a short while and continue
                    if (!nextCanRead)
                    {
                        Thread.Sleep(200);
                        continue;
                    }

                    _logger.Verbose("Next file {filePath} can be read", nextFilePath);

                    // if we can read, we need to be absolutely sure that we've read everything from the previous file - therefore:
                    if (!string.IsNullOrWhiteSpace(filePath))
                    {
                        (reader, filePath, canRead) = GetStreamReader(fileNumber, bytePosition);

                        // if we can read from the previous reader
                        if (canRead)
                        {
                            // we need to dispose this new one
                            nextReader?.Dispose();

                            _logger.Verbose("Ensuring that we read the last of the file {filePath}", filePath);

                            // read the last parts from the old file
                            foreach (var message in ReadUsing(reader, filePath, cancellationToken, throwWhenCancelled))
                            {
                                fileNumber = message.FileNumber;
                                bytePosition = message.BytePosition;

                                yield return message;
                            }

                            reader?.Dispose();

                            _logger.Verbose("Re-initializing reader for next file {filePath}", nextFilePath);

                            // and then re-init the new reader
                            (nextReader, nextFilePath, _) = GetStreamReader(fileNumber + 1, -1);
                        }

                        reader?.Dispose();
                    }

                    reader = nextReader;
                    filePath = nextFilePath;
                }

                didReadEvents = false;

                foreach (var message in ReadUsing(reader, filePath, cancellationToken, throwWhenCancelled))
                {
                    fileNumber = message.FileNumber;
                    bytePosition = message.BytePosition;

                    yield return message;

                    didReadEvents = true;
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
                            _logger.Verbose("Line {line} did not end with #", line);
                            yield break;
                        }

                        lineCounter++;

                        if (firstIteration)
                        {
                            _logger.Verbose("Successfully initiated read operation from file {filePath}", filePath);
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
                    _logger.Verbose("Successfully read {count} lines from file {filePath}", lineCounter, filePath);
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
                    _logger.Verbose(exception, "Got exception when trying to open {filePath}", filePath);
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
                    _logger.Verbose(exception, "Got exception when trying to file {filePath} stream to position {bytePosition}", filePath, bytePosition);
                    return (null, null, false);
                }
            }

            return (new StreamReader(stream, Encoding.UTF8), filePath, true);
        }
    }
}