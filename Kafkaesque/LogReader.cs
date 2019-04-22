using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Kafkaesque.Internals;

namespace Kafkaesque
{
    public class LogReader
    {
        readonly string _directoryPath;

        internal LogReader(string directoryPath)
        {
            _directoryPath = directoryPath;
        }

        public IEnumerable<LogEvent> Read(int fileNumber = -1, int bytePosition = -1, CancellationToken cancellationToken = default)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var reader = GetStreamReader(fileNumber, bytePosition);

                foreach (var message in ReadUsing(reader.reader, reader.filePath))
                {
                    fileNumber = message.FileNumber;
                    bytePosition = message.BytePosition;

                    yield return message;
                }
            }
        }

        IEnumerable<LogEvent> ReadUsing(StreamReader reader, string filePath)
        {
            var fileNumber = FileSnap.Create(filePath).FileNumber;

            using (reader)
            {
                string line;

                while ((line = reader.ReadLine()) != null)
                {
                    if (!line.EndsWith("#"))
                    {
                        yield break;
                    }

                    var position = reader.GetBytePosition();
                    var data = Convert.FromBase64String(line.Substring(0, line.Length - 1));

                    yield return new LogEvent(data, fileNumber, position);
                }
            }
        }

        (StreamReader reader, string filePath) GetStreamReader(int fileNumber, int bytePosition)
        {
            var dirSnap = new DirSnap(_directoryPath);

            if (fileNumber == -1 && dirSnap.IsEmpty) return (null, null);

            var filePath = fileNumber == -1
                ? dirSnap.FirstFile().FilePath
                : dirSnap.GetFilePath(fileNumber);

            var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

            if (bytePosition != -1)
            {
                try
                {
                    stream.Position = bytePosition;
                }
                catch
                {
                    stream.Dispose();
                    throw;
                }
            }

            return (new StreamReader(stream, Encoding.UTF8), filePath);
        }
    }
}