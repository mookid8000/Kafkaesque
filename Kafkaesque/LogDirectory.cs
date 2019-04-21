using System;
using System.IO;
using System.Threading;
// ReSharper disable SuggestBaseTypeForParameter

namespace Kafkaesque
{
    public class LogDirectory
    {
        readonly string _directoryPath;

        public LogDirectory(DirectoryInfo directoryInfo) : this(directoryInfo.FullName)
        {
            if (directoryInfo == null) throw new ArgumentNullException(nameof(directoryInfo));
        }

        public LogDirectory(string directoryPath)
        {
            _directoryPath = directoryPath ?? throw new ArgumentNullException(nameof(directoryPath));

            if (!Directory.Exists(directoryPath))
            {
                CreateLogDirectory(directoryPath);
            }
        }

        //public LogWriter GetWriter(CancellationToken cancellationToken = default) => new ThreadLogWriter(_directoryPath, cancellationToken);
        //public LogWriter GetWriter(CancellationToken cancellationToken = default) => new CrazyLogWriter(_directoryPath, cancellationToken);

        public LogWriter GetWriter(CancellationToken cancellationToken = default) => new TaskLogWriter(_directoryPath, cancellationToken);

        public LogReader GetReader() => new LogReader(_directoryPath);

        static void CreateLogDirectory(string directoryPath)
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
}
