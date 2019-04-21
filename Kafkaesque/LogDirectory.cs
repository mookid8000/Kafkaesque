using System;
using System.IO;
using System.Threading;

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
                catch (Exception exception)
                {
                    if (Directory.Exists(directoryPath)) return;

                    throw new IOException($"Could not create directory '{directoryPath}'", exception);
                }
            }
        }

        //public LogWriter GetWriter(CancellationToken cancellationToken = default) => new ThreadLogWriter(_directoryPath, cancellationToken);
        public LogWriter GetWriter(CancellationToken cancellationToken = default) => new TaskLogWriter(_directoryPath, cancellationToken);
        //public LogWriter GetWriter(CancellationToken cancellationToken = default) => new CrazyLogWriter(_directoryPath, cancellationToken);

        public LogReader GetReader() => new LogReader(_directoryPath);
    }
}
