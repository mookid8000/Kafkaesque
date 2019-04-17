using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Kafkaesque.Internals;

namespace Kafkaesque
{
    public class LogReader : IDisposable
    {
        readonly DirSnap _dirSnap;

        StreamReader _currentReader;
        string _leftOvers;

        internal LogReader(string directoryPath)
        {
            _dirSnap = new DirSnap(directoryPath);
        }

        public IEnumerable<LogEvent> Read(int fileNumber = -1, int bytePosition = -1)
        {
            if (_dirSnap.IsEmpty)
            {
                yield break;
            }

            _currentReader = _currentReader ?? (_currentReader = GetCurrentReader(fileNumber, bytePosition));

            string line;

            while ((line = _currentReader.ReadLine()) != null)
            {
                if (!line.EndsWith("#"))
                {
                    _leftOvers = line;
                    yield break;
                }

                if (_leftOvers != null)
                {
                    line = string.Concat(_leftOvers, line);
                    _leftOvers = null;
                }

                var position = _currentReader.GetBytePosition();
                var data = Convert.FromBase64String(line.Substring(0, line.Length - 1));

                yield return new LogEvent(data, fileNumber, position);
            }
        }

        StreamReader GetCurrentReader(int fileNumber, int bytePosition)
        {
            while (true)
            {
                var filePath = fileNumber == -1
                    ? _dirSnap.FirstFile().FilePath
                    : _dirSnap.GetFilePath(fileNumber);

                var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

                if (bytePosition != -1)
                {
                    stream.Position = bytePosition;
                }

                return new StreamReader(stream, Encoding.UTF8);
            }
        }

        public void Dispose()
        {
            _currentReader?.Dispose();
        }
    }
}