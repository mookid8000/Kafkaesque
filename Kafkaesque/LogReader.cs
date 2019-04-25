using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using Kafkaesque.Internals;
using Serilog;

namespace Kafkaesque
{
    public class LogReader
    {
        readonly string _directoryPath;
        readonly ILogger _logger;

        internal LogReader(string directoryPath)
        {
            _directoryPath = directoryPath;
            _logger = Log.ForContext<LogReader>().ForContext("dir", directoryPath);
        }

        public IEnumerable<LogEvent> Read(int fileNumber = -1, int bytePosition = -1, CancellationToken cancellationToken = default)
        {
            _logger.Verbose("Initiating read from file {fileNumber} position {bytePosition}", fileNumber, bytePosition);

            // use these two to remember if we've done an empty read, in which case we might try and advance the file pointer
            var didReadEvents = true;

            while (!cancellationToken.IsCancellationRequested)
            {
                var (reader, filePath, canRead) = GetStreamReader(fileNumber, bytePosition);

                // if we can't read from here, try the next file
                if (!canRead || !didReadEvents)
                {
                    _logger.Verbose("Could not read from file {fileNumber} position {bytePosition} - trying next file", fileNumber, bytePosition);

                    var (nextReader, nextFilePath, nextCanRead) = GetStreamReader(fileNumber + 1, -1);

                    // if we still can't read, wait a short while and continue
                    if (!nextCanRead)
                    {
                        reader?.Dispose();
                        Thread.Sleep(200);
                        continue;
                    }

                    // if we can read, we need to be absolutely sure that we've read everything from the previous file - therefore:
                    if (!string.IsNullOrWhiteSpace(filePath))
                    {
                        _logger.Verbose("Next file {filePath} can be read", nextFilePath);

                        reader?.Dispose();

                        (reader, filePath, canRead) = GetStreamReader(fileNumber, bytePosition);

                        if (canRead)
                        {
                            _logger.Verbose("Ensuring that we read the last of the file {filePath}", filePath);

                            foreach (var message in ReadUsing(reader, filePath))
                            {
                                fileNumber = message.FileNumber;
                                bytePosition = message.BytePosition;

                                yield return message;
                            }
                        }
                    }
                    else
                    {
                        _logger.Verbose("So, we're reading from {filePath} now", nextFilePath);
                    }

                    reader = nextReader;
                    filePath = nextFilePath;
                }

                didReadEvents = false;

                foreach (var message in ReadUsing(reader, filePath))
                {
                    fileNumber = message.FileNumber;
                    bytePosition = message.BytePosition;

                    yield return message;

                    didReadEvents = true;
                }
            }
        }

        IEnumerable<LogEvent> ReadUsing(StreamReader reader, string filePath)
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