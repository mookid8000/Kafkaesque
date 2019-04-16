using System;
using System.IO;

namespace Kafkaesque
{
    public class LogDirectory
    {
        readonly string _directoryPath;

        public LogDirectory(string directoryPath)
        {
            _directoryPath = directoryPath;

            if (!Directory.Exists(directoryPath))
            {
                try
                {
                    Directory.CreateDirectory(directoryPath);
                }
                catch(Exception exception)
                {
                    if (Directory.Exists(directoryPath)) return;

                    throw new IOException($"Could not create directory '{directoryPath}'", exception);
                }
            }
        }

        public LogWriter GetWriter() => new LogWriter(_directoryPath);

        public LogReader GetReader() => new LogReader(_directoryPath);
    }
}
